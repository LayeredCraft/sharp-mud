using LayeredCraft.OptimizedEnums;

namespace SharpMud.Ruleset.Classic;

public sealed partial class CharacterClass : OptimizedEnum<CharacterClass, int>
{
    public static readonly CharacterClass Warrior = new(1, nameof(Warrior));
    public static readonly CharacterClass Mage = new(2, nameof(Mage));
    public static readonly CharacterClass Cleric = new(3, nameof(Cleric));
    public static readonly CharacterClass Rogue = new(4, nameof(Rogue));

    private CharacterClass(int value, string name) : base(value, name)
    {
    }
}
