using SharpMud.Engine.Help;

namespace SharpMud.Engine.Tests.Help;

public sealed class HelpTopicTests
{
    [Fact]
    public void ContentHash_Changes_WhenBodyChanges()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "First version." };
        var firstHash = topic.ContentHash;

        topic.Body = "Second version.";

        topic.ContentHash.Should().NotBe(firstHash);
    }

    [Fact]
    public void ContentHash_IsStable_ForUnchangedBody()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Stable text." };

        topic.ContentHash.Should().Be(topic.ContentHash);
    }

    [Fact]
    public void SetAliases_ReplacesPreviousAliases_Wholesale()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard" };
        topic.SetAliases(["mage", "sorcerer"]);

        topic.SetAliases(["spellcaster"]);

        topic.Aliases.Should().Equal("spellcaster");
    }

    [Fact]
    public void ReplaceChunks_ReplacesPreviousChunks_Wholesale()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard" };
        var firstChunk = new HelpTopicChunk(Guid.NewGuid(), topic.Id, 0, "old", [1f], "model-v1", "hash1");
        topic.ReplaceChunks([firstChunk]);

        var secondChunk = new HelpTopicChunk(Guid.NewGuid(), topic.Id, 0, "new", [2f], "model-v1", "hash2");
        topic.ReplaceChunks([secondChunk]);

        topic.Chunks.Should().ContainSingle().Which.Should().Be(secondChunk);
    }

    [Fact]
    public void Touch_UpdatesUpdatedAtUtc()
    {
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard" };
        var before = topic.UpdatedAtUtc;

        topic.Touch();

        topic.UpdatedAtUtc.Should().BeOnOrAfter(before);
    }
}
