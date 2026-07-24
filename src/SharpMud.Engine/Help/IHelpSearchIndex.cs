namespace SharpMud.Engine.Help;

/// <summary>
/// Semantic search over <see cref="HelpTopic"/> content (ADR-0010) - tier
/// three of the help lookup pipeline, only ever consulted after exact-name/
/// alias and keyword lookup both miss. Implementations apply their own
/// relevance threshold and must return an empty list rather than a weak
/// guess when nothing clears it - "no help topic found" beats an unrelated
/// answer. The default implementation (<see cref="CosineHelpSearchIndex"/>)
/// is storage-agnostic (reads through <see cref="IHelpRepository"/> only),
/// so a future vector-storage swap (e.g. an ANN-backed index) replaces this
/// interface's implementation without touching any command.
/// </summary>
public interface IHelpSearchIndex
{
    /// <summary>Returns the best-matching topics for <paramref name="query"/>, ordered by descending relevance, above whatever threshold this implementation enforces. Empty, not an exception or a weak guess, when nothing qualifies.</summary>
    Task<IReadOnlyList<HelpSearchHit>> SearchAsync(string query, CancellationToken ct);
}
