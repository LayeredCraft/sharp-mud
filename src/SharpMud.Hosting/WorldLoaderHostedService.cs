using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpMud.Engine.Core;

namespace SharpMud.Hosting;

// Loads a persisted world, or builds and saves a fresh one - the generic
// half of what today's Program.cs does inline, parameterized by a
// consumer's IWorldBuilder instead of hardcoding HubWorldBuilder. Must be
// registered before GameLoopHostedService and any transport
// BackgroundService (see ServiceCollectionExtensions.AddSharpMudHosting) -
// the generic host awaits each IHostedService's StartAsync in registration
// order, so this fully populates WorldContext before anything that needs
// World/StartingRoom starts running.
internal sealed class WorldLoaderHostedService : IHostedService
{
    private readonly IThingRepository _repository;
    private readonly IWorldBuilder _worldBuilder;
    private readonly WorldContext _worldContext;
    private readonly IEnumerable<IStorageInitializer> _storageInitializers;
    private readonly ILogger<WorldLoaderHostedService> _logger;

    public WorldLoaderHostedService(
        IThingRepository repository,
        IWorldBuilder worldBuilder,
        WorldContext worldContext,
        IEnumerable<IStorageInitializer> storageInitializers,
        ILogger<WorldLoaderHostedService> logger)
    {
        _repository = repository;
        _worldBuilder = worldBuilder;
        _worldContext = worldContext;
        _storageInitializers = storageInitializers;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Run before the load below, regardless of hosted-service
        // registration order between the Hosting/Persistence packages -
        // see IStorageInitializer.
        foreach (var initializer in _storageInitializers)
            await initializer.InitializeAsync(cancellationToken);

        var loadedRoot = await _repository.LoadTreeAsync(_worldBuilder.RootId, cancellationToken);

        if (loadedRoot is not null)
        {
            var world = new World();
            PlayerLogin.RegisterSubtree(world, loadedRoot);
            var startingRoom = _worldBuilder.FindStartingRoom(loadedRoot);
            _worldContext.Initialize(world, startingRoom, loadedRoot);
            _logger.Information("Loaded persisted world");
            return;
        }

        var (builtWorld, builtStartingRoom) = _worldBuilder.Build();
        var root = builtWorld.GetThing(_worldBuilder.RootId)!;
        await _repository.SaveTreeAsync(root, cancellationToken);
        _worldContext.Initialize(builtWorld, builtStartingRoom, root);
        _logger.Information("No persisted world found - built and saved a fresh one");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
