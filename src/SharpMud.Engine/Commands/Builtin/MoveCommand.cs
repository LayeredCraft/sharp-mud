using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

// One instance per direction, pre-bound at registration (docs/commands.md
// movement walkthrough, step 3) - e.g. new MoveCommand(Direction.North,
// "north", ["n"]).
public sealed class MoveCommand(Direction direction, string verb, IReadOnlyList<string> aliases) : ICommand
{
    public string Verb { get; } = verb;
    public IReadOnlyList<string> Aliases { get; } = aliases;

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var exit = ctx.CurrentRoom.FindExit(direction);
        if (exit is null)
        {
            await ctx.Session.WriteLineAsync("You can't go that way.", ct);
            return;
        }

        if (exit.Lock is { IsLocked: true })
        {
            await ctx.Session.WriteLineAsync("The door is locked.", ct);
            return;
        }

        var destination = ctx.World.GetRoom(exit.DestinationRoomId);
        if (destination is null)
        {
            await ctx.Session.WriteLineAsync("You can't go that way.", ct);
            return;
        }

        await ctx.World.MovePlayerAsync(ctx.Actor, ctx.CurrentRoom, destination, direction, ct);
        await LookCommand.SendRoomDescriptionAsync(ctx.Actor, destination, ctx.World, ctx.Session, ct);
    }
}
