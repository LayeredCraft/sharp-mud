using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;

namespace SharpMud.Ruleset.Rpg;

/// <summary>
/// The <c>kill</c>/<c>attack</c> command - starts a combat encounter against
/// an NPC in the current room. Registered by
/// <c>AddSharpMudRpgRuleset(...)</c>, not meant to be constructed directly
/// by a consumer.
/// </summary>
public sealed class AttackCommand : ICommand
{
    private readonly ICombatManager _combatManager;

    /// <summary>Creates the command against the shared <see cref="ICombatManager"/>.</summary>
    public AttackCommand(ICombatManager combatManager)
    {
        _combatManager = combatManager;
    }

    /// <summary>The canonical verb, <c>kill</c>.</summary>
    public string Verb => "kill";

    /// <summary>Aliases for <see cref="Verb"/> - just <c>attack</c>.</summary>
    public IReadOnlyList<string> Aliases { get; } = ["attack"];

    /// <summary>
    /// Guards (not already fighting, actor can fight, a matching NPC target
    /// exists in the room and isn't already engaged by someone else), then
    /// starts the encounter via <see cref="ICombatManager.StartEncounter"/>.
    /// Resolution happens on the next game tick, not synchronously here.
    /// </summary>
    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Kill what?", ct))
            return;

        if (_combatManager.IsInCombat(ctx.Actor.Id))
        {
            await ctx.Session.WriteLineAsync("You are already fighting!", ct);
            return;
        }

        // Every built-in IPlayerFactory attaches CombatantBehavior to a
        // fresh player, but nothing enforces that for a consumer's own
        // IPlayerFactory - without this guard, a player missing it would
        // start an encounter here successfully and only fail later, at tick
        // time, on CombatResolver's attacker.FindBehavior<CombatantBehavior>()!.
        if (!ctx.Actor.HasBehavior<CombatantBehavior>())
        {
            await ctx.Session.WriteLineAsync("You have no way to fight.", ct);
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

        // Without this, a second attacker could start a second, independent
        // encounter against a target someone else is already fighting -
        // both encounters would then resolve/remove/award victory for the
        // same kill (see ICombatManager.IsDefenderEngaged).
        if (_combatManager.IsDefenderEngaged(target.Id))
        {
            await ctx.Session.WriteLineAsync($"Someone else is already fighting {target.Name}!", ct);
            return;
        }

        _combatManager.StartEncounter(ctx.Actor, target);
        await ctx.Session.WriteLineAsync($"You attack {target.Name}!", ct);
    }
}
