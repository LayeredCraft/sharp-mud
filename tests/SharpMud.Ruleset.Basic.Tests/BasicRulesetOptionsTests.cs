namespace SharpMud.Ruleset.Basic.Tests;

public sealed class BasicRulesetOptionsTests
{
    [Fact]
    public void Validate_DoesNotThrow_ForDefaultOptions()
    {
        var sut = new BasicRulesetOptions();

        var act = sut.Validate;

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Throws_WhenStartingHitPointsIsNotPositive(int hitPoints)
    {
        var sut = new BasicRulesetOptions { StartingHitPoints = hitPoints };

        var act = sut.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Throws_WhenStartingDamageMinIsNotPositive(int damageMin)
    {
        var sut = new BasicRulesetOptions { StartingDamageMin = damageMin };

        var act = sut.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_Throws_WhenStartingDamageMaxIsLessThanStartingDamageMin()
    {
        var sut = new BasicRulesetOptions { StartingDamageMin = 5, StartingDamageMax = 4 };

        var act = sut.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
