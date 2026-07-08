using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class WearCommand : ICommand
{
    public string Verb => "wear";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Wear what?", ct))
            return;

        var target = string.Join(' ', ctx.Args);
        var item = ObjectMatcher.FindMatch(CarriedItems.Of(ctx.Actor), target, i => i.Name);

        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You aren't carrying that.", ct);
            return;
        }

        var wearable = item.FindBehavior<WearableBehavior>();
        if (wearable is null)
        {
            await ctx.Session.WriteLineAsync("You can't wear that.", ct);
            return;
        }

        var equipped = ctx.Actor.FindBehavior<EquippedBehavior>();
        if (equipped is null)
        {
            await ctx.Session.WriteLineAsync("You can't wear anything.", ct);
            return;
        }

        // Auto-swap: whatever's already in the slot comes off first.
        if (equipped.Equipped.TryGetValue(wearable.Slot, out var previous) && previous is not null)
        {
            await ctx.Session.WriteLineAsync($"You remove {previous.Name}.", ct);
        }

        equipped.Equipped[wearable.Slot] = item;

        await ctx.Session.WriteLineAsync($"You wear {item.Name}.", ct);
    }
}
