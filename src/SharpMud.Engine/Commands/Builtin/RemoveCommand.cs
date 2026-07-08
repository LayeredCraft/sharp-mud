using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class RemoveCommand : ICommand
{
    public string Verb => "remove";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Remove what?", ct))
            return;

        var equipped = ctx.Actor.FindBehavior<EquippedBehavior>();
        if (equipped is null)
        {
            await ctx.Session.WriteLineAsync("You aren't wearing anything.", ct);
            return;
        }

        var target = string.Join(' ', ctx.Args);
        var wornItems = equipped.Equipped.Where(kvp => kvp.Value is not null).Select(kvp => kvp.Value!);
        var item = ObjectMatcher.FindMatch(wornItems, target, i => i.Name);

        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You aren't wearing that.", ct);
            return;
        }

        var slot = equipped.Equipped.First(kvp => kvp.Value == item).Key;
        equipped.Equipped[slot] = null;

        await ctx.Session.WriteLineAsync($"You remove {item.Name}.", ct);
    }
}
