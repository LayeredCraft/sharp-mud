using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// One exit Thing per direction of travel (simpler than WheelMUD's single
// bidirectional exit + MultipleParentsBehavior - see docs/engine-vs-ruleset.md
// Decisions). The exit Thing is typically a child of the room it exits from.
public sealed class ExitBehavior : Behavior
{
    public required Direction Direction { get; init; }

    // Mutable (not required init) so SharpMud.Persistence can resolve this
    // from a shadow FK column after both Things in the pair are loaded -
    // see docs/persistence.md Rehydration. Always set at construction time
    // by ordinary game logic (HubWorldBuilder etc).
    public Thing Destination { get; set; } = null!;
}
