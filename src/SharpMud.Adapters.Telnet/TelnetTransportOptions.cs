namespace SharpMud.Adapters.Telnet;

// Registered as a singleton by AddSharpMudTelnetTransport - the port a
// BackgroundService (DI-constructed, no direct call-time arguments) needs
// isn't otherwise reachable from TelnetTransportBackgroundService's
// constructor.
internal sealed record TelnetTransportOptions(int Port);
