using Microsoft.EntityFrameworkCore;
using SharpMud.Engine.Help;
using SharpMud.Persistence.Tests.TestKit;

namespace SharpMud.Persistence.Tests;

public sealed class HelpRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly HelpRepository _sut;

    public HelpRepositoryTests()
    {
        _sut = new HelpRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SaveTopicAsync_ThenGetAllTopicsAsync_RoundTripsTopicWithAliasesKeywordsAndChunks()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Category = "classes", Body = "How to become a wizard." };
        topic.SetAliases(["mage", "sorcerer"]);
        topic.SetKeywords(["magic", "spellcasting"]);
        topic.ReplaceChunks([
            new HelpTopicChunk(Guid.NewGuid(), topic.Id, 0, "How to become a wizard.", [0.1f, 0.2f, 0.3f], "stub-hashed-bow-v1", topic.ContentHash),
        ]);

        await _sut.SaveTopicAsync(topic, TestContext.Current.CancellationToken);

        var all = await _sut.GetAllTopicsAsync(TestContext.Current.CancellationToken);

        var loaded = all.Should().ContainSingle().Subject;
        loaded.Key.Should().Be("wizard");
        loaded.Category.Should().Be("classes");
        loaded.Body.Should().Be("How to become a wizard.");
        loaded.Aliases.Should().BeEquivalentTo(["mage", "sorcerer"]);
        loaded.Keywords.Should().BeEquivalentTo(["magic", "spellcasting"]);

        var chunk = loaded.Chunks.Should().ContainSingle().Subject;
        chunk.Text.Should().Be("How to become a wizard.");
        chunk.Embedding.Should().BeEquivalentTo(new[] { 0.1f, 0.2f, 0.3f });
        chunk.EmbeddingModelId.Should().Be("stub-hashed-bow-v1");
    }

    [Fact]
    public async Task FindByNameOrAliasAsync_MatchesByKey_CaseInsensitive()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Text." };
        await _sut.SaveTopicAsync(topic, TestContext.Current.CancellationToken);

        var found = await _sut.FindByNameOrAliasAsync("WIZARD", TestContext.Current.CancellationToken);

        found.Should().NotBeNull();
        found!.Id.Should().Be(topic.Id);
    }

    [Fact]
    public async Task FindByNameOrAliasAsync_MatchesByAlias()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Text." };
        topic.SetAliases(["mage"]);
        await _sut.SaveTopicAsync(topic, TestContext.Current.CancellationToken);

        var found = await _sut.FindByNameOrAliasAsync("mage", TestContext.Current.CancellationToken);

        found.Should().NotBeNull();
        found!.Id.Should().Be(topic.Id);
    }

    [Fact]
    public async Task FindByNameOrAliasAsync_ReturnsNull_WhenNoMatch()
    {
        var found = await _sut.FindByNameOrAliasAsync("nobody", TestContext.Current.CancellationToken);

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindByKeywordAsync_ReturnsMatchingTopics()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Text." };
        topic.SetKeywords(["magic"]);
        await _sut.SaveTopicAsync(topic, TestContext.Current.CancellationToken);

        var found = await _sut.FindByKeywordAsync("magic", TestContext.Current.CancellationToken);

        found.Should().ContainSingle().Which.Id.Should().Be(topic.Id);
    }

    [Fact]
    public async Task FindByKeywordAsync_OrdersMultipleMatches_ByKey()
    {
        var zebra = new HelpTopic { Id = HelpTopicId.New(), Key = "zebra-topic", Body = "Text." };
        zebra.SetKeywords(["magic"]);
        var alpha = new HelpTopic { Id = HelpTopicId.New(), Key = "alpha-topic", Body = "Text." };
        alpha.SetKeywords(["magic"]);
        await _sut.SaveTopicAsync(zebra, TestContext.Current.CancellationToken);
        await _sut.SaveTopicAsync(alpha, TestContext.Current.CancellationToken);

        var found = await _sut.FindByKeywordAsync("magic", TestContext.Current.CancellationToken);

        found.Select(t => t.Key).Should().Equal("alpha-topic", "zebra-topic");
    }

    [Fact]
    public async Task SaveTopicAsync_ThrowsOnDuplicateKey_WhenBypassingRepositorysOwnIdDedupe()
    {
        var first = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Text." };
        await _sut.SaveTopicAsync(first, TestContext.Current.CancellationToken);

        // A distinct Id but the same Key - HelpRepository.SaveTopicAsync
        // only dedupes by Id, so this simulates two concurrent creates for
        // the same new key racing each other; the unique index is what
        // actually prevents the duplicate row.
        var second = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Other text." };

        var act = async () => await _sut.SaveTopicAsync(second, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeletingATopicDirectly_CascadesToItsChunks_EvenBypassingHelpRepository()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Text." };
        topic.ReplaceChunks([new HelpTopicChunk(Guid.NewGuid(), topic.Id, 0, "text", [1f], "model", "hash")]);
        await _sut.SaveTopicAsync(topic, TestContext.Current.CancellationToken);

        // Deletes the HelpTopic row directly, bypassing
        // HelpRepository.DeleteTopicAsync entirely (which would clean up
        // chunks itself) - proves the FK's cascade delete is what actually
        // prevents an orphaned chunk here, not just HelpRepository's own
        // discipline.
        await using (var context = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            var row = await context.HelpTopics.SingleAsync(t => t.Id == topic.Id, TestContext.Current.CancellationToken);
            context.HelpTopics.Remove(row);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            var orphanedChunks = await context.HelpTopicChunks.Where(c => c.HelpTopicId == topic.Id).ToListAsync(TestContext.Current.CancellationToken);
            orphanedChunks.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task SaveTopicAsync_CalledTwice_ReplacesChunksWholesale()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Text." };
        topic.ReplaceChunks([new HelpTopicChunk(Guid.NewGuid(), topic.Id, 0, "old", [1f], "model-v1", "hash1")]);
        await _sut.SaveTopicAsync(topic, TestContext.Current.CancellationToken);

        topic.ReplaceChunks([new HelpTopicChunk(Guid.NewGuid(), topic.Id, 0, "new", [2f], "model-v2", "hash2")]);
        await _sut.SaveTopicAsync(topic, TestContext.Current.CancellationToken);

        var all = await _sut.GetAllTopicsAsync(TestContext.Current.CancellationToken);
        var loaded = all.Should().ContainSingle().Subject;
        loaded.Chunks.Should().ContainSingle().Which.Text.Should().Be("new");
    }

    [Fact]
    public async Task DeleteTopicAsync_RemovesTopicAndItsChunks()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Text." };
        topic.ReplaceChunks([new HelpTopicChunk(Guid.NewGuid(), topic.Id, 0, "text", [1f], "model", "hash")]);
        await _sut.SaveTopicAsync(topic, TestContext.Current.CancellationToken);

        await _sut.DeleteTopicAsync(topic.Id, TestContext.Current.CancellationToken);

        var all = await _sut.GetAllTopicsAsync(TestContext.Current.CancellationToken);
        all.Should().BeEmpty();
    }
}
