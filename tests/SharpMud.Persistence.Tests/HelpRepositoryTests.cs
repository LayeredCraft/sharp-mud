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
