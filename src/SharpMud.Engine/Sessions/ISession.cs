namespace SharpMud.Engine.Sessions;

public interface ISession
{
    string SessionId { get; }
    bool IsConnected { get; }

    ValueTask<string?> ReadLineAsync(CancellationToken ct);
    ValueTask WriteLineAsync(string text, CancellationToken ct);
    ValueTask WriteAsync(string text, CancellationToken ct);
    ValueTask DisconnectAsync(string? reason, CancellationToken ct);
}
