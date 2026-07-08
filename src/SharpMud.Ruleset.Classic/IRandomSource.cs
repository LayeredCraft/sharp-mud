namespace SharpMud.Ruleset.Classic;

public interface IRandomSource
{
    // Inclusive on both ends (unlike System.Random.Next), matching how d20/
    // damage-range rolls are naturally expressed.
    int Next(int minInclusive, int maxInclusive);
}
