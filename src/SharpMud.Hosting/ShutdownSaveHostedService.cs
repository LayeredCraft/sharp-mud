using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpMud.Engine.Core;

namespace SharpMud.Hosting;

// Whole-world snapshot on graceful shutdown, replacing Program.cs's final
// repository.SaveTreeAsync(hubArea, ...) call - specifically for NPC state
// (wander position, live combat HP) that isn't tied to any player session
// and so isn't already covered by each session's own on-disconnect save
// (docs/persistence.md). A BackgroundService stopping doesn't imply a save
// happens on the way out by itself, so this needs to be explicit -
// see docs/adr/0006-nuget-package-distribution.md.
internal sealed class ShutdownSaveHostedService : IHostedService
{
    private readonly WorldContext _worldContext;
    private readonly IThingRepository _repository;
    private readonly ILogger<ShutdownSaveHostedService> _logger;

    public ShutdownSaveHostedService(WorldContext worldContext, IThingRepository repository, ILogger<ShutdownSaveHostedService> logger)
    {
        _worldContext = worldContext;
        _repository = repository;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_worldContext.IsInitialized)
            return;

        // CancellationToken.None, not the token this method receives - the
        // host passes an already-cancelled/expiring token during shutdown,
        // and using it here would risk aborting the save at exactly the
        // moment it matters most (same reasoning as SessionLoop's
        // disconnect-time save).
        await _repository.SaveTreeAsync(_worldContext.Root, CancellationToken.None);
        _logger.Information("Saved world on shutdown");
    }
}
