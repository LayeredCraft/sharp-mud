using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Builder;

// Shared by dig/tunnel - both parse a leading direction argument the same
// way, and both eventually need "walk up to the tree root to save
// everything that changed" (a dug room, and its own reverse exit, live
// outside the current room's own subtree as siblings under the same area,
// not as its descendants - see ADR-0009).
internal static class BuilderCommandHelpers
{
    /// <summary>Parses a direction name (e.g. <c>"north"</c>, <c>"ne"</c>) case-insensitively. Accepts the same names <see cref="DirectionExtensions.ToDisplayString"/> produces, plus the <c>MoveCommand</c> short aliases.</summary>
    public static bool TryParseDirection(string text, out Direction direction)
    {
        switch (text.ToLowerInvariant())
        {
            case "north" or "n": direction = Direction.North; return true;
            case "south" or "s": direction = Direction.South; return true;
            case "east" or "e": direction = Direction.East; return true;
            case "west" or "w": direction = Direction.West; return true;
            case "northeast" or "ne": direction = Direction.NorthEast; return true;
            case "northwest" or "nw": direction = Direction.NorthWest; return true;
            case "southeast" or "se": direction = Direction.SouthEast; return true;
            case "southwest" or "sw": direction = Direction.SouthWest; return true;
            case "up" or "u": direction = Direction.Up; return true;
            case "down" or "d": direction = Direction.Down; return true;
            default: direction = default; return false;
        }
    }

    /// <summary>Finds every registered room whose <see cref="Thing.Name"/> matches <paramref name="name"/> (case-insensitive, exact) - a caller decides how to handle zero or more than one match.</summary>
    public static IReadOnlyList<Thing> FindRoomsByName(IWorld world, string name) =>
        world.AllWithBehavior<RoomBehavior>()
            .Where(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Whether <paramref name="room"/> already has an exit in
    /// <paramref name="direction"/> - <c>MoveCommand</c> resolves exits via
    /// <c>FirstOrDefault</c>, so wiring a second exit in an already-occupied
    /// direction doesn't fail loudly; it silently shadows the new one behind
    /// whichever exit was registered first, and the room's exit listing
    /// starts showing the same direction twice. <see cref="DigCommand"/> and
    /// <see cref="TunnelCommand"/> both check this on the origin room before
    /// connecting anything - <see cref="TunnelCommand"/> also checks it
    /// against the destination room's opposite direction, since that side
    /// can be occupied too.
    /// </summary>
    public static bool HasExit(Thing room, Direction direction) =>
        room.Children.Any(c => c.FindBehavior<ExitBehavior>()?.Direction == direction);

    /// <summary>Walks up <see cref="Thing.Parent"/> to the tree root (the first ancestor with no parent) - the node <see cref="IThingRepository.SaveTreeAsync"/> needs so a save captures every changed Thing, not just the current room's own subtree.</summary>
    public static Thing FindRoot(Thing thing)
    {
        var current = thing;
        while (current.Parent is { } parent)
            current = parent;

        return current;
    }
}
