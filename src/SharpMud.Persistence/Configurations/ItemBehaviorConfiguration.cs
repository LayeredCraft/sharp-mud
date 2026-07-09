using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class ItemBehaviorConfiguration : IEntityTypeConfiguration<ItemBehavior>
{
    public void Configure(EntityTypeBuilder<ItemBehavior> builder)
    {
    }
}
