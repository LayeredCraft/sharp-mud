namespace SharpMud.Engine.Core;

public enum EventScope
{
    SelfOnly,
    SelfDown,
    ParentsDown,
}

public abstract class GameEvent
{
    public required Thing ActiveThing { get; init; }
}

public abstract class CancellableGameEvent : GameEvent
{
    public bool IsCanceled { get; private set; }
    public string? CancelReason { get; private set; }

    public void Cancel(string reason)
    {
        IsCanceled = true;
        CancelReason = reason;
    }
}

// Published by Thing.Add/Remove before mutating the tree - any Behavior
// anywhere in the traversed scope can veto (see docs/engine-vs-ruleset.md).
public sealed class AddChildEvent : CancellableGameEvent
{
    public required Thing Container { get; init; }
}

public sealed class RemoveChildEvent : CancellableGameEvent
{
    public required Thing Container { get; init; }
}

public sealed class ThingEvents(Thing owner)
{
    private readonly List<Action<Thing, CancellableGameEvent>> _requestHandlers = [];
    private readonly List<Action<Thing, GameEvent>> _eventHandlers = [];

    public void SubscribeRequest(Action<Thing, CancellableGameEvent> handler) => _requestHandlers.Add(handler);

    public void UnsubscribeRequest(Action<Thing, CancellableGameEvent> handler) => _requestHandlers.Remove(handler);

    public void SubscribeEvent(Action<Thing, GameEvent> handler) => _eventHandlers.Add(handler);

    public void UnsubscribeEvent(Action<Thing, GameEvent> handler) => _eventHandlers.Remove(handler);

    public void PublishRequest(CancellableGameEvent evt, EventScope scope)
    {
        foreach (var target in TargetsFor(scope))
        {
            foreach (var handler in target.Events._requestHandlers.ToArray())
            {
                handler(target, evt);
                if (evt.IsCanceled)
                    return;
            }
        }
    }

    public void PublishEvent(GameEvent evt, EventScope scope)
    {
        foreach (var target in TargetsFor(scope))
            foreach (var handler in target.Events._eventHandlers.ToArray())
                handler(target, evt);
    }

    private IEnumerable<Thing> TargetsFor(EventScope scope) => scope switch
    {
        EventScope.SelfOnly => [owner],
        EventScope.SelfDown => SelfAndDescendants(owner),
        EventScope.ParentsDown => owner.Parents.SelectMany(SelfAndDescendants),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
    };

    // Breadth-first via a Queue<Thing> (not recursion) to avoid stack
    // overflows on deep containment trees - matches WheelMUD's traversal.
    private static IEnumerable<Thing> SelfAndDescendants(Thing root)
    {
        var queue = new Queue<Thing>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;
            foreach (var child in current.Children)
                queue.Enqueue(child);
        }
    }
}
