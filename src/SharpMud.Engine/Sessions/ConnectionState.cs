using LayeredCraft.OptimizedEnums;

namespace SharpMud.Engine.Sessions;

/// <summary>
/// A player's connection lifecycle state: <see cref="Playing"/> (actively connected) or
/// <see cref="Linkdead"/> (disconnected, awaiting reconnect within <c>ReconnectPolicy.GraceWindow</c>).
/// </summary>
/// <remarks>
/// A <c>LayeredCraft.OptimizedEnums</c> state machine (ADR-0004), same precedent as
/// <c>Race</c>/<c>CharacterClass</c> in <c>SharpMud.Ruleset.Classic</c> - the legal
/// transitions live on the enum itself via <see cref="CanTransitionTo"/> rather than being
/// re-checked at every call site that mutates <c>PlayerBehavior.ConnectionState</c>.
/// </remarks>
public sealed partial class ConnectionState : OptimizedEnum<ConnectionState, int>
{
    /// <summary>The player is actively connected and playing.</summary>
    public static readonly ConnectionState Playing = new(1, nameof(Playing));

    /// <summary>
    /// The player's connection was lost (not an explicit <c>quit</c>) and is awaiting
    /// reconnect within the grace window before being removed from the world.
    /// </summary>
    public static readonly ConnectionState Linkdead = new(2, nameof(Linkdead));

    private ConnectionState(int value, string name) : base(value, name)
    {
    }

    /// <summary>
    /// Returns whether transitioning from this state to <paramref name="next"/> is legal.
    /// Only <see cref="Playing"/>&#8594;<see cref="Linkdead"/> and
    /// <see cref="Linkdead"/>&#8594;<see cref="Playing"/> are allowed.
    /// </summary>
    public bool CanTransitionTo(ConnectionState next) =>
        (this == Playing && next == Linkdead) || (this == Linkdead && next == Playing);
}
