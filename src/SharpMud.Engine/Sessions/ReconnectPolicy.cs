namespace SharpMud.Engine.Sessions;

/// <summary>Policy constants governing how long a <see cref="ConnectionState.Linkdead"/> player may reconnect.</summary>
/// <remarks>
/// Shared by <c>LinkdeadSweeper</c> and <c>CombatManager</c> (ADR-0004) - resolves
/// networking.md's open question of whether the reconnect grace window and combat's
/// linkdead grace period are the same constant: they now are.
/// </remarks>
public static class ReconnectPolicy
{
    /// <summary>
    /// How long a player may stay <see cref="ConnectionState.Linkdead"/> before being
    /// force-removed from the world. A concrete placeholder, not a tuned final value -
    /// same spirit as <c>LoginFlow.MaxPasswordAttempts</c>.
    /// </summary>
    public static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(3);
}
