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
}
