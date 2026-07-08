using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class GetCommand : ICommand
{
    public string Verb => "get";
    public IReadOnlyList<string> Aliases { get; } = ["take"];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Get what?", ct))
            return;

        var target = string.Join(' ', ctx.Args);
        var item = ObjectMatcher.FindMatch(
            ctx.CurrentRoom.Children.Where(c => c.HasBehavior<ItemBehavior>()), target, i => i.Name);

        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You don't see that here.", ct);
            return;
        }

        if (!ctx.CurrentRoom.Remove(item))
        {
            await ctx.Session.WriteLineAsync("You can't take that.", ct);
            return;
        }

        ctx.Actor.Add(item);

        await ctx.Session.WriteLineAsync($"You get {item.Name}.", ct);
        await RoomBroadcast.ToOccupantsAsync(ctx.CurrentRoom, ctx.Actor, $"{ctx.Actor.Name} picks up {item.Name}.", ct);
    }
}
