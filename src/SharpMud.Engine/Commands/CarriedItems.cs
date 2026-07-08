using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands;

// Worn items are still Children of the wearer (see EquippedBehavior) - this
// filters an actor's Children down to items that are carried but not worn,
// which is what get/drop/give/inventory actually mean by "carrying."
public static class CarriedItems
{
    public static IEnumerable<Thing> Of(Thing actor)
    {
        var equipped = actor.FindBehavior<EquippedBehavior>()?.Equipped.Values
            .Where(t => t is not null)
            .Select(t => t!)
            .ToHashSet() ?? [];

        return actor.Children.Where(c => c.HasBehavior<ItemBehavior>() && !equipped.Contains(c));
    }
}
