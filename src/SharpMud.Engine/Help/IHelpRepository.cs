namespace SharpMud.Engine.Help;

/// <summary>
/// The aggregate-root repository for <see cref="HelpTopic"/> (ADR-0010) -
/// one repository per independently-addressable aggregate root, matching
/// <c>IThingRepository</c>'s shape (see <c>docs/persistence.md</c>: no
/// generic/per-concept repositories).
/// </summary>
public interface IHelpRepository
{
    /// <summary>Finds a topic by exact, case-insensitive <see cref="HelpTopic.Key"/> or <see cref="HelpTopic.Aliases"/> match - tier one of ADR-0010's lookup pipeline.</summary>
    Task<HelpTopic?> FindByNameOrAliasAsync(string name, CancellationToken ct);

    /// <summary>Finds every topic whose <see cref="HelpTopic.Keywords"/> contains an exact, case-insensitive match - tier two of ADR-0010's lookup pipeline.</summary>
    Task<IReadOnlyList<HelpTopic>> FindByKeywordAsync(string keyword, CancellationToken ct);

    /// <summary>Every topic, each with its current <see cref="HelpTopic.Chunks"/> loaded - used by <see cref="IHelpSearchIndex"/> (tier three) and by <c>helpindex rebuild</c>.</summary>
    Task<IReadOnlyList<HelpTopic>> GetAllTopicsAsync(CancellationToken ct);

    /// <summary>Creates or overwrites <paramref name="topic"/>, including its current <see cref="HelpTopic.Chunks"/>, as one unit.</summary>
    Task SaveTopicAsync(HelpTopic topic, CancellationToken ct);

    /// <summary>Deletes a topic and its chunks. No-op if <paramref name="id"/> doesn't exist.</summary>
    Task DeleteTopicAsync(HelpTopicId id, CancellationToken ct);
}
