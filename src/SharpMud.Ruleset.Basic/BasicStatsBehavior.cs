using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Basic;

// Deliberately minimal - plain numeric attributes only, no Race/CharacterClass
// (that's Classic-flavored content, not what Basic promises). Attached
// alongside SharpMud.Engine's PlayerBehavior to make a Thing a character;
// SharpMud.Ruleset.Rpg's CombatantBehavior handles the actual combat numbers.
public sealed class BasicStatsBehavior : Behavior
{
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
}
