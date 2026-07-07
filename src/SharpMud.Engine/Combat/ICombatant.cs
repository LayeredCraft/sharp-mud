namespace SharpMud.Engine.Combat;

// Extends docs/combat.md's sketch (CurrentHitPoints/ArmorClass/DamageRange
// only) with Name and MaxHitPoints - both needed for round messages and
// death/respawn handling, which the doc implied but didn't spell out as
// interface members.
public interface ICombatant
{
    string Name { get; }
    int CurrentHitPoints { get; set; }
    int MaxHitPoints { get; }
    int ArmorClass { get; }
    (int Min, int Max) DamageRange { get; }
}
