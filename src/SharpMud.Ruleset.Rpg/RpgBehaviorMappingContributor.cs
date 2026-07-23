using Microsoft.EntityFrameworkCore;
using SharpMud.Persistence;

namespace SharpMud.Ruleset.Rpg;

// Registers this package's own EF Core mapping for CombatantBehavior -
// Persistence never references SharpMud.Ruleset.Rpg directly, per
// docs/persistence.md. Scoped to this assembly's own
// IEntityTypeConfiguration<> types, same pattern as
// ClassicBehaviorMappingContributor - a consumer's own contributor scans
// its own assembly, never this one's.
/// <summary>
/// This package's <see cref="IBehaviorMappingContributor"/> - registers <see
/// cref="CombatantBehavior"/>'s EF Core mapping. Registered automatically by
/// <c>AddSharpMudRpgRuleset(...)</c>; a consumer doesn't need to register it
/// themselves.
/// </summary>
public sealed class RpgBehaviorMappingContributor : IBehaviorMappingContributor
{
    /// <summary>Applies every <c>IEntityTypeConfiguration&lt;T&gt;</c> in this assembly - currently just <see cref="CombatantBehavior"/>'s.</summary>
    public void ConfigureBehaviors(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RpgBehaviorMappingContributor).Assembly);
}
