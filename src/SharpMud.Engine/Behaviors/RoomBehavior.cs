using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Marks a Thing as a room. Minimal by design - exits are discovered via
// FindAll<ExitBehavior>() over Children, not a dedicated list (see
// docs/engine-vs-ruleset.md).
public sealed class RoomBehavior : Behavior;
