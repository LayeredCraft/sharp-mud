using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Classic;

// What used to be the ICombatant interface Player/Npc implemented - now a
// behavior any Thing can carry (a hostile plant, a turret - anything the
// ruleset wants to be able to fight), independent of StatsBehavior so an NPC
// doesn't need a full character sheet just to throw a punch.
public sealed class CombatantBehavior : Behavior
{
    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int DamageMin { get; set; }
    public int DamageMax { get; set; }
    public int ExperienceReward { get; set; }

    public (int Min, int Max) DamageRange => (DamageMin, DamageMax);
}
