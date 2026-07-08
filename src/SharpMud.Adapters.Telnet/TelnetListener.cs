using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using SharpMud.Engine.Sessions;

namespace SharpMud.Adapters.Telnet;

public sealed class TelnetListener(int port)
{
    private readonly TcpListener _listener = new(IPAddress.Any, port);

    public void Start() => _listener.Start();

    public void Stop() => _listener.Stop();

    public async IAsyncEnumerable<ISession> AcceptSessionsAsync([EnumeratorCancellation] CancellationToken ct)
    {
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

            yield return new TelnetSession(client);
        }
    }
}
