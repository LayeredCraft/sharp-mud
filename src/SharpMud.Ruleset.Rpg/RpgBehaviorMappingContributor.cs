using Microsoft.EntityFrameworkCore;
using SharpMud.Persistence;

namespace SharpMud.Ruleset.Rpg;

// Registers this package's own EF Core mapping for CombatantBehavior -
// Persistence never references SharpMud.Ruleset.Rpg directly, per
// docs/persistence.md. Scoped to this assembly's own
// IEntityTypeConfiguration<> types, same pattern as
// ClassicBehaviorMappingContributor - a consumer's own contributor scans
// its own assembly, never this one's.
public sealed class RpgBehaviorMappingContributor : IBehaviorMappingContributor
{
    public void ConfigureBehaviors(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RpgBehaviorMappingContributor).Assembly);
}
