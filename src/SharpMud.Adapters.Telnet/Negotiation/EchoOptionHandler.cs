namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>RFC 857 (ECHO). WILL/WONT is the entire protocol - it never subnegotiates.</summary>
internal sealed class EchoOptionHandler : ITelnetOptionHandler
{
    public TelnetOptionCode Code => TelnetOptionCode.Echo;

    public ValueTask OnSubnegotiationAsync(ReadOnlyMemory<byte> payload, CancellationToken ct) =>
        ValueTask.CompletedTask;
}
