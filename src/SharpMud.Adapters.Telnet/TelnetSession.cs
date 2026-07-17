using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using SharpMud.Adapters.Telnet.Negotiation;
using SharpMud.Engine.Sessions;

namespace SharpMud.Adapters.Telnet;

// Telnet transport: raw TCP + line-based I/O, with real IAC option
// negotiation (RFC 1143 "Q-Method" core + NAWS) via TelnetOptionNegotiator -
// see ADR-0002 (docs/adr/0002-telnet-protocol-negotiation.md). MCCP/MXP/
// TermType remain deferred - docs/networking.md Open Items.
public sealed class TelnetSession : ISession, IDisposable, ITelnetByteSink
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly StreamWriter _writer;
    private readonly TelnetOptionNegotiator _negotiator;
    private readonly NawsOptionHandler _naws;

    public string SessionId { get; } = Guid.NewGuid().ToString();
    public bool IsConnected => _client.Connected;
    public int TerminalWidth => _naws.Width;
    public int TerminalHeight => _naws.Height;

    public TelnetSession(TcpClient client, ILogger<TelnetSession> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _stream = client.GetStream();
        _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };
        _naws = new NawsOptionHandler(SessionId, logger);
        _negotiator = new TelnetOptionNegotiator(this, [new EchoOptionHandler(), _naws], logger);
    }

    public async ValueTask<string?> ReadLineAsync(CancellationToken ct)
    {
        var line = new List<byte>();
        var buffer = new byte[1];

        while (true)
        {
            int read;
            try
            {
                read = await _stream.ReadAsync(buffer, ct);
            }
            catch (IOException)
            {
                return null;
            }
            catch (SocketException)
            {
                return null;
            }

            if (read == 0)
                return null;

            var b = buffer[0];

            if (b == 0xFF) // IAC - hand off to option negotiation, don't forward it as text
            {
                await _negotiator.HandleIacAsync(_stream, ct);
                continue;
            }

            if (b == (byte)'\n')
                break;

            if (b != (byte)'\r')
                line.Add(b);
        }

        return Encoding.ASCII.GetString(line.ToArray());
    }

    public async ValueTask WriteLineAsync(string text, CancellationToken ct) => await _writer.WriteLineAsync(text);

    public async ValueTask WriteAsync(string text, CancellationToken ct) => await _writer.WriteAsync(text);

    public ValueTask DisconnectAsync(string? reason, CancellationToken ct)
    {
        _client.Close();
        return ValueTask.CompletedTask;
    }

    // IAC WILL ECHO tells an RFC-1116-compliant client "I (the server) will
    // handle echoing" - compliant clients stop echoing locally, and since we
    // never echo it either, typed input (a password) doesn't appear on
    // screen. IAC WONT ECHO restores normal local echo afterward. Not a
    // security boundary by itself - a noncompliant/raw client can ignore
    // this - just suppresses display for normal telnet clients. See
    // docs/accounts-auth.md Open Items.
    //
    // enabled=true means normal (client-side) echo, which is the ECHO
    // *option* being off from our side (WONT); enabled=false means we take
    // over echoing (the option is on, WILL) - the boolean is inverted
    // relative to RequestLocalAsync's "enable this option" meaning.
    public ValueTask SetEchoAsync(bool enabled, CancellationToken ct) =>
        _negotiator.RequestLocalAsync(TelnetOptionCode.Echo, enable: !enabled, ct);

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
        _client.Dispose();
    }

    // Kicks off NAWS negotiation - fire-and-forget, matching WheelMUD's own
    // negotiation timing (see ADR-0002): the client's response arrives
    // asynchronously via the normal ReadLineAsync loop and updates
    // TerminalWidth/TerminalHeight in place whenever it does.
    internal ValueTask StartNegotiationAsync(CancellationToken ct) =>
        _negotiator.RequestRemoteAsync(TelnetOptionCode.Naws, enable: true, ct);

    ValueTask ITelnetByteSink.WriteAsync(byte[] bytes, CancellationToken ct) => _stream.WriteAsync(bytes, ct);
}
