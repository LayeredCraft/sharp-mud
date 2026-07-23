using Microsoft.EntityFrameworkCore;
using SharpMud.Persistence;

namespace SharpMud.Ruleset.Basic;

// Registers this package's own EF Core mapping for BasicStatsBehavior -
// scoped to this assembly's own IEntityTypeConfiguration<> types, same
// pattern as RpgBehaviorMappingContributor/ClassicBehaviorMappingContributor.
// Without this, a Basic world/player carrying BasicStatsBehavior hits the
// same unmapped TPH discriminator subtype problem CombatantBehavior would
// have without RpgBehaviorMappingContributor.
public sealed class BasicBehaviorMappingContributor : IBehaviorMappingContributor
{
    public void ConfigureBehaviors(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BasicBehaviorMappingContributor).Assembly);
}
