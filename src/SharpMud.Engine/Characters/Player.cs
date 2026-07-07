using SharpMud.Engine.Combat;

namespace SharpMud.Engine.Characters;

public sealed class Player : ICombatant
{
    public required PlayerId Id { get; init; }
    public AccountId AccountId { get; init; }
    public required string Name { get; set; }

    public Race Race { get; set; }
    public CharacterClass Class { get; set; }

    // Base D&D-style attributes; Race/Class modifier tables are not yet
    // defined (see docs/character.md Open Items) so these are applied
    // unmodified for now.
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }

    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int MaxMana { get; set; }
    public int CurrentMana { get; set; }
    public int MaxStamina { get; set; }
    public int CurrentStamina { get; set; }

    // Combat stats (docs/combat.md ICombatant) - formulas deriving these
    // from attributes/level/class are still an open item (docs/character.md);
    // for now they're flat values set at creation.
    public int ArmorClass { get; set; }
    public int DamageMin { get; set; }
    public int DamageMax { get; set; }

    (int Min, int Max) ICombatant.DamageRange => (DamageMin, DamageMax);

    public int Level { get; set; } = 1;
    public long Experience { get; set; }

    public required RoomId CurrentRoomId { get; set; }
    public List<InventoryItem> Inventory { get; } = [];
    public Dictionary<EquipSlot, InventoryItem?> Equipped { get; } = [];
    public List<string> Aliases { get; } = [];

    // Dice-roll character creation (docs/character.md) isn't implemented yet -
    // this gives the v1 local CLI a playable character to start the
    // foundation build-order phase (movement/look/chat) without it.
    public static Player CreateDefault(string name, RoomId startingRoomId) => new()
    {
        Id = PlayerId.New(),
        Name = name,
        Race = Race.Human,
        Class = CharacterClass.Warrior,
        Strength = 10,
        Dexterity = 10,
        Constitution = 10,
        Intelligence = 10,
        Wisdom = 10,
        Charisma = 10,
        MaxHitPoints = 20,
        CurrentHitPoints = 20,
        MaxMana = 10,
        CurrentMana = 10,
        MaxStamina = 10,
        CurrentStamina = 10,
        ArmorClass = 10,
        DamageMin = 1,
        DamageMax = 4,
        CurrentRoomId = startingRoomId,
    };
}
