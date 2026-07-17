using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Ticking;

namespace SharpMud.Engine.Sessions;

// Registered once with IGameLoop (mirrors WanderManager/CombatManager)
// rather than a Timer per disconnected player - ADR-0004. SessionLoop stops
// removing a disconnected player's Thing from the world immediately; this
// sweeper is what actually finishes that removal, once ReconnectPolicy
// .GraceWindow has elapsed without a reconnect.
public sealed class LinkdeadSweeper(IWorld world, IThingRepository repository) : ITickable
{
    public async Task OnTickAsync(TickContext ctx, CancellationToken ct)
    {
        foreach (var player in world.AllWithBehavior<PlayerBehavior>().ToArray())
        {
            var playerBehavior = player.FindBehavior<PlayerBehavior>()!;
            if (playerBehavior.ConnectionState != ConnectionState.Linkdead)
                continue;

            if (ctx.Timestamp - playerBehavior.LinkdeadSinceUtc!.Value < ReconnectPolicy.GraceWindow)
                continue;

            await repository.SaveTreeAsync(player, ct);

            player.Parent?.Remove(player);
            world.Unregister(player.Id);
        }
    }
}
