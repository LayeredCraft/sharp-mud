using SharpMud.Engine.Characters;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class LookCommand : ICommand
{
    public string Verb => "look";
    public IReadOnlyList<string> Aliases { get; } = ["l"];

    public Task ExecuteAsync(CommandContext ctx, CancellationToken ct) =>
        SendRoomDescriptionAsync(ctx.Actor, ctx.CurrentRoom, ctx.World, ctx.Session, ct);

    // Shared with MoveCommand, which sends the destination's description
    // after a successful move (docs/commands.md movement walkthrough, step 6).
    public static async Task SendRoomDescriptionAsync(
        Player viewer, Room room, IWorld world, ISession session, CancellationToken ct)
    {
        await session.WriteLineAsync(room.Name, ct);
        await session.WriteLineAsync(room.Description, ct);

        var exitList = room.Exits.Count > 0
            ? string.Join(", ", room.Exits.Select(exit => exit.Direction.ToDisplayString()))
            : "none";
        await session.WriteLineAsync($"Exits: {exitList}", ct);

        foreach (var other in world.PlayersInRoom(room.Id).Where(p => p.Id != viewer.Id))
            await session.WriteLineAsync($"{other.Name} is here.", ct);
    }
}
