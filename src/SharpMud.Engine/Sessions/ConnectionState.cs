using LayeredCraft.OptimizedEnums;

namespace SharpMud.Engine.Sessions;

// LayeredCraft.OptimizedEnums as a small state machine (ADR-0004), same
// precedent as Race/CharacterClass in SharpMud.Ruleset.Classic - the legal
// transitions live on the enum itself rather than being re-checked at every
// call site that mutates PlayerBehavior.ConnectionState.
public sealed partial class ConnectionState : OptimizedEnum<ConnectionState, int>
{
    public static readonly ConnectionState Playing = new(1, nameof(Playing));
    public static readonly ConnectionState Linkdead = new(2, nameof(Linkdead));

    private ConnectionState(int value, string name) : base(value, name)
    {
    }

    public bool CanTransitionTo(ConnectionState next) =>
        (this == Playing && next == Linkdead) || (this == Linkdead && next == Playing);
}
