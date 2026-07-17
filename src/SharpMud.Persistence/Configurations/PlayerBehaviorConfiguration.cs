using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

public sealed class PlayerBehaviorConfiguration : IEntityTypeConfiguration<PlayerBehavior>
{
    public void Configure(EntityTypeBuilder<PlayerBehavior> builder)
    {
        builder.Property(x => x.Username).IsRequired();
        builder.Property(x => x.PasswordHash).IsRequired();

        // Enforced at the DB level, not just the application-side lookup
        // before creating a character - docs/accounts-auth.md.
        builder.HasIndex(x => x.Username).IsUnique();

        builder.Ignore(x => x.Session);   // runtime-only
        builder.Ignore(x => x.Aliases);   // not persisted yet - docs/persistence.md Open Items

        // ConnectionState/LinkdeadSinceUtc are runtime-only session-lifecycle
        // state, same category as Session - a process restart already drops
        // all live sessions, so there's nothing meaningful to persist here
        // (ADR-0004).
        builder.Ignore(x => x.ConnectionState);
        builder.Ignore(x => x.LinkdeadSinceUtc);
    }
}
