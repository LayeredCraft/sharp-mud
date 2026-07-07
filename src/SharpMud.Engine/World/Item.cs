using SharpMud.Engine.Characters;

namespace SharpMud.Engine.World;

public sealed class Item
{
    public required ItemId Id { get; init; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";

    // null = not wearable (e.g. a coin, a key). Non-null = the slot it
    // occupies when worn (docs/character.md Equipped dictionary).
    public EquipSlot? Slot { get; init; }
}
