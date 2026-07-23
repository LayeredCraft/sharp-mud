using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharpMud.Ruleset.Rpg.Configurations;

// internal: discovered via ApplyConfigurationsFromAssembly's reflection scan
// (RpgBehaviorMappingContributor), never referenced by name - not part of
// this package's public contract.
internal sealed class CombatantBehaviorConfiguration : IEntityTypeConfiguration<CombatantBehavior>
{
    public void Configure(EntityTypeBuilder<CombatantBehavior> builder)
    {
        // All plain int properties - default mapping.
    }
}
