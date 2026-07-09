using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

// No extra columns - registering the type is enough for TPH discovery,
// since navigation-based auto-discovery is off (see BehaviorConfiguration).
public sealed class RoomBehaviorConfiguration : IEntityTypeConfiguration<RoomBehavior>
{
    public void Configure(EntityTypeBuilder<RoomBehavior> builder)
    {
    }
}
