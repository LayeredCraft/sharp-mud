namespace SharpMud.Engine.Help;

/// <summary>
/// A help topic - the aggregate root of sharp-mud's help system (ADR-0010).
/// Independently addressable, own repository (<see cref="IHelpRepository"/>),
/// same "aggregate root gets exactly one repository" shape as <c>Thing</c>/
/// <c>IThingRepository</c> - not a <c>Thing</c>/<c>Behavior</c> itself.
/// Authored and edited only via an in-game admin command; there is no
/// file-based content path (see ADR-0010's Decision Outcome for why, and
/// <c>docs/research/wheelmud-findings.md</c> §12 for the WheelMUD precedent
/// this deliberately deviates from).
/// </summary>
public sealed class HelpTopic
{
    private readonly List<string> _aliases = [];
    private readonly List<string> _keywords = [];
    private readonly List<HelpTopicChunk> _chunks = [];

    /// <summary>Stable identity, independent of <see cref="Key"/> (which can be renamed via edit).</summary>
    public required HelpTopicId Id { get; init; }

    /// <summary>The canonical, primary name this topic is looked up by - e.g. <c>help wizard</c>. Editable directly, like <c>Thing.Name</c>/<c>Thing.Description</c> (both plain settable properties, the established shape for admin-command-edited content in this codebase).</summary>
    public required string Key { get; set; }

    /// <summary>Free-text grouping, e.g. <c>"classes"</c>. Empty string means uncategorized.</summary>
    public string Category { get; set; } = "";

    /// <summary>The authored help text shown to a player on lookup - the canonical source of truth (ADR-0010); <see cref="HelpTopicChunk"/>s are a rebuildable derivative of this, never the reverse.</summary>
    public string Body { get; set; } = "";

    /// <summary>When <see cref="Key"/>/<see cref="Category"/>/<see cref="Body"/>/aliases/keywords last changed. Only mutated via <see cref="Touch"/>.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Additional names this topic is looked up by, besides <see cref="Key"/>. Mutate via <see cref="SetAliases"/>.</summary>
    public IReadOnlyList<string> Aliases => _aliases;

    /// <summary>Free-text keywords this topic matches on the keyword-lookup tier (ADR-0010's pipeline, tier two). Mutate via <see cref="SetKeywords"/>.</summary>
    public IReadOnlyList<string> Keywords => _keywords;

    /// <summary>This topic's current embedding chunks - a rebuildable index over <see cref="Body"/>, not authoritative content. Replaced wholesale via <see cref="ReplaceChunks"/>, never edited individually.</summary>
    public IReadOnlyList<HelpTopicChunk> Chunks => _chunks;

    /// <summary>SHA-256 hex digest of <see cref="Body"/> - compared against each <see cref="HelpTopicChunk.SourceContentHash"/> to detect a stale embedding index. Derived, not persisted directly.</summary>
    public string ContentHash => HelpContentHashing.Compute(Body);

    /// <summary>Replaces this topic's alias set wholesale.</summary>
    public void SetAliases(IEnumerable<string> aliases)
    {
        _aliases.Clear();
        _aliases.AddRange(aliases);
    }

    /// <summary>Replaces this topic's keyword set wholesale.</summary>
    public void SetKeywords(IEnumerable<string> keywords)
    {
        _keywords.Clear();
        _keywords.AddRange(keywords);
    }

    /// <summary>Records that this topic's content changed just now - called by the admin edit command after making its changes.</summary>
    public void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;

    /// <summary>Replaces this topic's <see cref="Chunks"/> wholesale - called by <c>helpindex rebuild</c>. Chunks are always regenerated in full from the current <see cref="Body"/>, never patched individually.</summary>
    public void ReplaceChunks(IEnumerable<HelpTopicChunk> chunks)
    {
        _chunks.Clear();
        _chunks.AddRange(chunks);
    }
}
