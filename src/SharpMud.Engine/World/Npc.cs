using SharpMud.Engine.Combat;

namespace SharpMud.Engine.World;

public sealed class Npc : ICombatant
{
    public required NpcId Id { get; init; }
    public required string Name { get; set; }
    public required RoomId RoomId { get; set; }

    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int DamageMin { get; set; }
    public int DamageMax { get; set; }
    public int ExperienceReward { get; set; }

    (int Min, int Max) ICombatant.DamageRange => (DamageMin, DamageMax);
}
