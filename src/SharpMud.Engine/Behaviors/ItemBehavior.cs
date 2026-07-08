using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Marker only - presence means "this Thing can be picked up/dropped/given"
// (get/drop/give commands look for this). Distinguishes an item child of a
// room from an exit or NPC child.
public sealed class ItemBehavior : Behavior;
