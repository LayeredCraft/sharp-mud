namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>
/// Mutable Q-Method negotiation state for one telnet option on one
/// connection. <see cref="UsState"/> tracks whether we've offered
/// WILL/WONT for this option; <see cref="HimState"/> tracks whether the
/// remote side has agreed to DO/DONT. The two "queued opposite" flags
/// record a one-deep "try the opposite as soon as this settles" request
/// made while a negotiation for that side is already in flight.
/// </summary>
internal sealed class TelnetOptionState
{
    public bool WantOption { get; set; }
    public OptionSide UsState { get; set; } = OptionSide.No;
    public bool UsQueuedOpposite { get; set; }
    public OptionSide HimState { get; set; } = OptionSide.No;
    public bool HimQueuedOpposite { get; set; }
}
