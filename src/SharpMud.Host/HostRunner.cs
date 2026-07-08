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
                connectionTasks.Add(HandleConnectionAsync(session, world, parser, registry, startingRoom, ct));
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
        Thing startingRoom,
        CancellationToken ct)
    {
        try
        {
            await session.WriteAsync("Name: ", ct);
            var name = await session.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(name))
            {
                await session.DisconnectAsync("No name given.", ct);
                return;
            }

            var player = HubWorldBuilder.CreatePlayer(world, name.Trim(), startingRoom);
            player.FindBehavior<PlayerBehavior>()!.Session = session;

            await SessionLoop.RunAsync(world, parser, registry, session, player, startingRoom, ct);
        }
        catch (Exception ex)
        {
            // Exception isolation per connection - one bad session must not
            // take down the listener or other connections.
            Console.Error.WriteLine($"Session error: {ex}");
        }
    }
}
