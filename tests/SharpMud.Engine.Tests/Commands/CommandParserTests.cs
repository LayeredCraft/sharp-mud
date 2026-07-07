using SharpMud.Engine.Commands;

namespace SharpMud.Engine.Tests.Commands;

public sealed class CommandParserTests
{
    private readonly CommandParser _sut = new();

    [Fact]
    public void Parse_LowercasesVerb_WhenInputIsMixedCase()
    {
        var result = _sut.Parse("NoRTh");

        result.Verb.Should().Be("north");
    }

    [Fact]
    public void Parse_SplitsArgsFromVerb_WhenInputHasMultipleTokens()
    {
        var result = _sut.Parse("say hello there");

        result.Verb.Should().Be("say");
        result.Args.Should().Equal("hello", "there");
    }

    [Fact]
    public void Parse_CollapsesRepeatedWhitespace_WhenInputHasExtraSpaces()
    {
        var result = _sut.Parse("  say   hello   there  ");

        result.Verb.Should().Be("say");
        result.Args.Should().Equal("hello", "there");
    }

    [Fact]
    public void Parse_ReturnsEmptyVerb_WhenInputIsBlank()
    {
        var result = _sut.Parse("   ");

        result.Verb.Should().BeEmpty();
        result.Args.Should().BeEmpty();
    }

    [Fact]
    public void Parse_PreservesRawInput_Always()
    {
        var result = _sut.Parse("  Say Hi  ");

        result.RawInput.Should().Be("  Say Hi  ");
    }
}
