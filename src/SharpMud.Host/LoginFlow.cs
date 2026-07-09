using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Host;

// Classic MUD login prompt (docs/accounts-auth.md) - only used by networked
// transports (Telnet now, SSH/WebSocket later). Local CLI stays login-free
// per SPEC.md and never calls this.
public static class LoginFlow
{
    // Not yet tuned - docs/accounts-auth.md Open Items flags the exact
    // retry/lockout policy as still undecided; this is a concrete default,
    // not a considered final answer.
    private const int MaxPasswordAttempts = 3;

    // Returns null if the connection should be dropped (empty input,
    // disconnect mid-flow) - a failed login attempt on its own loops back to
    // the username prompt rather than dropping the connection.
    public static async Task<Thing?> RunAsync(
        ISession session, World world, IThingRepository repository, Thing startingRoom, CancellationToken ct)
    {
        while (true)
        {
            await session.WriteAsync("Username: ", ct);
            var username = (await session.ReadLineAsync(ct))?.Trim();
            if (string.IsNullOrWhiteSpace(username))
                return null;

            var existing = await FindAndAttachExistingAsync(world, repository, username, startingRoom, ct);

            var player = existing is not null
                ? await LoginExistingAsync(session, existing, ct)
                : await MaybeCreateAsync(session, world, username, startingRoom, repository, ct);

            if (player is not null)
                return player;

            if (!session.IsConnected)
                return null;
        }
    }

    private static async Task<Thing?> FindAndAttachExistingAsync(
        World world, IThingRepository repository, string username, Thing startingRoom, CancellationToken ct)
    {
        var alreadyLive = world.AllWithBehavior<PlayerBehavior>()
            .FirstOrDefault(p => p.FindBehavior<PlayerBehavior>()!.Username == username);
        if (alreadyLive is not null)
            return alreadyLive;

        var loaded = await repository.FindPlayerByUsernameAsync(username, ct);
        if (loaded is null)
            return null;

        // loaded.Parent is a freshly-reconstructed standalone Thing from
        // this DB call, not the live room other players are actually in -
        // attach into the real live room instead (falls back to the hub if
        // that room no longer exists). See docs/persistence.md.
        var liveRoom = loaded.Parent is { } lastRoom ? world.GetThing(lastRoom.Id) ?? startingRoom : startingRoom;
        liveRoom.Add(loaded);
        PlayerLogin.RegisterSubtree(world, loaded);
        return loaded;
    }

    private static async Task<Thing?> LoginExistingAsync(ISession session, Thing existing, CancellationToken ct)
    {
        var playerBehavior = existing.FindBehavior<PlayerBehavior>()!;

        for (var attempt = 1; attempt <= MaxPasswordAttempts; attempt++)
        {
            await session.WriteAsync("Password: ", ct);
            await session.SetEchoAsync(false, ct);
            var password = await session.ReadLineAsync(ct);
            await session.SetEchoAsync(true, ct);
            await session.WriteLineAsync(string.Empty, ct); // newline the client's own echo-off didn't provide

            if (password is null)
                return null;

            if (!PasswordHashing.Verify(playerBehavior.PasswordHash, password))
            {
                // Generic message regardless of what went wrong - don't
                // reveal whether the username existed, per docs/accounts-auth.md.
                await session.WriteLineAsync("Login incorrect.", ct);
                continue;
            }

            if (playerBehavior.Session is { IsConnected: true })
            {
                await session.WriteLineAsync("That character is already logged in.", ct);
                return null;
            }

            return existing;
        }

        return null;
    }

    private static async Task<Thing?> MaybeCreateAsync(
        ISession session, World world, string username, Thing startingRoom, IThingRepository repository, CancellationToken ct)
    {
        await session.WriteAsync("Create a new character? (y/n) ", ct);
        var confirm = (await session.ReadLineAsync(ct))?.Trim();
        if (!string.Equals(confirm, "y", StringComparison.OrdinalIgnoreCase))
            return null;

        await session.WriteAsync("Password: ", ct);
        await session.SetEchoAsync(false, ct);
        var password = await session.ReadLineAsync(ct);
        await session.WriteAsync("\r\nConfirm password: ", ct);
        var confirmPassword = await session.ReadLineAsync(ct);
        await session.SetEchoAsync(true, ct);
        await session.WriteLineAsync(string.Empty, ct);

        if (string.IsNullOrEmpty(password) || password != confirmPassword)
        {
            await session.WriteLineAsync("Passwords didn't match.", ct);
            return null;
        }

        var player = HubWorldBuilder.CreatePlayer(world, username, PasswordHashing.Hash(password), startingRoom);

        // Persist immediately, not just on the eventual disconnect-triggered
        // save - a crash before this player's first disconnect shouldn't
        // lose a freshly created login.
        await repository.SaveTreeAsync(player, ct);

        return player;
    }
}
