namespace SharpMud.Engine.Help;

/// <summary>
/// Brute-force cosine-similarity <see cref="IHelpSearchIndex"/> (ADR-0010) -
/// loads every <see cref="HelpTopic"/>/<see cref="HelpTopicChunk"/> through
/// <see cref="IHelpRepository"/> and compares in app code; no vector-search
/// dependency. Fine at help-topic corpus scale (dozens-hundreds of topics);
/// revisit (e.g. an ANN-backed index) if that scale ever changes - see
/// ADR-0010's Negative Consequences.
/// </summary>
public sealed class CosineHelpSearchIndex : IHelpSearchIndex
{
    private readonly IHelpRepository _repository;
    private readonly IEmbeddingProvider _embeddingProvider;

    /// <summary>Creates a search index reading topics through <paramref name="repository"/> and embedding queries via <paramref name="embeddingProvider"/>.</summary>
    public CosineHelpSearchIndex(IHelpRepository repository, IEmbeddingProvider embeddingProvider)
    {
        _repository = repository;
        _embeddingProvider = embeddingProvider;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HelpSearchHit>> SearchAsync(string query, CancellationToken ct)
    {
        var queryVector = await _embeddingProvider.EmbedAsync(query, ct);
        var topics = await _repository.GetAllTopicsAsync(ct);
        var relevanceThreshold = _embeddingProvider.RelevanceThreshold;

        var bestByTopic = new Dictionary<HelpTopicId, HelpSearchHit>();
        foreach (var topic in topics)
        {
            foreach (var chunk in topic.Chunks)
            {
                var score = CosineSimilarity(queryVector, chunk.Embedding);
                if (score < relevanceThreshold)
                    continue;

                if (!bestByTopic.TryGetValue(topic.Id, out var existing) || score > existing.Score)
                    bestByTopic[topic.Id] = new HelpSearchHit(topic, score);
            }
        }

        return bestByTopic.Values.OrderByDescending(hit => hit.Score).ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dot = 0, magnitudeA = 0, magnitudeB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dot / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
