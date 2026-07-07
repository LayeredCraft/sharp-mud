using SharpMud.Engine.Sessions;

namespace SharpMud.Adapters.Cli;

// v1 local transport (docs/networking.md): stdin/stdout, single process,
// single player. Console.ReadLine is blocking, which is fine for this
// single-session adapter - a networked adapter (Telnet/SSH/WebSocket) would
// use a real async socket read instead.
public sealed class ConsoleSession : ISession
{
    public string SessionId { get; } = Guid.NewGuid().ToString();
    public bool IsConnected { get; private set; } = true;

    public ValueTask<string?> ReadLineAsync(CancellationToken ct)
    {
        var line = Console.ReadLine();
        if (line is null)
            IsConnected = false;

        return ValueTask.FromResult(line);
    }

    public ValueTask WriteLineAsync(string text, CancellationToken ct)
    {
        Console.WriteLine(text);
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteAsync(string text, CancellationToken ct)
    {
        Console.Write(text);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(string? reason, CancellationToken ct)
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
