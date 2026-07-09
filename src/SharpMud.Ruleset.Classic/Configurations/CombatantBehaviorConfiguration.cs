using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharpMud.Ruleset.Classic.Configurations;

public sealed class CombatantBehaviorConfiguration : IEntityTypeConfiguration<CombatantBehavior>
{
    public void Configure(EntityTypeBuilder<CombatantBehavior> builder)
    {
        // All plain int properties - default mapping.
    }
}
