using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class ExitBehaviorConfiguration : IEntityTypeConfiguration<ExitBehavior>
{
    public void Configure(EntityTypeBuilder<ExitBehavior> builder)
    {
        builder.Ignore(x => x.Destination);
        builder.Property<Guid>("ExitDestinationId").IsRequired();
    }
}
