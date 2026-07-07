namespace SharpMud.Engine.Commands;

// Classic MUD ordinal syntax (docs/commands.md): "get sword" matches the
// first/nearest match; "get 2.sword" selects the second. Shared across every
// object-targeting verb (get, drop, kill, wear, give, etc).
public static class ObjectMatcher
{
    public static (int Ordinal, string Name) ParseTarget(string rawArg)
    {
        var dotIndex = rawArg.IndexOf('.');
        if (dotIndex > 0 && int.TryParse(rawArg[..dotIndex], out var ordinal) && ordinal > 0)
            return (ordinal, rawArg[(dotIndex + 1)..]);

        return (1, rawArg);
    }

    public static T? FindMatch<T>(IEnumerable<T> candidates, string rawArg, Func<T, string> nameSelector)
        where T : class
    {
        var (ordinal, name) = ParseTarget(rawArg);

        return candidates
            .Where(c => nameSelector(c).Contains(name, StringComparison.OrdinalIgnoreCase))
            .Skip(ordinal - 1)
            .FirstOrDefault();
    }
}
