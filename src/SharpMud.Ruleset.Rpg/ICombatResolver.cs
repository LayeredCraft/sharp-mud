using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

/// <summary>The outcome of one <see cref="ICombatResolver.ResolveRound"/> call.</summary>
/// <param name="Hit">Whether the attack landed.</param>
/// <param name="Damage">Damage applied - always 0 when <paramref name="Hit"/> is <see langword="false"/>.</param>
/// <param name="DefenderDefeated">Whether this round dropped the defender's <see cref="CombatantBehavior.CurrentHitPoints"/> to zero or below.</param>
public sealed record CombatRoundResult(bool Hit, int Damage, bool DefenderDefeated);

/// <summary>Resolves a single round of combat between two <see cref="CombatantBehavior"/>-carrying Things.</summary>
public interface ICombatResolver
{
    /// <summary>
    /// Resolves and applies one round: rolls to hit, and on a hit rolls and
    /// applies damage directly to <paramref name="defender"/>'s <see
    /// cref="CombatantBehavior.CurrentHitPoints"/>. Both Things must already
    /// carry <see cref="CombatantBehavior"/>.
    /// </summary>
    CombatRoundResult ResolveRound(Thing attacker, Thing defender);
}
