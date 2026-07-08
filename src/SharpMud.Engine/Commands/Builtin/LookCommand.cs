using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class LookCommand : ICommand
{
    public string Verb => "look";
    public IReadOnlyList<string> Aliases { get; } = ["l"];

    public Task ExecuteAsync(CommandContext ctx, CancellationToken ct) =>
        SendRoomDescriptionAsync(ctx.Actor, ctx.CurrentRoom, ct);

    // Shared with MoveCommand, which sends the destination's description
    // after a successful move.
    public static async Task SendRoomDescriptionAsync(Thing viewer, Thing room, CancellationToken ct)
    {
        var session = viewer.FindBehavior<PlayerBehavior>()?.Session;
        if (session is null)
            return;

        await session.WriteLineAsync(room.Name, ct);
        await session.WriteLineAsync(room.Description, ct);

        var exits = room.Children.Select(c => c.FindBehavior<ExitBehavior>()).OfType<ExitBehavior>().ToList();
        var exitList = exits.Count > 0
            ? string.Join(", ", exits.Select(e => e.Direction.ToDisplayString()))
            : "none";
        await session.WriteLineAsync($"Exits: {exitList}", ct);

        foreach (var other in room.Children.Where(c => c != viewer && c.HasBehavior<PlayerBehavior>()))
            await session.WriteLineAsync($"{other.Name} is here.", ct);

        foreach (var npc in room.Children.Where(c => c.HasBehavior<NpcBehavior>()))
            await session.WriteLineAsync($"{npc.Name} is here.", ct);

        foreach (var item in room.Children.Where(c => c.HasBehavior<ItemBehavior>()))
            await session.WriteLineAsync($"{item.Name} is on the ground.", ct);
    }
}
