namespace SharpMud.Engine.Commands.Builtin;

public sealed class DropCommand : ICommand
{
    public string Verb => "drop";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Drop what?", ct))
            return;

        var target = string.Join(' ', ctx.Args);
        var item = ObjectMatcher.FindMatch(CarriedItems.Of(ctx.Actor), target, i => i.Name);

        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You aren't carrying that.", ct);
            return;
        }

        if (!ctx.Actor.Remove(item))
        {
            await ctx.Session.WriteLineAsync("You can't drop that.", ct);
            return;
        }

        ctx.CurrentRoom.Add(item);

        await ctx.Session.WriteLineAsync($"You drop {item.Name}.", ct);
        await RoomBroadcast.ToOccupantsAsync(ctx.CurrentRoom, ctx.Actor, $"{ctx.Actor.Name} drops {item.Name}.", ct);
    }
}
