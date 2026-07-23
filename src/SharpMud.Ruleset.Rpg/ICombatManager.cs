using System.Diagnostics.CodeAnalysis;
using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

public sealed class CombatEncounter
{
    public required Thing Attacker { get; init; }
    public required Thing Defender { get; init; }
}

// v1 scope is player-vs-NPC only - no PvP verb/aggression rules exist yet,
// so an encounter is always keyed by the attacking Thing.
public interface ICombatManager
{
    bool IsInCombat(ThingId thingId);
    void StartEncounter(Thing attacker, Thing defender);
    void EndEncounter(ThingId thingId);
    bool TryGetEncounter(ThingId thingId, [MaybeNullWhen(false)] out CombatEncounter encounter);
}
