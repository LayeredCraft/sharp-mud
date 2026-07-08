namespace SharpMud.Engine.Core;

// Drastically smaller than the old Room/Player/Npc/Item-specific IWorld -
// most of what it used to do (PlayersInRoom, GetNpc, GetItem, notifying
// occupants) is now just querying Thing.Children/Behaviors directly, or
// belongs on Behavior/CommandGuards helpers. This is purely a global lookup
// registry, since Thing.Children only gives local containment.
public interface IWorld
{
    Thing? GetThing(ThingId id);
    void Register(Thing thing);
    void Unregister(ThingId id);
    IEnumerable<Thing> AllWithBehavior<T>() where T : Behavior;
}
