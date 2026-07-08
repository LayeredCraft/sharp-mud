using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Host;

// Shared by every transport (local CLI, Telnet, ...) so adding a transport
// never touches game logic - just a new ISession implementation feeding the
// same loop (SPEC.md transport-agnostic sessions decision).
public static class SessionLoop
{
    public static async Task RunAsync(
        World world,
        ICommandParser parser,
        ICommandRegistry registry,
        ISession session,
        Thing player,
        Thing startingRoom,
        CancellationToken ct)
    {
        await session.WriteLineAsync("Welcome to SharpMud.", ct);
        await session.WriteLineAsync(string.Empty, ct);

        await LookCommand.SendRoomDescriptionAsync(player, startingRoom, ct);

        while (session.IsConnected && !ct.IsCancellationRequested)
        {
            await session.WriteAsync("> ", ct);

            var input = await session.ReadLineAsync(ct);
            if (input is null)
                break;

            var parsed = parser.Parse(input);
            if (parsed.Verb.Length == 0)
                continue;

            var currentRoom = player.Parent;
            if (currentRoom is null)
                break;

            if (!registry.TryResolve(parsed.Verb, out var command))
            {
                await session.WriteLineAsync("Huh?", ct);
                continue;
            }

            var context = new CommandContext(player, currentRoom, parsed.Args, world, session);
            await command.ExecuteAsync(context, ct);
        }

        player.Parent?.Remove(player);
        world.Unregister(player.Id);
    }
}
