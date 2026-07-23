using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

// Diku/Circle-style d20-vs-AC roll (docs/combat.md decision). Level/skill
// to-hit modifiers and the exact damage formula are still open items there -
// this is currently an unmodified d20 roll against the defender's AC.
public sealed class CombatResolver : ICombatResolver
{
    private readonly IDiceRoller _dice;
    private readonly IRandomSource _random;

    public CombatResolver(IDiceRoller dice, IRandomSource random)
    {
        _dice = dice;
        _random = random;
    }

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
