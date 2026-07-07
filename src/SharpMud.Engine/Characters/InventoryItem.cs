namespace SharpMud.Engine.Characters;

// Placeholder shape - the item system itself is a later build-order phase
// (see docs/commands.md build order). This exists only so Player can carry
// a typed inventory list without waiting on that phase.
public sealed record InventoryItem(ItemId Id, string Name);
