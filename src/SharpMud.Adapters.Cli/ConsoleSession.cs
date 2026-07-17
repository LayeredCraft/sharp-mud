using SharpMud.Engine.Sessions;

namespace SharpMud.Adapters.Cli;

// v1 local transport (docs/networking.md): stdin/stdout, single process,
// single player. Console.ReadLine is blocking, which is fine for this
// single-session adapter - a networked adapter (Telnet/SSH/WebSocket) would
// use a real async socket read instead.
public sealed class ConsoleSession : ISession
{
    private const int DefaultTerminalWidth = 80;
    private const int DefaultTerminalHeight = 20;

    public string SessionId { get; } = Guid.NewGuid().ToString();
    public bool IsConnected { get; private set; } = true;

    // Real console dimensions are free and accurate for local single-player
    // use, unlike a hardcoded number - but redirected stdio (CI, piped
    // input) can throw on these, so fall back to Telnet's own unnegotiated
    // defaults (see ISession.TerminalWidth/TerminalHeight remarks).
    public int TerminalWidth => TryGetConsoleDimension(() => Console.WindowWidth, DefaultTerminalWidth);
    public int TerminalHeight => TryGetConsoleDimension(() => Console.WindowHeight, DefaultTerminalHeight);

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

    // No-op - local CLI never logs in (SPEC.md), so this is never exercised.
    public ValueTask SetEchoAsync(bool enabled, CancellationToken ct) => ValueTask.CompletedTask;

    private static int TryGetConsoleDimension(Func<int> getDimension, int fallback)
    {
        try
        {
            return getDimension();
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (ArgumentOutOfRangeException)
        {
            return fallback;
        }
    }
}
