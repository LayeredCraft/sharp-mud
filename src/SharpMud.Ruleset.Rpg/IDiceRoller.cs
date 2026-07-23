namespace SharpMud.Ruleset.Rpg;

/// <summary>
/// "N dice of M sides plus a modifier" over the engine's <see
/// cref="SharpMud.Engine.Core.IRandomSource"/> - a generic RPG mechanic, not
/// bare randomness (so it doesn't belong in Engine) and not tied to any one
/// ruleset's specific formulas (so it doesn't belong in a concrete leaf
/// package either). See docs/adr/0008-ruleset-scaffolding-tier.md.
/// </summary>
public interface IDiceRoller
{
    /// <summary>Rolls <paramref name="diceCount"/> dice of <paramref name="sides"/> sides each and adds <paramref name="modifier"/>.</summary>
    int Roll(int diceCount, int sides, int modifier = 0);
}
