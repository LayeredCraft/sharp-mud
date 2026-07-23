using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Behaviors;

// Identity: Username/PasswordHash (docs/accounts-auth.md - revised from an
// external OAuth + separate Account entity design; one character per login
// now, no "alts"). Session lives here too (mirrors WheelMUD's
// UserControlledBehavior) rather than a separate world-level lookup table,
// so "is this Thing currently controlled by a connected session" is just
// Session != null.
public sealed class PlayerBehavior : Behavior
{
    public required string Username { get; init; }
    public required string PasswordHash { get; set; }
    public ISession? Session { get; set; }
    public List<string> Aliases { get; } = [];

    /// <summary>
    /// This player's connection lifecycle state. Runtime-only, like <see cref="Session"/>
    /// (<c>Ignore</c>'d in <c>PlayerBehaviorConfiguration</c>) - ADR-0004. Only mutated via
    /// <see cref="EnterLinkdead"/>/<see cref="Reconnect"/>, never set directly.
    /// </summary>
    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Playing;

    /// <summary>
    /// When this player entered <see cref="Sessions.ConnectionState.Linkdead"/>, or
    /// <see langword="null"/> while <see cref="Sessions.ConnectionState.Playing"/>.
    /// </summary>
    public DateTimeOffset? LinkdeadSinceUtc { get; private set; }

    /// <summary>
    /// This player's granted access levels - persisted (unlike <see
    /// cref="ConnectionState"/>, a role assignment must survive a restart).
    /// Defaults to <see cref="SecurityRole.Player"/> for new characters.
    /// Only mutated via <see cref="GrantRole"/>/<see cref="RevokeRole"/> -
    /// ADR-0005.
    /// </summary>
    public SecurityRole Roles { get; private set; } = SecurityRole.Player;

    /// <summary>Whether this player's <c>say</c>/<c>emote</c> are currently blocked - persisted. Only mutated via <see cref="Mute"/>/<see cref="Unmute"/>.</summary>
    public bool IsMuted { get; private set; }

    /// <summary>Whether this player is blocked from logging in - persisted, enforced in <c>LoginFlow</c>. Only mutated via <see cref="Ban"/>/<see cref="Unban"/>.</summary>
    public bool IsBanned { get; private set; }

    /// <summary>
    /// Whether this player's session was just forcibly disconnected by
    /// <c>BootCommand</c> (or <c>BanCommand</c> disconnecting an online
    /// target). Transient, like <see cref="Session"/>/<see
    /// cref="ConnectionState"/> (<c>Ignore</c>'d in
    /// <c>PlayerBehaviorConfiguration</c>) - it only needs to survive long
    /// enough for this same connection's <c>SessionLoop.RunAsync</c> to see
    /// it in its own <c>finally</c> block, crossing from the admin's call
    /// stack to the target's. Without this, an admin-triggered disconnect
    /// looks identical to a dropped connection, so the target would just
    /// resume via the normal <see cref="Sessions.ConnectionState.Linkdead"/>
    /// reconnect path (ADR-0004) - making <c>boot</c> a no-op as a
    /// moderation tool. Only set via <see cref="MarkBooted"/>.
    /// </summary>
    public bool WasBooted { get; private set; }

    /// <summary>
    /// Transitions this player to <see cref="Sessions.ConnectionState.Linkdead"/> - called by
    /// <c>SessionLoop</c> when a connection is lost (not an explicit <c>quit</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Already <see cref="Sessions.ConnectionState.Linkdead"/>.</exception>
    public void EnterLinkdead(DateTimeOffset now)
    {
        if (!ConnectionState.CanTransitionTo(ConnectionState.Linkdead))
            throw new InvalidOperationException($"Cannot transition from {ConnectionState.Name} to Linkdead.");

        ConnectionState = ConnectionState.Linkdead;
        LinkdeadSinceUtc = now;
    }

    /// <summary>
    /// Transitions this player back to <see cref="Sessions.ConnectionState.Playing"/> and
    /// clears <see cref="LinkdeadSinceUtc"/> - called by <c>LoginFlow</c> when a
    /// <see cref="Sessions.ConnectionState.Linkdead"/> player reconnects within the grace window.
    /// </summary>
    /// <exception cref="InvalidOperationException">Already <see cref="Sessions.ConnectionState.Playing"/>.</exception>
    public void Reconnect()
    {
        if (!ConnectionState.CanTransitionTo(ConnectionState.Playing))
            throw new InvalidOperationException($"Cannot transition from {ConnectionState.Name} to Playing.");

        ConnectionState = ConnectionState.Playing;
        LinkdeadSinceUtc = null;
    }

    /// <summary>
    /// Marks this player as having just been forcibly disconnected - called
    /// by <c>BootCommand</c>/<c>BanCommand</c> before disconnecting the
    /// target's session. See <see cref="WasBooted"/>.
    /// </summary>
    public void MarkBooted() => WasBooted = true;

    /// <summary>
    /// Grants <paramref name="role"/> and every role it implies (e.g.
    /// granting <see cref="SecurityRole.FullAdmin"/> also grants <see
    /// cref="SecurityRole.MinorAdmin"/> and <see cref="SecurityRole.Player"/>)
    /// - ADR-0005's accumulation rule. Idempotent.
    /// </summary>
    public void GrantRole(SecurityRole role) => Roles |= role.ImpliedRoles;

    /// <summary>
    /// Revokes <paramref name="role"/>, unless some other role this player
    /// currently holds implies it (e.g. revoking <see
    /// cref="SecurityRole.MinorAdmin"/> while still holding <see
    /// cref="SecurityRole.FullAdmin"/> would otherwise leave <see
    /// cref="SecurityRole.FullAdmin"/> set with a role it implies cleared).
    /// Returns <see langword="null"/> on success, or a message naming the
    /// blocking higher tier on failure - a normal, directly
    /// user-triggerable business-rule outcome per
    /// coding-standards.md's Error Handling section, not a thrown
    /// exception.
    /// </summary>
    public string? RevokeRole(SecurityRole role)
    {
        foreach (var candidate in RolesWithImplications)
        {
            if (candidate == role)
                continue;

            if ((Roles & candidate) == candidate && candidate.Implies(role))
                return $"Still has {candidate}, which includes {role} - revoke {candidate} instead.";
        }

        Roles &= ~role;
        return null;
    }

    /// <summary>Blocks this player's <c>say</c>/<c>emote</c>. Idempotent.</summary>
    public void Mute() => IsMuted = true;

    /// <summary>Restores this player's <c>say</c>/<c>emote</c>. Idempotent.</summary>
    public void Unmute() => IsMuted = false;

    /// <summary>Blocks this player from logging in. Idempotent.</summary>
    public void Ban() => IsBanned = true;

    /// <summary>Restores this player's ability to log in. Idempotent.</summary>
    public void Unban() => IsBanned = false;

    // The only roles with an "implies" relationship to another role - see
    // SecurityRoleExtensions.ImpliedRoles. RevokeRole only needs to check
    // these four, not every SecurityRole member.
    private static readonly SecurityRole[] RolesWithImplications =
        [SecurityRole.FullAdmin, SecurityRole.MinorAdmin, SecurityRole.FullBuilder, SecurityRole.MinorBuilder];
}
