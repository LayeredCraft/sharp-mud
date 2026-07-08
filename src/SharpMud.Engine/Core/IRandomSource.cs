namespace SharpMud.Engine.Core;

// Generic randomness abstraction - used by ruleset combat (d20/damage rolls)
// and engine-level wandering AI alike, so it lives in Engine rather than
// being owned by (and duplicated across) individual rulesets.
public interface IRandomSource
{
    // Inclusive on both ends (unlike System.Random.Next), matching how d20/
    // damage-range rolls are naturally expressed.
    int Next(int minInclusive, int maxInclusive);
}
