using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharpMud.Ruleset.Basic.Configurations;

// internal: discovered via ApplyConfigurationsFromAssembly's reflection scan
// (BasicBehaviorMappingContributor), never referenced by name - not part of
// this package's public contract.
internal sealed class BasicStatsBehaviorConfiguration : IEntityTypeConfiguration<BasicStatsBehavior>
{
    public void Configure(EntityTypeBuilder<BasicStatsBehavior> builder)
    {
        // All plain int/long properties - default mapping.
    }
}
