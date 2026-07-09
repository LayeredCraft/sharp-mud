using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Behaviors;

namespace SharpMud.Persistence.Configurations;

// Equipped (Dictionary<EquipSlot, Thing?>) not persisted yet - carried items
// themselves persist fine via Thing.Children; only which slot each is worn
// in doesn't survive a restart yet. See docs/persistence.md Open Items.
public sealed class EquippedBehaviorConfiguration : IEntityTypeConfiguration<EquippedBehavior>
{
    public void Configure(EntityTypeBuilder<EquippedBehavior> builder)
    {
        builder.Ignore(x => x.Equipped);
    }
}
