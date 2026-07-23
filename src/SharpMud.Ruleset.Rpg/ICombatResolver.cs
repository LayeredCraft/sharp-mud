using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

public sealed record CombatRoundResult(bool Hit, int Damage, bool DefenderDefeated);

/// <summary>Resolves a single round of combat between two <see cref="CombatantBehavior"/>-carrying Things.</summary>
public interface ICombatResolver
{
    CombatRoundResult ResolveRound(Thing attacker, Thing defender);
}
