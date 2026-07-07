using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class RemoveCommand : ICommand
{
    public string Verb => "remove";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count == 0)
        {
            await ctx.Session.WriteLineAsync("Remove what?", ct);
            return;
        }

        var target = string.Join(' ', ctx.Args);
        var equippedItems = ctx.Actor.Equipped.Values
            .Where(id => id is not null)
            .Select(id => ctx.World.GetItem(id!.Value))
            .OfType<Item>();

        var item = ObjectMatcher.FindMatch(equippedItems, target, i => i.Name);
        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You aren't wearing that.", ct);
            return;
        }

        var slot = ctx.Actor.Equipped.First(kvp => kvp.Value == item.Id).Key;
        ctx.Actor.Equipped[slot] = null;
        ctx.Actor.Inventory.Add(item.Id);

        await ctx.Session.WriteLineAsync($"You remove {item.Name}.", ct);
    }
}
