namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>Telnet protocol command byte values (RFC 854).</summary>
internal static class TelnetCommandByte
{
    internal const byte Se = 240;
    internal const byte Sb = 250;
    internal const byte Will = 251;
    internal const byte Wont = 252;
    internal const byte Do = 253;
    internal const byte Dont = 254;
    internal const byte Iac = 255;
}
