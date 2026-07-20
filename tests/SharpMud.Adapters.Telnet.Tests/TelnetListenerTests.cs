using Microsoft.Extensions.Logging;

namespace SharpMud.Adapters.Telnet.Tests;

public sealed class TelnetListenerTests
{
    // Regression test for a real bug: AcceptTcpClientAsync(ct) was observed
    // hanging indefinitely past a cancelled token with no connection pending
    // (this SDK/preview), which meant graceful shutdown (SIGINT/SIGTERM)
    // never completed - see docs/networking.md / SharpMud.Host's shutdown
    // wiring. TelnetListener now proactively Stop()s itself on cancellation
    // as a fallback; this test fails (times out) if that regresses.
    [Fact]
    public async Task AcceptSessionsAsync_StopsPromptly_WhenCancelledWithNoConnectionsPending()
    {
        var listener = new TelnetListener(0, Substitute.For<ILogger<TelnetSession>>()); // port 0 = OS picks a free port
        listener.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        // cts.Token IS derived from TestContext.Current.CancellationToken
        // (linked above) plus a CancelAfter - xUnit1051 doesn't trace that
        // and flags this line as a false positive.
#pragma warning disable xUnit1051
        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var _ in listener.AcceptSessionsAsync(cts.Token))
            {
                // No connections expected - loop should simply end.
            }
        });
#pragma warning restore xUnit1051

        var completed = await Task.WhenAny(enumerationTask, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.Should().Be(enumerationTask, "AcceptSessionsAsync should stop shortly after cancellation, not hang");
    }
}
