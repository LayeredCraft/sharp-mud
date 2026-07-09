using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class NpcBehaviorConfiguration : IEntityTypeConfiguration<NpcBehavior>
{
    public void Configure(EntityTypeBuilder<NpcBehavior> builder)
    {
    }
}
