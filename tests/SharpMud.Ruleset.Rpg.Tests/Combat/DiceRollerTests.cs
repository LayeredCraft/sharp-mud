using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg.Tests.Combat;

public sealed class DiceRollerTests
{
    [Fact]
    public void Roll_SumsEachDieResultPlusModifier()
    {
        var random = Substitute.For<IRandomSource>();
        random.Next(1, 6).Returns(3, 5, 2);

        var sut = new DiceRoller(random);

        var result = sut.Roll(3, 6, modifier: 4);

        result.Should().Be(3 + 5 + 2 + 4);
    }

    [Fact]
    public void Roll_DefaultsModifierToZero()
    {
        var random = Substitute.For<IRandomSource>();
        random.Next(1, 20).Returns(15);

        var sut = new DiceRoller(random);

        var result = sut.Roll(1, 20);

        result.Should().Be(15);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(-1, 6)]
    public void Roll_Throws_WhenDiceCountIsLessThanOne(int diceCount, int sides)
    {
        var random = Substitute.For<IRandomSource>();
        var sut = new DiceRoller(random);

        var act = () => sut.Roll(diceCount, sides);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Roll_Throws_WhenSidesIsLessThanOne(int diceCount, int sides)
    {
        var random = Substitute.For<IRandomSource>();
        var sut = new DiceRoller(random);

        var act = () => sut.Roll(diceCount, sides);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
