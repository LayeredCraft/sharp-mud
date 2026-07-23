using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharpMud.Ruleset.Basic.Configurations;

public sealed class BasicStatsBehaviorConfiguration : IEntityTypeConfiguration<BasicStatsBehavior>
{
    public void Configure(EntityTypeBuilder<BasicStatsBehavior> builder)
    {
        // All plain int/long properties - default mapping.
    }
}
