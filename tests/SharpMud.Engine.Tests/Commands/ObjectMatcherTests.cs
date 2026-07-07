using SharpMud.Engine.Commands;

namespace SharpMud.Engine.Tests.Commands;

public sealed class ObjectMatcherTests
{
    private sealed record Named(string Name);

    [Fact]
    public void ParseTarget_ReturnsOrdinalOne_WhenNoPrefixGiven()
    {
        var (ordinal, name) = ObjectMatcher.ParseTarget("sword");

        ordinal.Should().Be(1);
        name.Should().Be("sword");
    }

    [Fact]
    public void ParseTarget_ExtractsOrdinal_WhenDotPrefixGiven()
    {
        var (ordinal, name) = ObjectMatcher.ParseTarget("2.sword");

        ordinal.Should().Be(2);
        name.Should().Be("sword");
    }

    [Fact]
    public void ParseTarget_TreatsNonNumericPrefixAsPartOfName()
    {
        // "st.sword" isn't "<ordinal>.<name>" - the whole thing is the name.
        var (ordinal, name) = ObjectMatcher.ParseTarget("st.sword");

        ordinal.Should().Be(1);
        name.Should().Be("st.sword");
    }

    [Fact]
    public void FindMatch_ReturnsFirstMatch_WhenNoOrdinalGiven()
    {
        var candidates = new[] { new Named("rusty sword"), new Named("shiny sword") };

        var result = ObjectMatcher.FindMatch(candidates, "sword", c => c.Name);

        result.Should().Be(candidates[0]);
    }

    [Fact]
    public void FindMatch_ReturnsSecondMatch_WhenOrdinalPrefixGiven()
    {
        var candidates = new[] { new Named("rusty sword"), new Named("shiny sword") };

        var result = ObjectMatcher.FindMatch(candidates, "2.sword", c => c.Name);

        result.Should().Be(candidates[1]);
    }

    [Fact]
    public void FindMatch_ReturnsNull_WhenOrdinalExceedsMatchCount()
    {
        var candidates = new[] { new Named("rusty sword") };

        var result = ObjectMatcher.FindMatch(candidates, "2.sword", c => c.Name);

        result.Should().BeNull();
    }

    [Fact]
    public void FindMatch_IsCaseInsensitive()
    {
        var candidates = new[] { new Named("Rusty Sword") };

        var result = ObjectMatcher.FindMatch(candidates, "SWORD", c => c.Name);

        result.Should().Be(candidates[0]);
    }
}
