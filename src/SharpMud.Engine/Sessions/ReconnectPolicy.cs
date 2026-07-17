namespace SharpMud.Engine.Sessions;

// Shared by LinkdeadSweeper and CombatManager (ADR-0004) - resolves
// networking.md's open question of whether the reconnect grace window and
// combat's linkdead grace period are the same constant: they now are.
// 3 minutes is a concrete placeholder, not a tuned final value - same spirit
// as LoginFlow.MaxPasswordAttempts.
public static class ReconnectPolicy
{
    public static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(3);
}
