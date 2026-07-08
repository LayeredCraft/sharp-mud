using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Behaviors;

// Identity only - no stats. Session lives here (mirrors WheelMUD's
// UserControlledBehavior) rather than in a separate world-level lookup
// table, so "is this Thing currently controlled by a connected session" is
// just Session != null.
public sealed class PlayerBehavior : Behavior
{
    public AccountId AccountId { get; init; }
    public ISession? Session { get; set; }
    public List<string> Aliases { get; } = [];
}
