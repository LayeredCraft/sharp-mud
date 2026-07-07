namespace SharpMud.Engine.Ticking;

public sealed class GameLoopOptions
{
    // Configurable rather than hardcoded per docs/architecture.md; default
    // value is still an open item pending combat pacing/tuning (docs/combat.md).
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(2);
}

public sealed class GameLoop(GameLoopOptions options) : IGameLoop
{
    private readonly List<ITickable> _tickables = [];

    public void Register(ITickable tickable) => _tickables.Add(tickable);

    public void Unregister(ITickable tickable) => _tickables.Remove(tickable);

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(options.TickInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            var context = new TickContext(DateTimeOffset.UtcNow);
            foreach (var tickable in _tickables.ToArray())
                tickable.OnTick(context);
        }
    }
}
