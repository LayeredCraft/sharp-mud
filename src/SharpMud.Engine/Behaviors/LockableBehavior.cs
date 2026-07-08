using SharpMud.Engine.Core;

namespace SharpMud.Engine.Behaviors;

// Attached to an exit Thing to make it lockable. Subscribes to move requests
// on its parent and cancels them when locked - MoveCommand never checks
// IsLocked directly (docs/engine-vs-ruleset.md sequence walkthrough).
public sealed class LockableBehavior : Behavior
{
    public bool IsLocked { get; set; }
    public bool IsClosed { get; set; }
    public Thing? RequiredKey { get; set; }

    protected override void OnAddBehavior() => Parent!.Events.SubscribeRequest(OnRequest);

    protected override void OnRemoveBehavior() => Parent!.Events.UnsubscribeRequest(OnRequest);

    private void OnRequest(Thing target, CancellableGameEvent evt)
    {
        if (evt is UseExitEvent && IsLocked)
            evt.Cancel("The door is locked.");
    }
}
