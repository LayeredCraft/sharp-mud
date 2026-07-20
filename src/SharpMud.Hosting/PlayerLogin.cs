using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Hosting;

// Local CLI only - reconnects a persisted character if one exists for this
// name, otherwise creates one fresh, with no password check at all. CLI
// stays login-free per SPEC.md; Telnet's real username/password prompt is
// LoginFlow, not this (docs/accounts-auth.md).
public sealed class PlayerLogin
{
    // Never actually checked (CLI has no login) - a fixed placeholder so
    // PlayerBehavior.PasswordHash (required) has something valid-shaped in
    // it, rather than leaving a local dev character's hash empty/guessable.
    private static readonly string LocalCliPasswordHash = PasswordHashing.Hash(Guid.NewGuid().ToString());

    private readonly WorldContext _worldContext;
    private readonly IThingRepository _repository;
    private readonly IPlayerFactory _playerFactory;

    public PlayerLogin(WorldContext worldContext, IThingRepository repository, IPlayerFactory playerFactory)
    {
        _worldContext = worldContext;
        _repository = repository;
        _playerFactory = playerFactory;
    }

    public static void RegisterSubtree(World world, Thing thing)
    {
        world.Register(thing);
        foreach (var child in thing.Children)
            RegisterSubtree(world, child);
    }

    public async Task<Thing> ResolveOrCreateAsync(string name, CancellationToken ct)
    {
        var world = _worldContext.World;

        var alreadyOnline = world.AllWithBehavior<PlayerBehavior>().FirstOrDefault(p => p.Name == name);
        if (alreadyOnline is not null)
            return alreadyOnline;

        var loaded = await _repository.FindPlayerByUsernameAsync(name, ct);
        if (loaded is null)
            return _playerFactory.CreatePlayer(world, name, LocalCliPasswordHash, _worldContext.StartingRoom);

        // loaded.Parent is a freshly-reconstructed standalone Thing from
        // this DB call, not the live room other players are actually in -
        // attach into the real live room instead (falls back to the hub if
        // that room no longer exists).
        var liveRoom = loaded.Parent is { } lastRoom ? world.GetThing(lastRoom.Id) ?? _worldContext.StartingRoom : _worldContext.StartingRoom;
        liveRoom.Add(loaded);
        RegisterSubtree(world, loaded);
        return loaded;
    }
}
