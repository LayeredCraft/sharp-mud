using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Attached to whichever Things can wear items. Worn items are still Children
// of the wearer (same as carried items) - this only tracks which slot each
// worn item occupies. Carried-but-not-worn items are Children not present in
// Equipped's values - no separate Inventory list needed (docs/engine-vs-ruleset.md).
public sealed class EquippedBehavior : Behavior
{
    public Dictionary<EquipSlot, Thing?> Equipped { get; } = [];
}
