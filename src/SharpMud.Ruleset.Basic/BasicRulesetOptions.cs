namespace SharpMud.Ruleset.Basic;

// Plain mutable options class configured via AddSharpMudBasicRuleset(...)'s
// callback, not IOptions<T>/appsettings.json-bound - same shape as Engine's
// GameLoopOptions. These are the tunable starting numbers for a fresh
// player character; the default default world's NPC keeps its own fixed
// stats regardless (small, deliberately simple content, not a tunable).
public sealed class BasicRulesetOptions
{
    public int StartingHitPoints { get; set; } = 20;
    public int StartingArmorClass { get; set; } = 10;
    public int StartingDamageMin { get; set; } = 1;
    public int StartingDamageMax { get; set; } = 4;
}
