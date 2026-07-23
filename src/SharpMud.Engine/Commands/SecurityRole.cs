namespace SharpMud.Engine.Commands;

/// <summary>
/// Access-level flags checked by <see cref="RoleGuardedCommand"/> (bitwise
/// AND, any-of semantics) - ported in full from WheelMUD's own
/// <c>SecurityRole</c> (see docs/research/wheelmud-findings.md §11), even
/// though several values (<see cref="Mobile"/>/<see cref="Item"/>/<see
/// cref="Room"/>/<see cref="Married"/>/<see cref="TutorialPlayer"/>) have no
/// consumer in sharp-mud yet - see ADR-0005's Decision Outcome for why.
/// </summary>
/// <remarks>
/// Explicit power-of-two values on every member are load-bearing, not
/// stylistic - C#'s default sequential auto-numbering (<c>0, 1, 2, 3...</c>)
/// would not produce distinct bits (e.g. <see cref="Room"/> would silently
/// equal <see cref="Mobile"/> | <see cref="Item"/> combined), breaking
/// <see cref="RoleGuardedCommand"/>'s bitwise-AND check into granting
/// unrelated permissions on overlapping bits. <see cref="All"/> is defined
/// as the union of every individual flag, not a separate hardcoded value,
/// so it can't drift out of sync if a flag is added later.
/// </remarks>
[Flags]
public enum SecurityRole : uint
{
    /// <summary>No roles.</summary>
    None = 0,

    /// <summary>A non-player command issuer (e.g. an NPC). No consumer yet.</summary>
    Mobile = 1 << 0,

    /// <summary>An item-driven command issuer. No consumer yet.</summary>
    Item = 1 << 1,

    /// <summary>A room-driven command issuer. No consumer yet.</summary>
    Room = 1 << 2,

    /// <summary>A player still in a tutorial/onboarding flow. No consumer yet.</summary>
    TutorialPlayer = 1 << 3,

    /// <summary>An ordinary player - the default role every new character starts with.</summary>
    Player = 1 << 4,

    /// <summary>A player granted helper-tier privileges. No consumer yet.</summary>
    Helper = 1 << 5,

    /// <summary>A player in a married-couple relationship. No consumer yet (no marriage system).</summary>
    Married = 1 << 6,

    /// <summary>Limited world-building access (Slice 4).</summary>
    MinorBuilder = 1 << 7,

    /// <summary>Full world-building access (Slice 4). Implies <see cref="MinorBuilder"/>.</summary>
    FullBuilder = 1 << 8,

    /// <summary>Day-to-day moderation: <c>boot</c>/<c>mute</c>/<c>unmute</c>/<c>announce</c>.</summary>
    MinorAdmin = 1 << 9,

    /// <summary>
    /// Full administration: everything <see cref="MinorAdmin"/> can do, plus
    /// <c>ban</c>/<c>unban</c>/<c>rolegrant</c>/<c>rolerevoke</c>. Implies
    /// <see cref="MinorAdmin"/> and <see cref="Player"/>.
    /// </summary>
    FullAdmin = 1 << 10,

    /// <summary>The union of every individual role above.</summary>
    All = Mobile | Item | Room | TutorialPlayer | Player | Helper | Married | MinorBuilder | FullBuilder | MinorAdmin | FullAdmin,
}
