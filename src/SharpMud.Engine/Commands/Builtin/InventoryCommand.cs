using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class InventoryCommand : ICommand
{
    public string Verb => "inventory";
    public IReadOnlyList<string> Aliases { get; } = ["i"];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var carried = CarriedItems.Of(ctx.Actor).ToList();

        await ctx.Session.WriteLineAsync("You are carrying:", ct);
        if (carried.Count == 0)
        {
            await ctx.Session.WriteLineAsync("  nothing", ct);
        }
        else
        {
            foreach (var item in carried)
                await ctx.Session.WriteLineAsync($"  {item.Name}", ct);
        }

        var worn = ctx.Actor.FindBehavior<EquippedBehavior>()?.Equipped
            .Where(kvp => kvp.Value is not null)
            .ToList();

        if (worn is not { Count: > 0 })
            return;

        await ctx.Session.WriteLineAsync("You are wearing:", ct);
        foreach (var (slot, item) in worn)
            await ctx.Session.WriteLineAsync($"  {item!.Name} ({slot})", ct);
    }
}
