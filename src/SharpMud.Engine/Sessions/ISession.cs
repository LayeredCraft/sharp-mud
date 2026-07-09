namespace SharpMud.Engine.Sessions;

public interface ISession
{
    string SessionId { get; }
    bool IsConnected { get; }

    ValueTask<string?> ReadLineAsync(CancellationToken ct);
    ValueTask WriteLineAsync(string text, CancellationToken ct);
    ValueTask WriteAsync(string text, CancellationToken ct);
    ValueTask DisconnectAsync(string? reason, CancellationToken ct);

    // enabled=true: normal visible input. enabled=false: best-effort request
    // to hide subsequent input (password entry, see docs/accounts-auth.md).
    // Telnet implements this via IAC WILL/WONT ECHO; adapters that can't
    // support it (e.g. local CLI, which never logs in per SPEC.md) no-op.
    // Not a security boundary on its own - a non-compliant client can ignore
    // the negotiation - just makes normal clients not show the password.
    ValueTask SetEchoAsync(bool enabled, CancellationToken ct);
}
