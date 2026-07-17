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

    // Runtime-only, like Session (Ignore'd in PlayerBehaviorConfiguration) -
    // ADR-0004. Playing<->Linkdead only; SessionLoop transitions to Linkdead
    // on disconnect instead of immediately tearing the Thing down, LoginFlow
    // transitions back to Playing on a successful reconnect within
    // ReconnectPolicy.GraceWindow.
    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Playing;
    public DateTimeOffset? LinkdeadSinceUtc { get; private set; }

    public void EnterLinkdead(DateTimeOffset now)
    {
        if (!ConnectionState.CanTransitionTo(ConnectionState.Linkdead))
            throw new InvalidOperationException($"Cannot transition from {ConnectionState.Name} to Linkdead.");

        ConnectionState = ConnectionState.Linkdead;
        LinkdeadSinceUtc = now;
    }

    public void Reconnect()
    {
        if (!ConnectionState.CanTransitionTo(ConnectionState.Playing))
            throw new InvalidOperationException($"Cannot transition from {ConnectionState.Name} to Playing.");

        ConnectionState = ConnectionState.Playing;
        LinkdeadSinceUtc = null;
    }
}
