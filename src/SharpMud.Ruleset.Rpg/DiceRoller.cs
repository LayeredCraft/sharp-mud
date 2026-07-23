using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

public sealed class DiceRoller : IDiceRoller
{
    private readonly IRandomSource _random;

    public DiceRoller(IRandomSource random)
    {
        _random = random;
    }

    public int Roll(int diceCount, int sides, int modifier = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(diceCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);

        var total = modifier;
        for (var i = 0; i < diceCount; i++)
            total += _random.Next(1, sides);

        return total;
    }
}
