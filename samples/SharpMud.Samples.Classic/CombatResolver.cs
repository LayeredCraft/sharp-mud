using SharpMud.Engine.Core;

namespace SharpMud.Samples.Classic;

// Diku/Circle-style d20-vs-AC roll (docs/combat.md decision). Level/skill
// to-hit modifiers and the exact damage formula are still open items there -
// this is currently an unmodified d20 roll against the defender's AC.
public sealed class CombatResolver(IRandomSource random) : ICombatResolver
{
    public CombatRoundResult ResolveRound(Thing attacker, Thing defender)
    {
        var attackerCombatant = attacker.FindBehavior<CombatantBehavior>()!;
        var defenderCombatant = defender.FindBehavior<CombatantBehavior>()!;

        var toHitRoll = random.Next(1, 20);
        if (toHitRoll < defenderCombatant.ArmorClass)
            return new CombatRoundResult(false, 0, false);

        var (min, max) = attackerCombatant.DamageRange;
        var damage = random.Next(min, max);
        defenderCombatant.CurrentHitPoints -= damage;

        return new CombatRoundResult(true, damage, defenderCombatant.CurrentHitPoints <= 0);
    }
}
