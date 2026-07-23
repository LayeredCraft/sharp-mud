using Microsoft.EntityFrameworkCore;
using SharpMud.Persistence;

namespace SharpMud.Ruleset.Basic;

// Registers this package's own EF Core mapping for BasicStatsBehavior -
// scoped to this assembly's own IEntityTypeConfiguration<> types, same
// pattern as RpgBehaviorMappingContributor/ClassicBehaviorMappingContributor.
// Without this, a Basic world/player carrying BasicStatsBehavior hits the
// same unmapped TPH discriminator subtype problem CombatantBehavior would
// have without RpgBehaviorMappingContributor.
/// <summary>
/// This package's <see cref="IBehaviorMappingContributor"/> - registers <see
/// cref="BasicStatsBehavior"/>'s EF Core mapping. Registered automatically
/// by <c>AddSharpMudBasicRuleset(...)</c>; a consumer doesn't need to
/// register it themselves.
/// </summary>
public sealed class BasicBehaviorMappingContributor : IBehaviorMappingContributor
{
    /// <summary>Applies every <c>IEntityTypeConfiguration&lt;T&gt;</c> in this assembly - currently just <see cref="BasicStatsBehavior"/>'s.</summary>
    public void ConfigureBehaviors(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BasicBehaviorMappingContributor).Assembly);
}
