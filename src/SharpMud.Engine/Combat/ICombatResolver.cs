namespace SharpMud.Engine.Combat;

public sealed record CombatRoundResult(bool Hit, int Damage, bool DefenderDefeated);

public interface ICombatResolver
{
    CombatRoundResult ResolveRound(ICombatant attacker, ICombatant defender);
}
