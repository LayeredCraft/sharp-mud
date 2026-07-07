namespace SharpMud.Engine.Commands.Builtin;

public sealed class InventoryCommand : ICommand
{
    public string Verb => "inventory";
    public IReadOnlyList<string> Aliases { get; } = ["i"];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        await ctx.Session.WriteLineAsync("You are carrying:", ct);
        if (ctx.Actor.Inventory.Count == 0)
        {
            await ctx.Session.WriteLineAsync("  nothing", ct);
        }
        else
        {
            foreach (var item in ctx.Actor.Inventory.Select(ctx.World.GetItem))
                if (item is not null)
                    await ctx.Session.WriteLineAsync($"  {item.Name}", ct);
        }

        var worn = ctx.Actor.Equipped.Where(kvp => kvp.Value is not null).ToList();
        if (worn.Count == 0)
            return;

        await ctx.Session.WriteLineAsync("You are wearing:", ct);
        foreach (var (slot, itemId) in worn)
        {
            var item = ctx.World.GetItem(itemId!.Value);
            if (item is not null)
                await ctx.Session.WriteLineAsync($"  {item.Name} ({slot})", ct);
        }
    }
}
