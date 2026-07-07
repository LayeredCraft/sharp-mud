namespace SharpMud.Engine.Combat;

// Diku/Circle-style d20-vs-AC roll (docs/combat.md decision). Level/skill
// to-hit modifiers and the exact damage formula are still open items there -
// this is currently an unmodified d20 roll against the defender's AC.
public sealed class CombatResolver(IRandomSource random) : ICombatResolver
{
    public CombatRoundResult ResolveRound(ICombatant attacker, ICombatant defender)
    {
        var toHitRoll = random.Next(1, 20);
        if (toHitRoll < defender.ArmorClass)
            return new CombatRoundResult(false, 0, false);

        var (min, max) = attacker.DamageRange;
        var damage = random.Next(min, max);
        defender.CurrentHitPoints -= damage;

        return new CombatRoundResult(true, damage, defender.CurrentHitPoints <= 0);
    }
}
