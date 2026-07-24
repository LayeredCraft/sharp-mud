using Microsoft.EntityFrameworkCore;
using SharpMud.Engine.Help;

namespace SharpMud.Persistence;

// Loads the full HelpTopic/HelpTopicChunk corpus per call, same "load
// everything" shape ThingRepository already uses - fine at help-topic scale
// (dozens-hundreds of topics), see ADR-0010's Negative Consequences. Fresh
// GameDbContext per call (IDbContextFactory), same reason as
// ThingRepository: DbContext isn't thread-safe, and re-adding the same
// tracked instances across repeated saves would throw.
public sealed class HelpRepository(IDbContextFactory<GameDbContext> dbContextFactory) : IHelpRepository
{
    public async Task<HelpTopic?> FindByNameOrAliasAsync(string name, CancellationToken ct)
    {
        var topics = await GetAllTopicsAsync(ct);
        return topics.FirstOrDefault(t =>
            string.Equals(t.Key, name, StringComparison.OrdinalIgnoreCase) ||
            t.Aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<IReadOnlyList<HelpTopic>> FindByKeywordAsync(string keyword, CancellationToken ct)
    {
        var topics = await GetAllTopicsAsync(ct);
        return topics
            .Where(t => t.Keywords.Any(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase)))
            // Deterministic order (not insertion/rowid order, which SQL
            // makes no guarantee about) so a caller taking FirstOrDefault
            // when multiple topics share a keyword gets a consistent
            // result across calls, not an arbitrary one - caught in PR
            // review.
            .OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<HelpTopic>> GetAllTopicsAsync(CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);

        var topics = await context.HelpTopics.AsNoTracking().ToListAsync(ct);
        var chunksByTopic = (await context.HelpTopicChunks.AsNoTracking().ToListAsync(ct))
            .ToLookup(c => c.HelpTopicId);

        foreach (var topic in topics)
            topic.ReplaceChunks(chunksByTopic[topic.Id].OrderBy(c => c.ChunkIndex));

        return topics;
    }

    // Delete-then-insert, two SaveChangesAsync calls - same PK-conflict
    // avoidance as ThingRepository.SaveTreeAsync (re-adding a still-tracked
    // instance in one batch throws).
    public async Task SaveTopicAsync(HelpTopic topic, CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);

        var existingTopic = await context.HelpTopics.Where(t => t.Id == topic.Id).ToListAsync(ct);
        context.HelpTopics.RemoveRange(existingTopic);

        var existingChunks = await context.HelpTopicChunks.Where(c => c.HelpTopicId == topic.Id).ToListAsync(ct);
        context.HelpTopicChunks.RemoveRange(existingChunks);

        await context.SaveChangesAsync(ct);

        context.HelpTopics.Add(topic);
        context.HelpTopicChunks.AddRange(topic.Chunks);

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteTopicAsync(HelpTopicId id, CancellationToken ct)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);

        var chunks = await context.HelpTopicChunks.Where(c => c.HelpTopicId == id).ToListAsync(ct);
        context.HelpTopicChunks.RemoveRange(chunks);

        var topics = await context.HelpTopics.Where(t => t.Id == id).ToListAsync(ct);
        context.HelpTopics.RemoveRange(topics);

        await context.SaveChangesAsync(ct);
    }
}
