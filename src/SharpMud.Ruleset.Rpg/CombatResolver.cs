using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

// Diku/Circle-style d20-vs-AC roll (docs/combat.md decision). Level/skill
// to-hit modifiers and the exact damage formula are still open items there -
// this is currently an unmodified d20 roll against the defender's AC.
/// <summary>
/// The default <see cref="ICombatResolver"/> - a Diku/Circle-style
/// unmodified d20-vs-armor-class roll, with damage rolled from the
/// attacker's <see cref="CombatantBehavior.DamageRange"/>. Public (rather
/// than internal) so a consumer can construct it directly in their own
/// tests, or explicitly replace the <see cref="ICombatResolver"/> DI
/// registration with their own combat math.
/// </summary>
public sealed class CombatResolver : ICombatResolver
{
    private readonly IDiceRoller _dice;
    private readonly IRandomSource _random;

    /// <summary>Creates the resolver against a dice roller and a raw randomness source (for the damage roll - see remarks on <see cref="ResolveRound"/>).</summary>
    public CombatResolver(IDiceRoller dice, IRandomSource random)
    {
        _dice = dice;
        _random = random;
    }

    /// <inheritdoc/>
    public CombatRoundResult ResolveRound(Thing attacker, Thing defender)
    {
        var attackerCombatant = attacker.FindBehavior<CombatantBehavior>()!;
        var defenderCombatant = defender.FindBehavior<CombatantBehavior>()!;

        var toHitRoll = _dice.Roll(1, 20);
        if (toHitRoll < defenderCombatant.ArmorClass)
            return new CombatRoundResult(false, 0, false);

        // Damage range isn't dice notation (arbitrary min/max, not 1-based
        // per-die), so this stays a direct IRandomSource roll rather than
        // going through IDiceRoller.
        var (min, max) = attackerCombatant.DamageRange;
        var damage = _random.Next(min, max);
        defenderCombatant.CurrentHitPoints -= damage;

        return new CombatRoundResult(true, damage, defenderCombatant.CurrentHitPoints <= 0);
    }
}
