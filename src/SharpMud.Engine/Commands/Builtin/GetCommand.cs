using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class GetCommand : ICommand
{
    public string Verb => "get";
    public IReadOnlyList<string> Aliases { get; } = ["take"];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count == 0)
        {
            await ctx.Session.WriteLineAsync("Get what?", ct);
            return;
        }

        var target = string.Join(' ', ctx.Args);
        var item = ObjectMatcher.FindMatch(
            ctx.CurrentRoom.ItemsOnGround.Select(ctx.World.GetItem).OfType<Item>(),
            target,
            i => i.Name);

        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You don't see that here.", ct);
            return;
        }

        ctx.CurrentRoom.ItemsOnGround.Remove(item.Id);
        ctx.Actor.Inventory.Add(item.Id);

        await ctx.Session.WriteLineAsync($"You get {item.Name}.", ct);

        foreach (var other in ctx.World.PlayersInRoom(ctx.CurrentRoom.Id).Where(p => p.Id != ctx.Actor.Id))
        {
            var session = ctx.World.GetSession(other.Id);
            if (session is not null)
                await session.WriteLineAsync($"{ctx.Actor.Name} picks up {item.Name}.", ct);
        }
    }
}
