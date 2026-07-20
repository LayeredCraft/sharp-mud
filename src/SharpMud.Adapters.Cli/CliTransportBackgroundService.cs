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

    // IServiceProvider, not PlayerLogin/SessionLoop directly - see
    // TelnetTransportBackgroundService's constructor comment for why
    // eager constructor injection here would resolve WorldContext-dependent
    // services before WorldLoaderHostedService.StartAsync has populated it.
    public CliTransportBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var session = new ConsoleSession();
        var playerLogin = _serviceProvider.GetRequiredService<PlayerLogin>();
        var player = await playerLogin.ResolveOrCreateAsync("Adventurer", stoppingToken);
        player.FindBehavior<PlayerBehavior>()!.Session = session;

        var sessionLoop = _serviceProvider.GetRequiredService<SessionLoop>();
        await sessionLoop.RunAsync(session, player, stoppingToken);
    }
}
