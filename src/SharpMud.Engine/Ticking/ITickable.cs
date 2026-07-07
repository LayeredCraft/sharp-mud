namespace SharpMud.Engine.Ticking;

public sealed record TickContext(DateTimeOffset Timestamp);

// Async, not the sync void OnTick sketched in docs/architecture.md - combat
// resolution needs to await ISession writes each round (same reasoning as
// IWorld.MovePlayer becoming MovePlayerAsync).
public interface ITickable
{
    Task OnTickAsync(TickContext ctx, CancellationToken ct);
}
