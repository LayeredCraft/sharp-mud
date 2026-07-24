namespace SharpMud.Engine.Help;

/// <summary>
/// One embedded slice of a <see cref="HelpTopic"/>'s <see cref="HelpTopic.Body"/>
/// (ADR-0010) - a value object, always regenerated wholesale by <c>helpindex
/// rebuild</c> (see <see cref="HelpTopic.ReplaceChunks"/>), never edited in
/// place.
/// </summary>
/// <param name="Id">Row identity - needed only because it's its own persisted table row, not a domain concept callers reason about.</param>
/// <param name="HelpTopicId">The owning <see cref="HelpTopic"/>.</param>
/// <param name="ChunkIndex">Position within the topic's chunk sequence.</param>
/// <param name="Text">The chunk's source text, as embedded.</param>
/// <param name="Embedding">The embedding vector produced by <see cref="IEmbeddingProvider.EmbedAsync"/> for <paramref name="Text"/>.</param>
/// <param name="EmbeddingModelId">Which <see cref="IEmbeddingProvider.ModelId"/> produced <paramref name="Embedding"/> - lets a future rebuild detect a model change, not just a content change.</param>
/// <param name="SourceContentHash">The owning topic's <see cref="HelpTopic.ContentHash"/> at embed time - compared against the topic's current hash to detect a stale index.</param>
public sealed record HelpTopicChunk(
    Guid Id,
    HelpTopicId HelpTopicId,
    int ChunkIndex,
    string Text,
    float[] Embedding,
    string EmbeddingModelId,
    string SourceContentHash);
