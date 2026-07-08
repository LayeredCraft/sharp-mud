using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Published (SelfOnly, on the exit Thing) before a move through it completes.
// LockableBehavior cancels this when locked; MoveCommand doesn't need to know
// why a move might be blocked, only whether it was.
public sealed class UseExitEvent : CancellableGameEvent
{
    public required Thing Exit { get; init; }
}
