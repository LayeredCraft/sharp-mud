using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class WanderingBehaviorConfiguration : IEntityTypeConfiguration<WanderingBehavior>
{
    public void Configure(EntityTypeBuilder<WanderingBehavior> builder)
    {
        // WanderChancePercent is a plain int - default mapping.
    }
}
