using SharpMud.Engine.Help;
using SmartComponents.LocalEmbeddings;

namespace SharpMud.Samples.Classic;

/// <summary>
/// Real, local <see cref="IEmbeddingProvider"/> (ADR-0011) - wraps
/// <c>SmartComponents.LocalEmbeddings</c>' <see cref="LocalEmbedder"/>
/// (ONNX Runtime, the package's default <c>bge-micro-v2</c> model
/// downloaded at build time, no runtime network call). Registered in
/// <c>Program.cs</c> in place of the default
/// <c>StubEmbeddingProvider</c> - the sample-app-only opt-in ADR-0010
/// anticipated, not a change to what any other consumer gets by default.
/// </summary>
public sealed class LocalEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    // Shared across every EmbedAsync call - the docs explicitly call out
    // that a single LocalEmbedder instance is safe to share across threads,
    // and constructing one loads the ONNX model, so one per app (DI
    // singleton), not one per call.
    private readonly LocalEmbedder _embedder = new();

    /// <inheritdoc/>
    public string ModelId => "smartcomponents-local-embeddings-default";

    // Measured directly against this model/corpus shape (ADR-0011): a
    // topic body about spellcasting scored ~0.62-0.68 cosine similarity
    // against real synonym queries ("wizard", "sorcerer", "how do i cast
    // spells"). "Unrelated" queries turned out to span a wider band than
    // first measured - genuinely unrelated text like "the weather today"
    // scores ~0.33, but short, generic single-word queries (built-in verbs
    // like "up", "who") scored as high as ~0.54, closer to the "related"
    // cluster than expected. 0.5 initially picked here caused "help up" to
    // incorrectly match this topic. 0.58 leaves real margin on both sides
    // of the measured gap; still empirically chosen, not derived from a
    // formula, and still worth revisiting once real topic content and
    // real player queries exist to calibrate against.
    public double RelevanceThreshold => 0.58;

    /// <inheritdoc/>
    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var embedding = _embedder.Embed<EmbeddingF32>(text);
        return Task.FromResult(embedding.Values.ToArray());
    }

    /// <inheritdoc/>
    public void Dispose() => _embedder.Dispose();
}
