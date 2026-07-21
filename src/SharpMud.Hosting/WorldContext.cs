using SharpMud.Engine.Core;

namespace SharpMud.Hosting;

// Holds the loaded-or-built World/StartingRoom/Root once
// WorldLoaderHostedService's StartAsync has run - registered as a
// singleton and populated exactly once during startup, before any
// transport BackgroundService begins accepting connections (hosted
// services' StartAsync calls run sequentially in registration order, and
// WorldLoaderHostedService registers first - see
// ServiceCollectionExtensions.AddSharpMudHosting). Other services take
// this via constructor injection instead of World/Thing directly, since
// those values don't exist yet at DI-container-build time.
public sealed class WorldContext
{
    private World? _world;
    private Thing? _startingRoom;
    private Thing? _root;

    // Lets ShutdownSaveHostedService skip the shutdown-time save safely if
    // the process is stopping before the world ever finished loading
    // (e.g. a crash during startup) - saving a null world would just throw
    // a second, more confusing exception on top of whatever caused the
    // early shutdown.
    public bool IsInitialized => _world is not null;

    public World World => _world
        ?? throw new InvalidOperationException($"{nameof(World)} is not available yet - it's only set after {nameof(WorldLoaderHostedService)} has started.");

    public Thing StartingRoom => _startingRoom
        ?? throw new InvalidOperationException($"{nameof(StartingRoom)} is not available yet - it's only set after {nameof(WorldLoaderHostedService)} has started.");

    public Thing Root => _root
        ?? throw new InvalidOperationException($"{nameof(Root)} is not available yet - it's only set after {nameof(WorldLoaderHostedService)} has started.");

    internal void Initialize(World world, Thing startingRoom, Thing root)
    {
        _world = world;
        _startingRoom = startingRoom;
        _root = root;
    }
}
