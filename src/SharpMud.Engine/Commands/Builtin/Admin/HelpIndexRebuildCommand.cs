using SharpMud.Engine.Help;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>
/// The <c>helpindex rebuild</c> command (<see cref="SecurityRole.MinorBuilder"/>,
/// ADR-0010) - re-chunks and re-embeds every <see cref="HelpTopic"/>'s <see
/// cref="HelpTopic.Body"/> via <see cref="IEmbeddingProvider"/>, replacing
/// each topic's <see cref="HelpTopic.Chunks"/> wholesale. No automatic
/// trigger exists (not on save, not at boot) - this command is the only way
/// the semantic-search index changes, deliberately keeping embedding-provider
/// calls out of the content-edit path (ADR-0010's Decision Outcome).
/// </summary>
public sealed class HelpIndexRebuildCommand : ICommand
{
    private readonly IHelpRepository _repository;
    private readonly IEmbeddingProvider _embeddingProvider;

    /// <summary>Creates the command, rebuilding topics from <paramref name="repository"/> via <paramref name="embeddingProvider"/>.</summary>
    public HelpIndexRebuildCommand(IHelpRepository repository, IEmbeddingProvider embeddingProvider)
    {
        _repository = repository;
        _embeddingProvider = embeddingProvider;
    }

    /// <inheritdoc/>
    public string Verb => "helpindex";

    /// <inheritdoc/>
    public IReadOnlyList<string> Aliases { get; } = [];

    /// <inheritdoc/>
    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args is not ["rebuild"])
        {
            await ctx.Session.WriteLineAsync("Usage: helpindex rebuild", ct);
            return;
        }

        var topics = await _repository.GetAllTopicsAsync(ct);
        foreach (var topic in topics)
        {
            var chunkTexts = HelpTopicChunker.Split(topic.Body);
            var chunks = new List<HelpTopicChunk>(chunkTexts.Count);
            for (var i = 0; i < chunkTexts.Count; i++)
            {
                var embedding = await _embeddingProvider.EmbedAsync(chunkTexts[i], ct);
                chunks.Add(new HelpTopicChunk(Guid.CreateVersion7(), topic.Id, i, chunkTexts[i], embedding, _embeddingProvider.ModelId, topic.ContentHash));
            }

            topic.ReplaceChunks(chunks);
            await _repository.SaveTopicAsync(topic, ct);
        }

        var chunkCount = topics.Sum(t => t.Chunks.Count);
        await ctx.Session.WriteLineAsync($"Rebuilt the help index: {topics.Count} topic(s), {chunkCount} chunk(s).", ct);
    }
}
