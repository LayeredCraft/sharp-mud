using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharpMud.Ruleset.Classic.Configurations;

public sealed class StatsBehaviorConfiguration : IEntityTypeConfiguration<StatsBehavior>
{
    public void Configure(EntityTypeBuilder<StatsBehavior> builder)
    {
        // Race/CharacterClass are LayeredCraft.OptimizedEnums, not plain
        // enums - convert to/from their underlying int Value.
        builder.Property(x => x.Race).HasConversion(r => r.Value, v => RaceFromValue(v));
        builder.Property(x => x.Class).HasConversion(c => c.Value, v => CharacterClassFromValue(v));
    }

    private static Race RaceFromValue(int value) => Race.TryFromValue(value, out var race) ? race : Race.Human;

    private static CharacterClass CharacterClassFromValue(int value) =>
        CharacterClass.TryFromValue(value, out var cls) ? cls : CharacterClass.Warrior;
}
