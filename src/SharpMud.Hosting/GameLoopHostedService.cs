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
    private readonly IEnumerable<ITickable> _tickables;

    public GameLoopHostedService(IGameLoop gameLoop, IEnumerable<ITickable> tickables)
    {
        _gameLoop = gameLoop;
        _tickables = tickables;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var tickable in _tickables)
            _gameLoop.Register(tickable);

        return _gameLoop.RunAsync(stoppingToken);
    }
}
