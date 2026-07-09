using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Host;

// Shared by the CLI and Telnet paths (docs/networking.md) - reconnect a
// persisted character if one exists for this name, otherwise create fresh.
// "Name" here is a placeholder for real login (docs/accounts-auth.md) - no
// password check yet.
public static class PlayerLogin
{
    public static async Task<Thing> ResolveOrCreateAsync(
        World world, IThingRepository repository, string name, Thing startingRoom, CancellationToken ct)
    {
        var alreadyOnline = world.AllWithBehavior<PlayerBehavior>().FirstOrDefault(p => p.Name == name);
        if (alreadyOnline is not null)
            return alreadyOnline;

        var loaded = await repository.FindPlayerByNameAsync(name, ct);
        if (loaded is null)
            return HubWorldBuilder.CreatePlayer(world, name, startingRoom);

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
