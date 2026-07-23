using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Rpg;

/// <summary>
/// The default <see cref="IDiceRoller"/> - sums <c>diceCount</c>
/// independent rolls of <see cref="IRandomSource"/> over <c>[1,
/// sides]</c>, plus a flat modifier. Public (rather than internal) so a
/// consumer can construct it directly in their own tests.
/// </summary>
public sealed class DiceRoller : IDiceRoller
{
    private readonly IRandomSource _random;

    /// <summary>Creates the roller against the engine's randomness source.</summary>
    public DiceRoller(IRandomSource random)
    {
        _random = random;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="diceCount"/> or <paramref name="sides"/> is less than 1.</exception>
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
