namespace SharpMud.Engine.Core;

// Every game object - room, player, item, NPC, exit, area - is this same
// sealed class. What an object IS comes entirely from which Behaviors are
// attached, never from subclassing. See docs/engine-vs-ruleset.md.
public sealed class Thing
{
    private readonly List<Thing> _children = [];
    private readonly List<Thing> _secondaryParents = [];

    public required ThingId Id { get; init; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public Thing? Parent { get; private set; }
    public IReadOnlyList<Thing> Children => _children;

    // Parent + any secondary parents (see MultipleParentsBehavior).
    public IReadOnlyList<Thing> Parents => Parent switch
    {
        null => _secondaryParents,
        not null when _secondaryParents.Count == 0 => [Parent],
        not null => [Parent, .. _secondaryParents],
    };

    public BehaviorManager Behaviors { get; }
    public ThingEvents Events { get; }

    public Thing(params Behavior[] behaviors)
    {
        Events = new ThingEvents(this);
        Behaviors = new BehaviorManager(this);
        foreach (var behavior in behaviors)
            Behaviors.Add(behavior);
    }

    public T? FindBehavior<T>() where T : Behavior => Behaviors.FindFirst<T>();

    public bool HasBehavior<T>() where T : Behavior => FindBehavior<T>() is not null;

    // Publishes a cancellable AddChildEvent before mutating - any Behavior
    // subscribed on this Thing can veto (e.g. a full container).
    public bool Add(Thing thing)
    {
        var request = new AddChildEvent { ActiveThing = thing, Container = this };
        Events.PublishRequest(request, EventScope.SelfOnly);
        if (request.IsCanceled)
            return false;

        thing.Parent?._children.Remove(thing);
        thing.Parent = this;
        _children.Add(thing);
        return true;
    }

    public bool Remove(Thing thing)
    {
        if (!_children.Contains(thing))
            return false;

        var request = new RemoveChildEvent { ActiveThing = thing, Container = this };
        Events.PublishRequest(request, EventScope.SelfOnly);
        if (request.IsCanceled)
            return false;

        _children.Remove(thing);
        thing.Parent = null;
        return true;
    }

    internal void AddSecondaryParent(Thing parent)
    {
        if (Parent is null)
        {
            Parent = parent;
            return;
        }

        if (Parent == parent || _secondaryParents.Contains(parent))
            return;

        _secondaryParents.Add(parent);
    }

    internal void RemoveSecondaryParent(Thing parent) => _secondaryParents.Remove(parent);
}
