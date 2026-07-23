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
    /// encounter - since encounters are keyed by attacker only, this is the
    /// check that stops a second attacker from starting a second, redundant
    /// encounter against a target someone else is already fighting (which
    /// would otherwise let both encounters independently resolve, remove,
    /// and award victory for the same kill).
    /// </summary>
    bool IsDefenderEngaged(ThingId defenderId);

    /// <summary>Starts (or replaces) the encounter keyed by <paramref name="attacker"/>.</summary>
    void StartEncounter(Thing attacker, Thing defender);

    /// <summary>Ends the encounter keyed by the given Thing, if one exists.</summary>
    void EndEncounter(ThingId thingId);

    /// <summary>Attempts to get the active encounter keyed by the given Thing.</summary>
    bool TryGetEncounter(ThingId thingId, [MaybeNullWhen(false)] out CombatEncounter encounter);
}
