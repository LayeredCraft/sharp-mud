using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin;

// One instance per direction, pre-bound at registration - e.g.
// new MoveCommand(Direction.North, "north", ["n"]).
public sealed class MoveCommand(Direction direction, string verb, IReadOnlyList<string> aliases) : ICommand
{
    public string Verb { get; } = verb;
    public IReadOnlyList<string> Aliases { get; } = aliases;

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var exit = ctx.CurrentRoom.Children
            .Select(c => c.FindBehavior<ExitBehavior>())
            .FirstOrDefault(e => e?.Direction == direction);

        if (exit is null)
        {
            await ctx.Session.WriteLineAsync("You can't go that way.", ct);
            return;
        }

        var exitThing = exit.Parent!;
        var request = new UseExitEvent { ActiveThing = ctx.Actor, Exit = exitThing };
        exitThing.Events.PublishRequest(request, EventScope.SelfOnly);
        if (request.IsCanceled)
        {
            await ctx.Session.WriteLineAsync(request.CancelReason ?? "You can't go that way.", ct);
            return;
        }

        var destination = exit.Destination;

        if (!ctx.CurrentRoom.Remove(ctx.Actor))
        {
            await ctx.Session.WriteLineAsync("You can't go that way.", ct);
            return;
        }

        await RoomBroadcast.ToOccupantsAsync(
            ctx.CurrentRoom, ctx.Actor, $"{ctx.Actor.Name} leaves {direction.ToDisplayString()}.", ct);

        destination.Add(ctx.Actor);

        await RoomBroadcast.ToOccupantsAsync(destination, ctx.Actor, $"{ctx.Actor.Name} arrives.", ct);

        await LookCommand.SendRoomDescriptionAsync(ctx.Actor, destination, ct);
    }
}
