using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;

namespace SharpMud.Ruleset.Classic;

public sealed class AttackCommand(ICombatManager combatManager) : ICommand
{
    public string Verb => "kill";
    public IReadOnlyList<string> Aliases { get; } = ["attack"];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Kill what?", ct))
            return;

        if (combatManager.IsInCombat(ctx.Actor.Id))
        {
            await ctx.Session.WriteLineAsync("You are already fighting!", ct);
            return;
        }

        var targetName = string.Join(' ', ctx.Args);
        var target = ObjectMatcher.FindMatch(
            ctx.CurrentRoom.Children.Where(c => c.HasBehavior<NpcBehavior>() && c.HasBehavior<CombatantBehavior>()),
            targetName,
            n => n.Name);

        if (target is null)
        {
            await ctx.Session.WriteLineAsync("You don't see that here.", ct);
            return;
        }

        combatManager.StartEncounter(ctx.Actor, target);
        await ctx.Session.WriteLineAsync($"You attack {target.Name}!", ct);
    }
}
