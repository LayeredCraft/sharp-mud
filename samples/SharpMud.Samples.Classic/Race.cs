using LayeredCraft.OptimizedEnums;

namespace SharpMud.Samples.Classic;

// LayeredCraft.OptimizedEnums instead of a plain enum specifically because
// each race will eventually carry its own stat modifiers (docs/character.md
// Open Items) - the singleton instances are the natural place for that data,
// rather than a parallel Dictionary<Race, StatModifiers> lookup table.
public sealed partial class Race : OptimizedEnum<Race, int>
{
    public static readonly Race Human = new(1, nameof(Human));
    public static readonly Race Elf = new(2, nameof(Elf));
    public static readonly Race Dwarf = new(3, nameof(Dwarf));
    public static readonly Race Halfling = new(4, nameof(Halfling));

    private Race(int value, string name) : base(value, name)
    {
    }
}
