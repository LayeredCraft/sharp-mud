using System.Diagnostics.CodeAnalysis;
using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

/// <summary>An active combat encounter - the attacking <see cref="Thing"/> and the <see cref="Thing"/> it's fighting.</summary>
public sealed class CombatEncounter
{
    /// <summary>The Thing that initiated the encounter (always the player, per the v1 scope note below).</summary>
    public required Thing Attacker { get; init; }

    /// <summary>The Thing being attacked.</summary>
    public required Thing Defender { get; init; }
}

// v1 scope is player-vs-NPC only - no PvP verb/aggression rules exist yet,
// so an encounter is always keyed by the attacking Thing.
/// <summary>
/// Tracks and resolves active combat encounters. Implemented by <see
/// cref="CombatManager"/>; a consumer typically only interacts with this
/// interface (e.g. a custom command checking <see cref="IsInCombat"/>),
/// resolved from DI rather than constructed directly.
/// </summary>
public interface ICombatManager
{
    /// <summary>Whether the given Thing is currently the attacker in an active encounter.</summary>
    bool IsInCombat(ThingId thingId);

    /// <summary>
    /// Whether the given Thing is currently the defender in any active
    /// encounter - since encounters are keyed by attacker only, a second
    /// attacker targeting an already-engaged defender would otherwise let
    /// both encounters independently resolve, remove, and award victory for
    /// the same kill. This is a point-in-time status query only - the actual
    /// enforcement against that race is <see cref="TryStartEncounter"/>,
    /// which checks and inserts atomically; don't call this separately and
    /// then call <see cref="TryStartEncounter"/> expecting the combination
    /// to be race-free, since another attacker can start an encounter
    /// between the two calls.
    /// </summary>
    bool IsDefenderEngaged(ThingId defenderId);

    /// <summary>
    /// Atomically starts the encounter keyed by <paramref name="attacker"/>,
    /// unless <paramref name="attacker"/> is already fighting or <paramref
    /// name="defender"/> is already engaged by a different attacker - the
    /// check and the insert happen under the same lock, so two concurrent
    /// callers (e.g. two players' independent session-loop tasks targeting
    /// the same NPC at nearly the same time) can't both succeed against the
    /// same defender. Returns whether the encounter was actually started.
    /// </summary>
    bool TryStartEncounter(Thing attacker, Thing defender);

    /// <summary>Ends the encounter keyed by the given Thing, if one exists.</summary>
    void EndEncounter(ThingId thingId);

    /// <summary>Attempts to get the active encounter keyed by the given Thing.</summary>
    bool TryGetEncounter(ThingId thingId, [MaybeNullWhen(false)] out CombatEncounter encounter);
}
