namespace SharpMud.Engine.Combat;

public sealed class RandomSource : IRandomSource
{
    public int Next(int minInclusive, int maxInclusive) => Random.Shared.Next(minInclusive, maxInclusive + 1);
}
