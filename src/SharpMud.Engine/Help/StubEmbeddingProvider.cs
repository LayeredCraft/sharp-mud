namespace SharpMud.Engine.Help;

/// <summary>
/// Deterministic placeholder <see cref="IEmbeddingProvider"/> (ADR-0010) -
/// feature-hashes lowercase word tokens into a fixed-size vector (the
/// "hashing trick"), with no external dependency or model asset. Retrieval
/// quality reflects literal word overlap only (no synonym/semantic
/// understanding - "wizard" and "mage" share no signal); this exists to
/// validate the pipeline/abstraction end-to-end with fully deterministic,
/// testable output, not to be the long-term provider. A real model (local
/// or API-based) swaps in behind <see cref="IEmbeddingProvider"/> later -
/// see ADR-0010's Negative Consequences.
/// </summary>
public sealed class StubEmbeddingProvider : IEmbeddingProvider
{
    private const int Dimensions = 128;

    private static readonly char[] TokenSeparators = [' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '"', '\''];

    /// <inheritdoc/>
    public string ModelId => "stub-hashed-bow-v1";

    /// <summary>Tuned empirically for this provider's sparse hashed-bag-of-words vectors, which score near 0 for genuinely unrelated text.</summary>
    public double RelevanceThreshold => 0.15;

    /// <inheritdoc/>
    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var vector = new float[Dimensions];
        foreach (var word in text.ToLowerInvariant().Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
            vector[(int)(Fnv1aHash(word) % Dimensions)] += 1f;

        Normalize(vector);
        return Task.FromResult(vector);
    }

    // FNV-1a - deterministic across processes/runs, unlike string.GetHashCode
    // (randomized per-process by default in .NET), which this needs to be
    // reproducible: the same word must always hash to the same bucket, both
    // within one run (query vs. chunk) and across separate rebuild/search
    // calls.
    private static uint Fnv1aHash(string text)
    {
        var hash = 2166136261u;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= 16777619u;
        }

        return hash;
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude == 0f)
            return;

        for (var i = 0; i < vector.Length; i++)
            vector[i] /= magnitude;
    }
}
