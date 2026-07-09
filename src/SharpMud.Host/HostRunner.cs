using SharpMud.Adapters.Telnet;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Host;

public static class HostRunner
{
    public static async Task RunTelnetAsync(
        World world,
        ICommandParser parser,
        ICommandRegistry registry,
        IThingRepository repository,
        Thing startingRoom,
        int port,
        CancellationToken ct)
    {
        var listener = new TelnetListener(port);
        listener.Start();
        Console.WriteLine($"Listening for telnet connections on port {port}...");

        var connectionTasks = new List<Task>();

        try
        {
            await foreach (var session in listener.AcceptSessionsAsync(ct))
                connectionTasks.Add(HandleConnectionAsync(session, world, parser, registry, repository, startingRoom, ct));
        }
        finally
        {
            listener.Stop();
            await Task.WhenAll(connectionTasks);
        }
    }

    private static async Task HandleConnectionAsync(
        ISession session,
        World world,
        ICommandParser parser,
        ICommandRegistry registry,
        IThingRepository repository,
        Thing startingRoom,
        CancellationToken ct)
    {
        try
        {
            var player = await LoginFlow.RunAsync(session, world, repository, startingRoom, ct);
            if (player is null)
            {
                await session.DisconnectAsync("Login failed.", ct);
                return;
            }

            player.FindBehavior<PlayerBehavior>()!.Session = session;

            await SessionLoop.RunAsync(world, parser, registry, session, player, repository, ct);
        }
        catch (Exception ex)
        {
            // Exception isolation per connection - one bad session must not
            // take down the listener or other connections.
            await Console.Error.WriteLineAsync($"Session error: {ex}", ct);
        }
    }
}
