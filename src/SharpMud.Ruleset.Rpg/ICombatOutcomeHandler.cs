using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

/// <summary>
/// A ruleset's hook into combat outcomes - <see cref="CombatManager"/> owns
/// generic encounter bookkeeping (round resolution, freezing on linkdead,
/// resetting <see cref="CombatantBehavior.CurrentHitPoints"/>) but has no
/// concept of a ruleset's own stats/leveling behavior or where a defeated
/// character should respawn. Implemented once per ruleset (e.g. Classic
/// touches its <c>StatsBehavior</c>'s XP; a ruleset with no leveling concept
/// at all can no-op the reward side) and registered via
/// <c>AddSharpMudRpgRuleset&lt;TCombatOutcomeHandler&gt;(...)</c>. See
/// docs/adr/0008-ruleset-scaffolding-tier.md's Decision Outcome for why this
/// mechanism replaces CombatManager's prior direct <c>StatsBehavior</c>
/// touches and hard-coded respawn room.
/// </summary>
public interface ICombatOutcomeHandler
{
    /// <summary>Called when <paramref name="victor"/> defeats <paramref name="defeated"/> - the hook for awarding XP/rewards.</summary>
    Task OnVictoryAsync(Thing victor, Thing defeated, CancellationToken ct);

    /// <summary>
    /// Called when <paramref name="defeated"/> loses the encounter to
    /// <paramref name="victor"/> - the hook for a death penalty (XP loss,
    /// stats-specific HP reset) and for deciding the respawn destination.
    /// <see cref="CombatantBehavior.CurrentHitPoints"/> is already reset by
    /// <see cref="CombatManager"/> before this is called, regardless of what
    /// this method does.
    /// </summary>
    Task<Thing> OnDefeatAsync(Thing defeated, Thing victor, CancellationToken ct);
}
