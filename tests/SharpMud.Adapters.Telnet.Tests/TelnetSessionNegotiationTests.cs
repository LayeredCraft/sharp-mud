using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SharpMud.Adapters.Telnet.Tests;

// Real-loopback integration tests, extending TelnetListenerTests's pattern:
// a real TcpListener/TcpClient pair, driving TelnetSession end-to-end
// through actual socket I/O rather than faking the transport.
public sealed class TelnetSessionNegotiationTests
{
    [Fact]
    public async Task NegotiatesNaws_AndUpdatesTerminalDimensions()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.StartAsync(ct);

        // Server should have already sent IAC DO NAWS on connect.
        var doNaws = await harness.ReadServerBytesAsync(3, ct);
        doNaws.Should().Equal(255, 253, 31);

        // Client agrees and reports a 120x50 window.
        await harness.WriteFromClientAsync([255, 251, 31], ct); // IAC WILL NAWS
        await harness.WriteFromClientAsync([255, 250, 31, 0, 120, 0, 50, 255, 240], ct); // IAC SB NAWS 120 50 IAC SE
        await harness.WriteFromClientAsync("look\r\n"u8.ToArray(), ct);

        var line = await harness.Session.ReadLineAsync(ct);

        line.Should().Be("look");
        harness.Session.TerminalWidth.Should().Be(120);
        harness.Session.TerminalHeight.Should().Be(50);
    }

    [Fact]
    public async Task ReadLineAsync_StillReadsPlainText_WhenInterleavedWithIacBytes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.StartAsync(ct);
        await harness.DrainInitialNegotiationAsync(ct);

        await harness.WriteFromClientAsync(Encoding.ASCII.GetBytes("say hel"), ct);
        await harness.WriteFromClientAsync([255, 251, 99], ct); // an unrelated IAC sequence mid-line
        await harness.WriteFromClientAsync(Encoding.ASCII.GetBytes("lo\r\n"), ct);

        var line = await harness.Session.ReadLineAsync(ct);

        line.Should().Be("say hello");
    }

    [Fact]
    public async Task HandleIacAsync_RefusesUnrequestedOption_OverTheRealSocket()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.StartAsync(ct);
        await harness.DrainInitialNegotiationAsync(ct);

        await harness.WriteFromClientAsync([255, 253, 99], ct); // IAC DO <unregistered option 99>
        await harness.WriteFromClientAsync("\r\n"u8.ToArray(), ct); // unblock ReadLineAsync so the byte gets processed
        await harness.Session.ReadLineAsync(ct);

        var response = await harness.ReadServerBytesAsync(3, ct);

        response.Should().Equal(255, 252, 99); // IAC WONT 99
    }

    [Fact]
    public async Task SetEchoAsync_StillEmitsTheSameWireBytes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await Harness.StartAsync(ct);
        await harness.DrainInitialNegotiationAsync(ct);

        await harness.Session.SetEchoAsync(enabled: false, ct);
        var willEcho = await harness.ReadServerBytesAsync(3, ct);

        // Confirm the WILL before requesting WONT - a second request while
        // the first is still unconfirmed would correctly just queue (see
        // TelnetOptionNegotiatorTests), not re-send, which isn't what this
        // test is checking.
        await harness.WriteFromClientAsync([255, 253, 1], ct); // IAC DO ECHO
        await harness.WriteFromClientAsync("\r\n"u8.ToArray(), ct);
        await harness.Session.ReadLineAsync(ct);

        await harness.Session.SetEchoAsync(enabled: true, ct);
        var wontEcho = await harness.ReadServerBytesAsync(3, ct);

        willEcho.Should().Equal(255, 251, 1); // IAC WILL ECHO
        wontEcho.Should().Equal(255, 252, 1); // IAC WONT ECHO
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly TelnetListener _listener;
        private readonly TcpClient _client;
        private readonly NetworkStream _clientStream;

        internal required TelnetSession Session { get; init; }

        private Harness(TelnetListener listener, TcpClient client, NetworkStream clientStream)
        {
            _listener = listener;
            _client = client;
            _clientStream = clientStream;
        }

        internal static async Task<Harness> StartAsync(CancellationToken ct)
        {
            var listener = new TelnetListener(0, Substitute.For<ILogger<TelnetSession>>());
            listener.Start();

            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, listener.LocalPort, ct);

            var sessionsEnumerator = listener.AcceptSessionsAsync(ct).GetAsyncEnumerator(ct);
            await sessionsEnumerator.MoveNextAsync();
            var session = (TelnetSession)sessionsEnumerator.Current;

            return new Harness(listener, client, client.GetStream()) { Session = session };
        }

        internal async Task DrainInitialNegotiationAsync(CancellationToken ct) =>
            await ReadServerBytesAsync(3, ct); // the IAC DO NAWS sent at StartNegotiationAsync

        internal Task WriteFromClientAsync(byte[] bytes, CancellationToken ct) =>
            _clientStream.WriteAsync(bytes, ct).AsTask();

        internal async Task<byte[]> ReadServerBytesAsync(int count, CancellationToken ct)
        {
            var buffer = new byte[count];
            var read = 0;
            while (read < count)
                read += await _clientStream.ReadAsync(buffer.AsMemory(read, count - read), ct);
            return buffer;
        }

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            _clientStream.Dispose();
            _client.Dispose();
            _listener.Stop();
            await Task.CompletedTask;
        }
    }
}
