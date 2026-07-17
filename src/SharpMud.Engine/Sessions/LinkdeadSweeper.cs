using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Ticking;

namespace SharpMud.Engine.Sessions;

/// <summary>
/// Force-removes and saves any player who has been <see cref="ConnectionState.Linkdead"/>
/// for longer than <see cref="ReconnectPolicy.GraceWindow"/> without reconnecting.
/// </summary>
/// <remarks>
/// Registered once with <c>IGameLoop</c> (mirrors <c>WanderManager</c>/<c>CombatManager</c>)
/// rather than a <see cref="System.Threading.Timer"/> per disconnected player (ADR-0004).
/// <c>SessionLoop</c> stops removing a disconnected player's <see cref="Thing"/> from the
/// world immediately; this sweeper is what actually finishes that removal, once
/// <see cref="ReconnectPolicy.GraceWindow"/> has elapsed without a reconnect.
/// </remarks>
public sealed class LinkdeadSweeper : ITickable
{
    private readonly IWorld _world;
    private readonly IThingRepository _repository;

    public LinkdeadSweeper(IWorld world, IThingRepository repository)
    {
        _world = world;
        _repository = repository;
    }

    /// <summary>
    /// Scans every registered player for a <see cref="ConnectionState.Linkdead"/> state
    /// past the grace window, saving and removing each one found.
    /// </summary>
    public async Task OnTickAsync(TickContext ctx, CancellationToken ct)
    {
        foreach (var player in _world.AllWithBehavior<PlayerBehavior>().ToArray())
        {
            // Every Thing returned by AllWithBehavior<PlayerBehavior>() has
            // one by construction - that's what the query filters on.
            var playerBehavior = player.FindBehavior<PlayerBehavior>()!;
            if (playerBehavior.ConnectionState != ConnectionState.Linkdead)
                continue;

            // Linkdead always sets LinkdeadSinceUtc (see PlayerBehavior.EnterLinkdead) -
            // the two fields change together, never independently.
            if (ctx.Timestamp - playerBehavior.LinkdeadSinceUtc!.Value < ReconnectPolicy.GraceWindow)
                continue;

            await _repository.SaveTreeAsync(player, ct);

            player.Parent?.Remove(player);
            _world.Unregister(player.Id);
        }
    }
}
