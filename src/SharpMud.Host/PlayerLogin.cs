using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Host;

// Local CLI only - reconnects a persisted character if one exists for this
// name, otherwise creates one fresh, with no password check at all. CLI
// stays login-free per SPEC.md; Telnet's real username/password prompt is
// LoginFlow, not this (docs/accounts-auth.md).
public static class PlayerLogin
{
    // Never actually checked (CLI has no login) - a fixed placeholder so
    // PlayerBehavior.PasswordHash (required) has something valid-shaped in
    // it, rather than leaving a local dev character's hash empty/guessable.
    private static readonly string LocalCliPasswordHash = PasswordHashing.Hash(Guid.NewGuid().ToString());

    public static async Task<Thing> ResolveOrCreateAsync(
        World world, IThingRepository repository, string name, Thing startingRoom, CancellationToken ct)
    {
        var alreadyOnline = world.AllWithBehavior<PlayerBehavior>().FirstOrDefault(p => p.Name == name);
        if (alreadyOnline is not null)
            return alreadyOnline;

        var loaded = await repository.FindPlayerByUsernameAsync(name, ct);
        if (loaded is null)
            return HubWorldBuilder.CreatePlayer(world, name, LocalCliPasswordHash, startingRoom);

        // loaded.Parent is a freshly-reconstructed standalone Thing from
        // this DB call, not the live room other players are actually in -
        // attach into the real live room instead (falls back to the hub if
        // that room no longer exists).
        var liveRoom = loaded.Parent is { } lastRoom ? world.GetThing(lastRoom.Id) ?? startingRoom : startingRoom;
        liveRoom.Add(loaded);
        RegisterSubtree(world, loaded);
        return loaded;
    }

    public static void RegisterSubtree(World world, Thing thing)
    {
        world.Register(thing);
        foreach (var child in thing.Children)
            RegisterSubtree(world, child);
    }
}
