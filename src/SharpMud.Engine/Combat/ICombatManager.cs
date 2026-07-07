using System.Diagnostics.CodeAnalysis;
using SharpMud.Engine.Characters;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Combat;

public sealed class CombatEncounter
{
    public required Player Attacker { get; init; }
    public required Npc Defender { get; init; }
}

// v1 scope is player-vs-NPC only - no PvP verb/aggression rules exist yet,
// so an encounter is always keyed by the attacking player.
public interface ICombatManager
{
    bool IsInCombat(PlayerId playerId);
    void StartEncounter(Player attacker, Npc defender);
    void EndEncounter(PlayerId playerId);
    bool TryGetEncounter(PlayerId playerId, [MaybeNullWhen(false)] out CombatEncounter encounter);
}
