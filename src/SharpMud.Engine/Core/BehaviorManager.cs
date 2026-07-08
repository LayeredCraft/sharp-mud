namespace SharpMud.Engine.Core;

public sealed class BehaviorManager(Thing owner)
{
    private readonly List<Behavior> _behaviors = [];

    public IReadOnlyList<Behavior> All => _behaviors;

    public void Add(Behavior behavior)
    {
        _behaviors.Add(behavior);
        behavior.SetParent(owner);
    }

    public void Remove(Behavior behavior)
    {
        behavior.SetParent(null);
        _behaviors.Remove(behavior);
    }

    public T? FindFirst<T>() where T : Behavior => _behaviors.OfType<T>().FirstOrDefault();

    public IEnumerable<T> FindAll<T>() where T : Behavior => _behaviors.OfType<T>();
}
