using SharpMud.Engine.Help;

namespace SharpMud.Engine.Tests.Help;

public sealed class HelpTopicChunkerTests
{
    [Fact]
    public void Split_ReturnsEmpty_ForEmptyOrWhitespaceBody()
    {
        HelpTopicChunker.Split("").Should().BeEmpty();
        HelpTopicChunker.Split("   \n  ").Should().BeEmpty();
    }

    [Fact]
    public void Split_ReturnsSingleChunk_ForSingleParagraph()
    {
        var chunks = HelpTopicChunker.Split("Wizards cast spells using mana.");

        chunks.Should().ContainSingle().Which.Should().Be("Wizards cast spells using mana.");
    }

    [Fact]
    public void Split_SplitsOnBlankLines_IntoTrimmedParagraphs()
    {
        var chunks = HelpTopicChunker.Split("First paragraph.\n\nSecond paragraph.\n\n  Third, with padding.  ");

        chunks.Should().Equal("First paragraph.", "Second paragraph.", "Third, with padding.");
    }
}
