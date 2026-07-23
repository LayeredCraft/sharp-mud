using Microsoft.EntityFrameworkCore;
using SharpMud.Persistence;

namespace SharpMud.Samples.Classic;

// Registers this ruleset's own EF Core mapping for its own behavior types
// (StatsBehavior) - Persistence never references
// SharpMud.Samples.Classic directly, per docs/persistence.md.
// CombatantBehavior's mapping moved to SharpMud.Ruleset.Rpg's own
// RpgBehaviorMappingContributor - see docs/adr/0008-ruleset-scaffolding-tier.md.
public sealed class ClassicBehaviorMappingContributor : IBehaviorMappingContributor
{
    public void ConfigureBehaviors(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClassicBehaviorMappingContributor).Assembly);
}
