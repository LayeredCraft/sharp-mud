using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Ticking;

namespace SharpMud.Engine.Behaviors;

// Registered once with IGameLoop (mirrors CombatManager in
// SharpMud.Samples.Classic) rather than one ITickable per wandering NPC -
// simpler Host wiring, and it keeps Behaviors free of a dependency on
// IGameLoop for self-registration.
public sealed class WanderManager(IWorld world, IRandomSource random) : ITickable
{
    public async Task OnTickAsync(TickContext ctx, CancellationToken ct)
    {
        foreach (var npc in world.AllWithBehavior<WanderingBehavior>().ToArray())
        {
            var wandering = npc.FindBehavior<WanderingBehavior>()!;
            if (random.Next(1, 100) > wandering.WanderChancePercent)
                continue;

            var origin = npc.Parent;
            if (origin is null)
                continue;

            var exits = origin.Children
                .Select(c => c.FindBehavior<ExitBehavior>())
                .OfType<ExitBehavior>()
                .ToList();

            if (exits.Count == 0)
                continue;

            var exit = exits[random.Next(0, exits.Count - 1)];
            var destination = exit.Destination;

            if (!origin.Remove(npc))
                continue;

            await RoomBroadcast.ToOccupantsAsync(origin, npc, $"{npc.Name} leaves {exit.Direction.ToDisplayString()}.", ct);

            destination.Add(npc);

            await RoomBroadcast.ToOccupantsAsync(destination, npc, $"{npc.Name} arrives.", ct);
        }
    }
}
