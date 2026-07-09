namespace SharpMud.Engine.Core;

// Adapted from WheelMUD (docs/research/wheelmud-findings.md §2). A Thing IS
// what its attached Behaviors say it is - a "player" is a Thing with a
// PlayerBehavior, a "room" is a Thing with a RoomBehavior, etc.
public abstract class Behavior
{
    // Surrogate key for the persistence layer only (EF Core TPH needs a
    // mapped primary key; Behavior has no natural one). Not used by any
    // game logic - see docs/persistence.md Open Items.
    public Guid PersistenceKey { get; private set; } = Guid.NewGuid();

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
