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
        var listener = new TelnetListener(0); // port 0 = OS picks a free port
        listener.Start();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var _ in listener.AcceptSessionsAsync(cts.Token))
            {
                // No connections expected - loop should simply end.
            }
        });

        var completed = await Task.WhenAny(enumerationTask, Task.Delay(TimeSpan.FromSeconds(5)));

        completed.Should().Be(enumerationTask, "AcceptSessionsAsync should stop shortly after cancellation, not hang");
    }
}
