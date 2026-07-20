using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpMud.Engine.Ticking;

namespace SharpMud.Hosting;

// Runs GameLoop as a BackgroundService rather than GameLoop itself taking a
// Microsoft.Extensions.Hosting dependency - GameLoop stays engine-agnostic
// of hosting concerns, matching the no-Ruleset/no-Hosting-reference rule
// for SharpMud.Engine (docs/engine-vs-ruleset.md). Registers every
// DI-resolved ITickable at startup instead of a dedicated registration
// callback - a consumer just does
// services.AddSingleton<ITickable, MyCombatManager>() for their own
// ruleset-specific tickables (docs/adr/0006-nuget-package-distribution.md).
internal sealed class GameLoopHostedService : BackgroundService
{
    private readonly IGameLoop _gameLoop;
    private readonly IServiceProvider _serviceProvider;

    // IServiceProvider, not IEnumerable<ITickable> directly - the generic
    // host resolves every registered IHostedService (constructing all of
    // them) before calling any of their StartAsync methods, so a
    // constructor-injected IEnumerable<ITickable> would resolve
    // WanderManager/LinkdeadSweeper/a consumer's CombatManager - all of
    // which read WorldContext.World/.StartingRoom - before
    // WorldLoaderHostedService.StartAsync has actually populated it.
    // Deferring resolution to ExecuteAsync (which only runs after
    // StartAsync, sequenced after WorldLoaderHostedService per
    // registration order) avoids that. This is the legitimate
    // "resolving something at runtime" exception to the no-service-locator
    // rule in coding-standards.md.
    public GameLoopHostedService(IGameLoop gameLoop, IServiceProvider serviceProvider)
    {
        _gameLoop = gameLoop;
        _serviceProvider = serviceProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var tickable in _serviceProvider.GetServices<ITickable>())
            _gameLoop.Register(tickable);

        return _gameLoop.RunAsync(stoppingToken);
    }
}
