using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class WearCommand : ICommand
{
    public string Verb => "wear";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count == 0)
        {
            await ctx.Session.WriteLineAsync("Wear what?", ct);
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

        if (item.Slot is not { } slot)
        {
            await ctx.Session.WriteLineAsync("You can't wear that.", ct);
            return;
        }

        // Auto-swap: whatever's already in the slot goes back to inventory.
        if (ctx.Actor.Equipped.TryGetValue(slot, out var previousId) && previousId is { } id)
        {
            ctx.Actor.Inventory.Add(id);
            var previous = ctx.World.GetItem(id);
            if (previous is not null)
                await ctx.Session.WriteLineAsync($"You remove {previous.Name}.", ct);
        }

        ctx.Actor.Inventory.Remove(item.Id);
        ctx.Actor.Equipped[slot] = item.Id;

        await ctx.Session.WriteLineAsync($"You wear {item.Name}.", ct);
    }
}
