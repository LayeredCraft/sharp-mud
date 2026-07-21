using Microsoft.EntityFrameworkCore;
using SharpMud.Persistence;

namespace SharpMud.Samples.Classic;

// Registers this ruleset's own EF Core mapping for its behavior types
// (StatsBehavior, CombatantBehavior) - Persistence never references
// Ruleset.Classic directly, per docs/persistence.md.
public sealed class ClassicBehaviorMappingContributor : IBehaviorMappingContributor
{
    public void ConfigureBehaviors(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClassicBehaviorMappingContributor).Assembly);
}
