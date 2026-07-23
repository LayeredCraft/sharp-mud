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
    /// <summary>Hit points at full health.</summary>
    public int MaxHitPoints { get; set; }

    /// <summary>Current hit points - the value <see cref="ICombatResolver"/> actually reads/writes during combat.</summary>
    public int CurrentHitPoints { get; set; }

    /// <summary>Armor class - the to-hit roll must meet or exceed this to land a hit.</summary>
    public int ArmorClass { get; set; }

    /// <summary>Minimum damage rolled on a hit - see <see cref="DamageRange"/>.</summary>
    public int DamageMin { get; set; }

    /// <summary>Maximum damage rolled on a hit - see <see cref="DamageRange"/>.</summary>
    public int DamageMax { get; set; }

    /// <summary>XP awarded to the victor when this combatant is defeated - see <see cref="ICombatOutcomeHandler.OnVictoryAsync"/>.</summary>
    public int ExperienceReward { get; set; }

    /// <summary>The <c>(<see cref="DamageMin"/>, <see cref="DamageMax"/>)</c> pair, as read by <see cref="ICombatResolver"/>.</summary>
    public (int Min, int Max) DamageRange => (DamageMin, DamageMax);
}
