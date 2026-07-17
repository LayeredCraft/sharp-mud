namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>
/// The RFC 1143 ("Q-Method") four-state variable, tracked independently
/// for both our side and the remote side of a telnet option. Prevents
/// infinite negotiation loops when both sides re-request the same option
/// while a negotiation is already in flight - see ADR-0002
/// (docs/adr/0002-telnet-protocol-negotiation.md).
/// </summary>
internal enum OptionSide
{
    No,
    WantNo,
    Yes,
    WantYes,
}
