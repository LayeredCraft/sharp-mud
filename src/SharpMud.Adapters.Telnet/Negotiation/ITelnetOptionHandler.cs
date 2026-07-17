namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>A single telnet option's subnegotiation payload handling.</summary>
internal interface ITelnetOptionHandler
{
    TelnetOptionCode Code { get; }

    /// <summary>
    /// Called with the raw bytes between <c>IAC SB &lt;code&gt;</c> and
    /// <c>IAC SE</c>. Telnet is an untrusted input surface (see
    /// .agents/skills/engineering-workflow/references/security.md) -
    /// implementations must tolerate malformed/short payloads rather than
    /// throwing.
    /// </summary>
    ValueTask OnSubnegotiationAsync(ReadOnlyMemory<byte> payload, CancellationToken ct);
}
