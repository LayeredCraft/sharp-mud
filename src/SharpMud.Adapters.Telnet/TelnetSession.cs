using System.Net.Sockets;
using System.Text;
using SharpMud.Engine.Sessions;

namespace SharpMud.Adapters.Telnet;

// Minimal telnet transport: raw TCP + line-based I/O with IAC (0xFF) byte
// sequences stripped from input. Deliberately does not negotiate MCCP/MXP/
// NAWS (docs/networking.md defers full protocol handling; WheelMUD's
// Server/Telnet/ is the reference to consult when that's actually needed).
public sealed class TelnetSession(TcpClient client) : ISession, IDisposable
{
    private readonly NetworkStream _stream = client.GetStream();
    private readonly StreamWriter _writer = new(client.GetStream(), Encoding.ASCII) { AutoFlush = true };

    public string SessionId { get; } = Guid.NewGuid().ToString();
    public bool IsConnected => client.Connected;

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

            if (b == 0xFF) // IAC - consume this command sequence, don't forward it as text
            {
                await SkipTelnetCommandAsync(ct);
                continue;
            }

            if (b == (byte)'\n')
                break;

            if (b != (byte)'\r')
                line.Add(b);
        }

        return Encoding.ASCII.GetString(line.ToArray());
    }

    // IAC is always followed by at least one more byte (the command); WILL/
    // WONT/DO/DONT commands carry a further option byte. Anything else is
    // read and discarded without attempting full option negotiation.
    private async Task SkipTelnetCommandAsync(CancellationToken ct)
    {
        var buffer = new byte[1];
        if (await _stream.ReadAsync(buffer, ct) == 0)
            return;

        var command = buffer[0];
        if (command is >= 251 and <= 254) // WILL, WONT, DO, DONT
            await _stream.ReadAsync(buffer, ct);
    }

    public async ValueTask WriteLineAsync(string text, CancellationToken ct) => await _writer.WriteLineAsync(text);

    public async ValueTask WriteAsync(string text, CancellationToken ct) => await _writer.WriteAsync(text);

    public ValueTask DisconnectAsync(string? reason, CancellationToken ct)
    {
        client.Close();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
        client.Dispose();
    }
}
