using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class AreaBehaviorConfiguration : IEntityTypeConfiguration<AreaBehavior>
{
    public void Configure(EntityTypeBuilder<AreaBehavior> builder)
    {
    }
}
