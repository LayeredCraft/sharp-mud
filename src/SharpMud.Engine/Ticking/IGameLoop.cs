namespace SharpMud.Engine.Ticking;

public interface IGameLoop
{
    void Register(ITickable tickable);
    void Unregister(ITickable tickable);

    // Runs until ct is cancelled - intended to be started as a background
    // task from Host once something actually implements ITickable (combat -
    // see docs/combat.md). Not started yet in v1, since nothing ticks.
    Task RunAsync(CancellationToken ct);
}
