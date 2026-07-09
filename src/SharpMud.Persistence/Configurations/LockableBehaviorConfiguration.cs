using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class LockableBehaviorConfiguration : IEntityTypeConfiguration<LockableBehavior>
{
    public void Configure(EntityTypeBuilder<LockableBehavior> builder)
    {
        builder.Ignore(x => x.RequiredKey);
        builder.Property<Guid?>("LockableRequiredKeyId");
    }
}
