namespace SharpMud.Engine.Help;

/// <summary>
/// Splits a <see cref="HelpTopic.Body"/> into per-paragraph chunks (blank
/// lines as boundaries) - used by <c>helpindex rebuild</c> to decide what
/// gets embedded. A static function, not a service, since it's pure text
/// splitting with no dependency.
/// </summary>
public static class HelpTopicChunker
{
    /// <summary>Splits <paramref name="body"/> on blank lines into non-empty, trimmed paragraphs. Empty/whitespace-only input yields no chunks.</summary>
    public static IReadOnlyList<string> Split(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];

        return body
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();
    }
}
