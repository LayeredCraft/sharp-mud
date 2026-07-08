namespace SharpMud.Engine.Core;

public sealed class World : IWorld
{
    private readonly Dictionary<ThingId, Thing> _things = [];

    public Thing? GetThing(ThingId id) => _things.GetValueOrDefault(id);

    public void Register(Thing thing) => _things[thing.Id] = thing;

    public void Unregister(ThingId id) => _things.Remove(id);

    public IEnumerable<Thing> AllWithBehavior<T>() where T : Behavior =>
        _things.Values.Where(t => t.HasBehavior<T>());
}
