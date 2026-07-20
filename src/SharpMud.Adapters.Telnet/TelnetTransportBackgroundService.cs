using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Sessions;
using SharpMud.Hosting;

namespace SharpMud.Adapters.Telnet;

// Absorbs what today's HostRunner.cs (src/SharpMud.Host) does - accepts
// Telnet connections and, per connection, runs LoginFlow then SessionLoop -
// but as a DI-constructed BackgroundService instead of a static class
// manually threaded through Program.cs. See
// docs/adr/0006-nuget-package-distribution.md for why this lives here and
// not in SharpMud.Hosting (Hosting must not reference this project).
internal sealed class TelnetTransportBackgroundService : BackgroundService
{
    private readonly TelnetTransportOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelnetSession> _sessionLogger;
    private readonly ILogger<TelnetTransportBackgroundService> _logger;

    // IServiceProvider, not LoginFlow/SessionLoop directly - the generic
    // host resolves every registered IHostedService (constructing all of
    // them) before calling any of their StartAsync methods. LoginFlow's
    // own constructor is harmless, but it depends on ICommandRegistry,
    // whose registration factory (AddSharpMudRuleset) can call into a
    // consumer's ruleset registration code that reads
    // WorldContext.StartingRoom - which isn't populated yet at that point.
    // Deferring resolution to ExecuteAsync/HandleConnectionAsync (which
    // only run after StartAsync, sequenced after
    // WorldLoaderHostedService.StartAsync per registration order) avoids
    // that. This is the legitimate "resolving something at runtime"
    // exception to the no-service-locator rule in coding-standards.md.
    public TelnetTransportBackgroundService(
        TelnetTransportOptions options,
        IServiceProvider serviceProvider,
        ILogger<TelnetSession> sessionLogger,
        ILogger<TelnetTransportBackgroundService> logger)
    {
        _options = options;
        _serviceProvider = serviceProvider;
        _sessionLogger = sessionLogger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TelnetListener(_options.Port, _sessionLogger);
        listener.Start();
        _logger.Information("Listening for telnet connections on port {Port}", _options.Port);

        var connectionTasks = new List<Task>();

        try
        {
            await foreach (var session in listener.AcceptSessionsAsync(stoppingToken))
                connectionTasks.Add(HandleConnectionAsync(session, stoppingToken));
        }
        finally
        {
            listener.Stop();
            await Task.WhenAll(connectionTasks);
        }
    }

    private async Task HandleConnectionAsync(ISession session, CancellationToken ct)
    {
        try
        {
            var loginFlow = _serviceProvider.GetRequiredService<LoginFlow>();
            var player = await loginFlow.RunAsync(session, ct);
            if (player is null)
            {
                await session.DisconnectAsync("Login failed.", ct);
                return;
            }

            player.FindBehavior<PlayerBehavior>()!.Session = session;

            var sessionLoop = _serviceProvider.GetRequiredService<SessionLoop>();
            await sessionLoop.RunAsync(session, player, ct);
        }
        catch (Exception ex)
        {
            // Exception isolation per connection - one bad session must not
            // take down the listener or other connections.
            _logger.Error(ex, "Session error for {SessionId}", session.SessionId);
        }
    }
}
