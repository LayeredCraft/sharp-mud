namespace SharpMud.Engine.Ticking;

public sealed record TickContext(DateTimeOffset Timestamp);

public interface ITickable
{
    void OnTick(TickContext ctx);
}
