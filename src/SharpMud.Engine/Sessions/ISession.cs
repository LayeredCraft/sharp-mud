namespace SharpMud.Engine.Sessions;

public interface ISession
{
    string SessionId { get; }
    bool IsConnected { get; }

    /// <summary>
    /// The client's negotiated terminal width in columns, or a sensible
    /// default (80) if the transport doesn't negotiate this or the client
    /// hasn't responded yet. Telnet negotiates this asynchronously via NAWS
    /// (RFC 1073, see docs/networking.md) - callers must tolerate the
    /// default value changing after session start, not just at construction.
    /// </summary>
    int TerminalWidth { get; }

    /// <summary>
    /// The client's negotiated terminal height in rows, or a sensible
    /// default (20) - see <see cref="TerminalWidth"/> remarks for
    /// negotiation timing.
    /// </summary>
    int TerminalHeight { get; }

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
