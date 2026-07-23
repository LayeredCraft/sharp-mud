using SharpMud.Engine.Core;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic;

// Deliberately minimal - plain numeric attributes only, no Race/CharacterClass
// (that's Classic-flavored content, not what Basic promises). Attached
// alongside SharpMud.Engine's PlayerBehavior to make a Thing a character;
// SharpMud.Ruleset.Rpg's CombatantBehavior handles the actual combat numbers.
/// <summary>
/// Basic's minimal character-progression behavior - just level and
/// experience, no race/class/attributes. Combat HP lives on <see
/// cref="CombatantBehavior"/>, not here.
/// </summary>
public sealed class BasicStatsBehavior : Behavior
{
    /// <summary>The character's level. Not currently used to modify combat numbers - see docs/character.md.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Accumulated experience, adjusted by <see cref="BasicCombatOutcomeHandler"/> on combat wins/losses.</summary>
    public long Experience { get; set; }
}
