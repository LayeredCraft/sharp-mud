namespace SharpMud.Engine.Help;

/// <summary>
/// Turns text into an embedding vector - the abstraction ADR-0010 puts
/// between the help-search pipeline and whatever produces embeddings, so a
/// real model can be swapped in later behind this interface without
/// touching <see cref="IHelpSearchIndex"/> or any command. The default
/// implementation (<see cref="StubEmbeddingProvider"/>) is a deterministic
/// placeholder, not a production-quality semantic model.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Identifies which model/version produced an embedding - stored on <see cref="HelpTopicChunk.EmbeddingModelId"/> so a future model change can be detected, not just a content change.</summary>
    string ModelId { get; }

    /// <summary>
    /// Below this cosine score, <see cref="IHelpSearchIndex"/> treats a
    /// match as too weak to show - "no help topic found" beats an
    /// unrelated guess (ADR-0010). Owned by the provider, not the search
    /// index, because different embedding models produce cosine scores on
    /// very different scales - e.g. a sparse hashed-bag-of-words vector
    /// scores near 0 for unrelated text, while a dense sentence-embedding
    /// model can score 0.3+ for completely unrelated text and 0.6+ for a
    /// real match (measured empirically for
    /// <c>SmartComponents.LocalEmbeddings</c>' default model - see
    /// ADR-0011); one hardcoded threshold can't be right for both.
    /// </summary>
    double RelevanceThreshold { get; }

    /// <summary>Embeds <paramref name="text"/> into a fixed-dimension vector.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
