using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Classic;

public sealed record CombatRoundResult(bool Hit, int Damage, bool DefenderDefeated);

public interface ICombatResolver
{
    CombatRoundResult ResolveRound(Thing attacker, Thing defender);
}
