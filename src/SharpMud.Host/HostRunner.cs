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
            await session.WriteAsync("Name: ", ct);
            var name = (await session.ReadLineAsync(ct))?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                await session.DisconnectAsync("No name given.", ct);
                return;
            }

            var player = await PlayerLogin.ResolveOrCreateAsync(world, repository, name, startingRoom, ct);
            var playerBehavior = player.FindBehavior<PlayerBehavior>()!;

            if (playerBehavior.Session is { IsConnected: true })
            {
                await session.WriteLineAsync("That character is already logged in.", ct);
                await session.DisconnectAsync("Already connected.", ct);
                return;
            }

            playerBehavior.Session = session;

            await SessionLoop.RunAsync(world, parser, registry, session, player, repository, ct);
        }
        catch (Exception ex)
        {
            // Exception isolation per connection - one bad session must not
            // take down the listener or other connections.
            Console.Error.WriteLine($"Session error: {ex}");
        }
    }
}
