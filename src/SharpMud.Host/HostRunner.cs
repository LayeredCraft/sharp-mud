using LayeredCraft.StructuredLogging;
using SharpMud.Adapters.Telnet;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Sessions;

namespace SharpMud.Host;

public static class HostRunner
{
    public static async Task RunTelnetAsync(TelnetHostContext context, CancellationToken ct)
    {
        var listener = new TelnetListener(context.Port, context.Logger);
        listener.Start();
        context.Logger.Information("Listening for telnet connections on port {Port}", context.Port);

        var connectionTasks = new List<Task>();

        try
        {
            await foreach (var session in listener.AcceptSessionsAsync(ct))
                connectionTasks.Add(HandleConnectionAsync(session, context, ct));
        }
        finally
        {
            listener.Stop();
            await Task.WhenAll(connectionTasks);
        }
    }

    private static async Task HandleConnectionAsync(ISession session, TelnetHostContext context, CancellationToken ct)
    {
        try
        {
            var player = await LoginFlow.RunAsync(session, context.World, context.Repository, context.StartingRoom, ct);
            if (player is null)
            {
                await session.DisconnectAsync("Login failed.", ct);
                return;
            }

            player.FindBehavior<PlayerBehavior>()!.Session = session;

            await SessionLoop.RunAsync(context.World, context.Parser, context.Registry, session, player, context.Repository, ct);
        }
        catch (Exception ex)
        {
            // Exception isolation per connection - one bad session must not
            // take down the listener or other connections.
            context.Logger.Error(ex, "Session error for {SessionId}", session.SessionId);
        }
    }
}
