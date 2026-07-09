using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class WearableBehaviorConfiguration : IEntityTypeConfiguration<WearableBehavior>
{
    public void Configure(EntityTypeBuilder<WearableBehavior> builder)
    {
        // Slot is a plain enum (not an OptimizedEnum) - default int mapping.
    }
}
