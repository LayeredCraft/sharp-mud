using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

/// <summary>
/// Wires a two-way exit between two rooms - one <see cref="ExitBehavior"/>
/// per direction of travel (see <see cref="ExitBehavior"/>'s own remarks on
/// why there's no single bidirectional exit type), each registered in the
/// world and attached as a child of the room it exits from. Shared by
/// world-boot content (<c>BasicWorldBuilder</c>) and the runtime
/// <c>dig</c>/<c>tunnel</c> commands (ADR-0009) - both need to build the
/// exact same pair of Things, and this is the one place that knows how.
/// </summary>
public static class RoomConnector
{
    /// <summary>Connects <paramref name="a"/> to <paramref name="b"/> via <paramref name="direction"/>, and <paramref name="b"/> back to <paramref name="a"/> via its opposite.</summary>
    public static void Connect(IWorld world, Thing a, Thing b, Direction direction)
    {
        var aToB = new Thing { Id = ThingId.New(), Name = direction.ToDisplayString() };
        aToB.Behaviors.Add(new ExitBehavior { Direction = direction, Destination = b });
        a.Add(aToB);
        world.Register(aToB);

        var bToA = new Thing { Id = ThingId.New(), Name = direction.Opposite().ToDisplayString() };
        bToA.Behaviors.Add(new ExitBehavior { Direction = direction.Opposite(), Destination = a });
        b.Add(bToA);
        world.Register(bToA);
    }
}
