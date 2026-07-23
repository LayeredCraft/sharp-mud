using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

/// <summary>
/// HP/armor-class/damage-range/XP-reward - a <see cref="Behavior"/> any
/// <see cref="Thing"/> can carry (a hostile plant, a turret - anything the
/// ruleset wants to be able to fight), independent of any stats behavior so
/// an NPC doesn't need a full character sheet just to throw a punch.
/// </summary>
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
