using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Presence means "this item can be worn." Absence (an ItemBehavior Thing
// with no WearableBehavior) means it can be carried but not worn.
public sealed class WearableBehavior : Behavior
{
    public required EquipSlot Slot { get; init; }
}
