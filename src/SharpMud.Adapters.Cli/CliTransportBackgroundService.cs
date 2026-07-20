using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpMud.Engine.Behaviors;
using SharpMud.Hosting;

namespace SharpMud.Adapters.Cli;

// Absorbs what today's Program.cs's CLI branch does inline: a single local
// stdin/stdout session, no login (SPEC.md), resolved/created via PlayerLogin.
internal sealed class CliTransportBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _lifetime;

    // IServiceProvider, not PlayerLogin/SessionLoop directly - see
    // TelnetTransportBackgroundService's constructor comment for why
    // eager constructor injection here would resolve WorldContext-dependent
    // services before WorldLoaderHostedService.StartAsync has populated it.
    public CliTransportBackgroundService(IServiceProvider serviceProvider, IHostApplicationLifetime lifetime)
    {
        _serviceProvider = serviceProvider;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var session = new ConsoleSession();
        var playerLogin = _serviceProvider.GetRequiredService<PlayerLogin>();
        var player = await playerLogin.ResolveOrCreateAsync("Adventurer", stoppingToken);
        player.FindBehavior<PlayerBehavior>()!.Session = session;

        var sessionLoop = _serviceProvider.GetRequiredService<SessionLoop>();
        await sessionLoop.RunAsync(session, player, stoppingToken);

        // CLI mode is a single, one-shot local session (SPEC.md) - once it
        // ends (quit or disconnect), the whole process should exit, matching
        // the pre-refactor behavior (Program.cs used to just fall through
        // to shutdown once this returned). Without this, IHost keeps
        // running indefinitely (GameLoop, etc.) with no session left to
        // serve, waiting for an external SIGINT/SIGTERM that a local CLI
        // user has no reason to expect. Telnet's BackgroundService
        // deliberately does NOT do this - many concurrent sessions, one
        // ending must not stop the host.
        if (!stoppingToken.IsCancellationRequested)
            _lifetime.StopApplication();
    }
}
