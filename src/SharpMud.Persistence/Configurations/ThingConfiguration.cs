using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Core;

namespace SharpMud.Persistence.Configurations;

// Every Thing/Behavior reference property (Parent, Children, Behaviors,
// ExitBehavior.Destination, ...) is Ignored here and reconstructed by
// ThingRepository through the real domain APIs instead - see
// docs/persistence.md Rehydration for why.
public sealed class ThingConfiguration : IEntityTypeConfiguration<Thing>
{
    public void Configure(EntityTypeBuilder<Thing> builder)
    {
        builder.ToTable("Things");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new ThingId(value));
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property<Guid?>("ParentId"); // shadow self-FK, no EF relationship

        builder.Ignore(x => x.Parent);
        builder.Ignore(x => x.Children);
        builder.Ignore(x => x.Parents);
        builder.Ignore(x => x.Behaviors);
        builder.Ignore(x => x.Events);
    }
}
