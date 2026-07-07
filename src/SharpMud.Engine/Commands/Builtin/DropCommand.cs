using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class DropCommand : ICommand
{
    public string Verb => "drop";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count == 0)
        {
            await ctx.Session.WriteLineAsync("Drop what?", ct);
            return;
        }

        var target = string.Join(' ', ctx.Args);
        var item = ObjectMatcher.FindMatch(
            ctx.Actor.Inventory.Select(ctx.World.GetItem).OfType<Item>(),
            target,
            i => i.Name);

        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You aren't carrying that.", ct);
            return;
        }

        ctx.Actor.Inventory.Remove(item.Id);
        ctx.CurrentRoom.ItemsOnGround.Add(item.Id);

        await ctx.Session.WriteLineAsync($"You drop {item.Name}.", ct);

        foreach (var other in ctx.World.PlayersInRoom(ctx.CurrentRoom.Id).Where(p => p.Id != ctx.Actor.Id))
        {
            var session = ctx.World.GetSession(other.Id);
            if (session is not null)
                await session.WriteLineAsync($"{ctx.Actor.Name} drops {item.Name}.", ct);
        }
    }
}
