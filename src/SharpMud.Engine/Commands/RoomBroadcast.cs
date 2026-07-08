using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands;

// Replaces the old World.PlayersInRoom/GetSession loops duplicated across
// Say/Emote/Get/Drop/Give/Move - finds every PlayerBehavior among a room's
// Children with a connected session and writes to it.
public static class RoomBroadcast
{
    public static async Task ToOccupantsAsync(Thing room, Thing? exclude, string message, CancellationToken ct)
    {
        foreach (var child in room.Children)
        {
            if (child == exclude)
                continue;

            var session = child.FindBehavior<PlayerBehavior>()?.Session;
            if (session is not null)
                await session.WriteLineAsync(message, ct);
        }
    }
}
