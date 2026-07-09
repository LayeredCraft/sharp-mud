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
}
