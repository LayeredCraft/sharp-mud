using SharpMud.Engine.Help;

namespace SharpMud.Engine.Tests.Help;

public sealed class CosineHelpSearchIndexTests
{
    private static HelpTopic MakeTopic(string key, params HelpTopicChunk[] chunks)
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = key };
        topic.ReplaceChunks(chunks);
        return topic;
    }

    [Fact]
    public async Task SearchAsync_ReturnsTopic_WhenChunkScoreClearsThreshold()
    {
        var topic = MakeTopic("wizard", new HelpTopicChunk(Guid.NewGuid(), HelpTopicId.New(), 0, "text", [1f, 0f], "model", "hash"));
        var repository = Substitute.For<IHelpRepository>();
        repository.GetAllTopicsAsync(Arg.Any<CancellationToken>()).Returns([topic]);
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        embeddingProvider.RelevanceThreshold.Returns(0.15);
        embeddingProvider.EmbedAsync("query", Arg.Any<CancellationToken>()).Returns([1f, 0f]);

        var sut = new CosineHelpSearchIndex(repository, embeddingProvider);

        var hits = await sut.SearchAsync("query", TestContext.Current.CancellationToken);

        hits.Should().ContainSingle();
        hits[0].Topic.Should().Be(topic);
        hits[0].Score.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenBestScoreIsBelowThreshold()
    {
        var topic = MakeTopic("wizard", new HelpTopicChunk(Guid.NewGuid(), HelpTopicId.New(), 0, "text", [1f, 0f], "model", "hash"));
        var repository = Substitute.For<IHelpRepository>();
        repository.GetAllTopicsAsync(Arg.Any<CancellationToken>()).Returns([topic]);
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        embeddingProvider.RelevanceThreshold.Returns(0.15);
        // Orthogonal vector - cosine similarity 0, well below the threshold.
        embeddingProvider.EmbedAsync("query", Arg.Any<CancellationToken>()).Returns([0f, 1f]);

        var sut = new CosineHelpSearchIndex(repository, embeddingProvider);

        var hits = await sut.SearchAsync("query", TestContext.Current.CancellationToken);

        hits.Should().BeEmpty("a weak match must yield no result, not a wrong-but-confident guess");
    }

    [Fact]
    public async Task SearchAsync_OrdersHits_ByDescendingScore()
    {
        var weakTopic = MakeTopic("weak", new HelpTopicChunk(Guid.NewGuid(), HelpTopicId.New(), 0, "text", [0.2f, 0.98f], "model", "hash"));
        var strongTopic = MakeTopic("strong", new HelpTopicChunk(Guid.NewGuid(), HelpTopicId.New(), 0, "text", [1f, 0f], "model", "hash"));
        var repository = Substitute.For<IHelpRepository>();
        repository.GetAllTopicsAsync(Arg.Any<CancellationToken>()).Returns([weakTopic, strongTopic]);
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        embeddingProvider.RelevanceThreshold.Returns(0.15);
        embeddingProvider.EmbedAsync("query", Arg.Any<CancellationToken>()).Returns([1f, 0f]);

        var sut = new CosineHelpSearchIndex(repository, embeddingProvider);

        var hits = await sut.SearchAsync("query", TestContext.Current.CancellationToken);

        hits.Should().HaveCount(2);
        hits[0].Topic.Should().Be(strongTopic);
        hits[1].Topic.Should().Be(weakTopic);
    }

    [Fact]
    public async Task SearchAsync_UsesBestChunkPerTopic_NotFirst()
    {
        var weakChunk = new HelpTopicChunk(Guid.NewGuid(), HelpTopicId.New(), 0, "weak", [0f, 1f], "model", "hash");
        var strongChunk = new HelpTopicChunk(Guid.NewGuid(), HelpTopicId.New(), 1, "strong", [1f, 0f], "model", "hash");
        var topic = MakeTopic("wizard", weakChunk, strongChunk);
        var repository = Substitute.For<IHelpRepository>();
        repository.GetAllTopicsAsync(Arg.Any<CancellationToken>()).Returns([topic]);
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        embeddingProvider.RelevanceThreshold.Returns(0.15);
        embeddingProvider.EmbedAsync("query", Arg.Any<CancellationToken>()).Returns([1f, 0f]);

        var sut = new CosineHelpSearchIndex(repository, embeddingProvider);

        var hits = await sut.SearchAsync("query", TestContext.Current.CancellationToken);

        hits.Should().ContainSingle().Which.Score.Should().BeApproximately(1.0, 0.0001);
    }
}
