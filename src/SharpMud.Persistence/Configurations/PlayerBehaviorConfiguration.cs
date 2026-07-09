using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class PlayerBehaviorConfiguration : IEntityTypeConfiguration<PlayerBehavior>
{
    public void Configure(EntityTypeBuilder<PlayerBehavior> builder)
    {
        builder.Ignore(x => x.AccountId); // vestigial, see docs/engine-vs-ruleset.md
        builder.Ignore(x => x.Session);   // runtime-only
        builder.Ignore(x => x.Aliases);   // not persisted yet - docs/persistence.md Open Items
    }
}
