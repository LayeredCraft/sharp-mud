using SharpMud.Engine.Core;

namespace SharpMud.Ruleset.Classic;

// The D&D-style attribute/derived-stat block from docs/character.md. Attached
// to whichever Things the ruleset wants to be a "character" - a Thing with
// this plus an engine PlayerBehavior is a player character.
public sealed class StatsBehavior : Behavior
{
    public Race Race { get; set; }
    public CharacterClass Class { get; set; }

    // Base attributes; Race/Class modifier tables are not yet defined (see
    // docs/character.md Open Items) so these are applied unmodified for now.
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }

    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int MaxMana { get; set; }
    public int CurrentMana { get; set; }
    public int MaxStamina { get; set; }
    public int CurrentStamina { get; set; }

    public int Level { get; set; } = 1;
    public long Experience { get; set; }
}
