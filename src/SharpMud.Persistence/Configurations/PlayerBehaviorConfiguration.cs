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

        // Roles/IsMuted/IsBanned (ADR-0005) - unlike ConnectionState, these
        // must survive a restart or they're useless. Roles is a plain
        // [Flags] enum - default int mapping, same as WearableBehavior.Slot,
        // no custom value converter needed.
        builder.Property(x => x.Roles);
        builder.Property(x => x.IsMuted);
        builder.Property(x => x.IsBanned);

        // WasBooted is transient, same category as Session/ConnectionState
        // above - it only needs to survive within one already-live process,
        // never across a restart (see PlayerBehavior.WasBooted's doc comment).
        builder.Ignore(x => x.WasBooted);
    }
}
