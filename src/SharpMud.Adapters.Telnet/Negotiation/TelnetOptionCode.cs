namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>
/// Recognized telnet option codes. Codes intentionally not yet included -
/// SuppressGoAhead (3), TermType (24), MCCP2 (86), MXP (91) - are deferred
/// per ADR-0002 (docs/adr/0002-telnet-protocol-negotiation.md); adding a
/// future option is "add a value here plus a handler class," not a
/// redesign of <see cref="TelnetOptionNegotiator"/>.
/// </summary>
internal enum TelnetOptionCode : byte
{
    /// <summary>RFC 857.</summary>
    Echo = 1,

    /// <summary>RFC 1073 - negotiates the client's terminal window size.</summary>
    Naws = 31,
}
