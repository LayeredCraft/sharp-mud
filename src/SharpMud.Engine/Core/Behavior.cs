namespace SharpMud.Engine.Core;

// Adapted from WheelMUD (docs/research/wheelmud-findings.md §2). A Thing IS
// what its attached Behaviors say it is - a "player" is a Thing with a
// PlayerBehavior, a "room" is a Thing with a RoomBehavior, etc.
public abstract class Behavior
{
    public Thing? Parent { get; private set; }

    internal void SetParent(Thing? newParent)
    {
        if (Parent == newParent)
            return;

        if (Parent is not null)
            OnRemoveBehavior();

        Parent = newParent;

        if (newParent is not null)
            OnAddBehavior();
    }

    // Override to subscribe to Parent.Events here.
    protected virtual void OnAddBehavior()
    {
    }

    // Override to unsubscribe from Parent.Events here.
    protected virtual void OnRemoveBehavior()
    {
    }
}
