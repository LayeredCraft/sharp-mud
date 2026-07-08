using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Marks a Thing as an area; rooms are its Children (containment, not a
// separate AreaId foreign key).
public sealed class AreaBehavior : Behavior;
