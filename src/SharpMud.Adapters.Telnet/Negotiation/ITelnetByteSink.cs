namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>
/// Lets <see cref="TelnetOptionNegotiator"/> write raw bytes without
/// depending on <see cref="System.Net.Sockets.NetworkStream"/> directly -
/// <see cref="TelnetSession"/> implements this over its real stream, tests
/// can substitute a fake that records written bytes.
/// </summary>
internal interface ITelnetByteSink
{
    ValueTask WriteAsync(byte[] bytes, CancellationToken ct);
}
