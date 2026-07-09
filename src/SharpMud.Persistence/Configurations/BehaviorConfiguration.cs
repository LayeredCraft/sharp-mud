using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Core;

namespace SharpMud.Persistence.Configurations;

// Base TPH mapping - concrete Behavior subtypes each get their own
// IEntityTypeConfiguration<T> (empty ones still register the type with EF
// Core for TPH discriminator purposes, since Behavior.Parent is Ignored and
// can't be used for automatic discovery).
public sealed class BehaviorConfiguration : IEntityTypeConfiguration<Behavior>
{
    public void Configure(EntityTypeBuilder<Behavior> builder)
    {
        builder.ToTable("Behaviors");
        builder.HasKey(x => x.PersistenceKey);
        builder.Property<Guid>("ThingId").IsRequired(); // shadow FK, owning Thing
        builder.Ignore(x => x.Parent);
        builder.HasDiscriminator<string>("BehaviorType");
    }
}
