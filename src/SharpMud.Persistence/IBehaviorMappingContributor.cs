using Microsoft.EntityFrameworkCore;

namespace SharpMud.Persistence;

// Lives here, not in SharpMud.Engine, because it takes an EF Core type
// (ModelBuilder) and Engine must never reference EF Core (docs/architecture.md).
// A ruleset registers its own EF Core mapping by referencing Persistence and
// implementing this - see docs/persistence.md "Why not per-type repositories,
// and why TPH". Engine's own behaviors are mapped directly inside
// GameDbContext, since Persistence already references Engine.
public interface IBehaviorMappingContributor
{
    void ConfigureBehaviors(ModelBuilder modelBuilder);
}
