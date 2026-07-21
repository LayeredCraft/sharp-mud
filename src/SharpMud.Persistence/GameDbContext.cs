using Microsoft.EntityFrameworkCore;
using SharpMud.Engine.Core;

namespace SharpMud.Persistence;

// EF Core here is used purely as "typed tables + LINQ queries," not for its
// object-graph/relationship-fixup features - every Thing/Behavior reference
// property (Parent, Children, Behaviors, ExitBehavior.Destination, ...) is
// explicitly Ignored in its IEntityTypeConfiguration and reconstructed by
// ThingRepository through the real domain APIs instead. See
// docs/persistence.md Rehydration for why.
public sealed class GameDbContext(
    DbContextOptions<GameDbContext> options,
    IEnumerable<IBehaviorMappingContributor> contributors) : DbContext(options)
{
    public DbSet<Thing> Things => Set<Thing>();
    public DbSet<Behavior> Behaviors => Set<Behavior>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in this assembly -
        // Thing, the base Behavior TPH mapping, and every engine-level
        // behavior type. Ruleset-level behaviors register via contributors
        // (SharpMud.Samples.Classic's own ApplyConfigurationsFromAssembly
        // call over its own assembly).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);

        foreach (var contributor in contributors)
            contributor.ConfigureBehaviors(modelBuilder);
    }
}
