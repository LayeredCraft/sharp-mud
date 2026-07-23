using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Commands.Builtin.Admin;

// Shared by the moderation commands below - target lookup and "is this
// player actually online" both need the exact same combined check
// LoginFlow.LoginExistingAsync already established (ConnectionState alone
// isn't enough - it's Ignore'd by PlayerBehaviorConfiguration, so it
// defaults back to Playing on any freshly-repository-loaded PlayerBehavior,
// including one sitting in World with no live session at all).
internal static class AdminCommandHelpers
{
    /// <summary>Finds a player by username, live in the world first, then falling back to the repository (offline). Never attaches an offline result into the world tree.</summary>
    public static async Task<Thing?> FindTargetAsync(IWorld world, IThingRepository repository, string username, CancellationToken ct)
    {
        var live = world.AllWithBehavior<PlayerBehavior>()
            .FirstOrDefault(p => string.Equals(p.FindBehavior<PlayerBehavior>()!.Username, username, StringComparison.OrdinalIgnoreCase));
        if (live is not null)
            return live;

        return await repository.FindPlayerByUsernameAsync(username, ct);
    }

    /// <summary>Whether a player Thing is currently online - both <see cref="Sessions.ConnectionState.Playing"/> and a connected <see cref="ISession"/>, not either alone.</summary>
    public static bool IsOnline(Thing player)
    {
        var behavior = player.FindBehavior<PlayerBehavior>();
        return behavior is { ConnectionState: var state, Session: { IsConnected: true } } && state == ConnectionState.Playing;
    }

    /// <summary>
    /// Parses an individually-grantable role name - rejects <see
    /// cref="SecurityRole.All"/> (every current and future flag, not a
    /// real assignable tier) and <see cref="SecurityRole.None"/> (a
    /// meaningless no-op), even though a plain <c>Enum.TryParse</c> would
    /// accept both since they're literally named enum members.
    /// </summary>
    public static bool TryParseGrantableRole(string roleName, out SecurityRole role)
    {
        if (Enum.TryParse(roleName, ignoreCase: true, out SecurityRole parsed)
            && parsed is not (SecurityRole.None or SecurityRole.All)
            && Enum.IsDefined(parsed))
        {
            role = parsed;
            return true;
        }

        role = SecurityRole.None;
        return false;
    }
}
