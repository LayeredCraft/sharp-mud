using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SharpMud.Engine.Sessions;

namespace SharpMud.Adapters.Telnet;

public sealed class TelnetListener
{
    private readonly TcpListener _listener;
    private readonly ILogger<TelnetSession> _sessionLogger;

    public TelnetListener(int port, ILogger<TelnetSession> sessionLogger)
    {
        ArgumentNullException.ThrowIfNull(sessionLogger);

        _listener = new TcpListener(IPAddress.Any, port);
        _sessionLogger = sessionLogger;
    }

    public void Start() => _listener.Start();

    public void Stop() => _listener.Stop();

    // The actual bound port - only meaningful after Start(), and needed
    // when constructed with port 0 (OS picks a free port), e.g. in tests
    // that connect a real client.
    internal int LocalPort => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public async IAsyncEnumerable<ISession> AcceptSessionsAsync([EnumeratorCancellation] CancellationToken ct)
    {
        // Belt-and-suspenders: AcceptTcpClientAsync(ct) is documented to
        // honor cancellation, but observed in practice (this SDK/preview)
        // to hang past a cancelled token with no connections pending -
        // Stop() unblocks it immediately via a thrown exception regardless.
        await using var registration = ct.Register(() => _listener.Stop());

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }
            catch (SocketException)
            {
                yield break;
            }

            var session = new TelnetSession(client, _sessionLogger);
            await session.StartNegotiationAsync(ct);
            yield return session;
        }
    }
}
