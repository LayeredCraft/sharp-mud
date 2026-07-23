namespace SharpMud.Ruleset.Basic;

/// <summary>
/// Tunable starting numbers for a fresh player character, configured via
/// <c>AddSharpMudBasicRuleset(...)</c>'s callback - not <c>IOptions&lt;T&gt;</c>/
/// appsettings.json-bound, same shape as <c>Engine</c>'s
/// <c>GameLoopOptions</c>. The default world's NPC keeps its own fixed
/// stats regardless (small, deliberately simple content, not a tunable).
/// </summary>
public sealed class BasicRulesetOptions
{
    /// <summary>A fresh character's starting (and max) hit points. Must be at least 1 - see <see cref="Validate"/>.</summary>
    public int StartingHitPoints { get; set; } = 20;

    /// <summary>A fresh character's starting armor class.</summary>
    public int StartingArmorClass { get; set; } = 10;

    /// <summary>A fresh character's minimum damage per hit. Must be at least 1 - see <see cref="Validate"/>.</summary>
    public int StartingDamageMin { get; set; } = 1;

    /// <summary>A fresh character's maximum damage per hit. Must be at least <see cref="StartingDamageMin"/> - see <see cref="Validate"/>.</summary>
    public int StartingDamageMax { get; set; } = 4;

    /// <summary>
    /// Fails fast at composition-root time on a combat-breaking configuration
    /// (non-positive starting HP/damage, or a damage range with no valid
    /// rolls) - called by <c>AddSharpMudBasicRuleset(...)</c> right after its
    /// <c>configureOptions</c> callback runs, so a bad value surfaces at
    /// startup instead of the first time a fight actually happens.
    /// </summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(StartingHitPoints, 1, nameof(StartingHitPoints));
        ArgumentOutOfRangeException.ThrowIfLessThan(StartingDamageMin, 1, nameof(StartingDamageMin));
        ArgumentOutOfRangeException.ThrowIfLessThan(StartingDamageMax, StartingDamageMin, nameof(StartingDamageMax));
    }
}
