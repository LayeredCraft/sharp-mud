using Microsoft.Extensions.DependencyInjection;
using SharpMud.Adapters.Cli;
using SharpMud.Engine.Characters;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.World;

var services = new ServiceCollection();
services.AddSingleton<ICommandParser, CommandParser>();
services.AddSingleton<ICommandRegistry>(_ =>
{
    var registry = new CommandRegistry();
    BuiltinCommands.RegisterAll(registry);
    return registry;
});

await using var provider = services.BuildServiceProvider();

var parser = provider.GetRequiredService<ICommandParser>();
var registry = provider.GetRequiredService<ICommandRegistry>();

var (world, startingRoomId) = WorldBuilder.BuildHub();

ISession session = new ConsoleSession();
var player = Player.CreateDefault("Adventurer", startingRoomId);
world.Connect(player, session);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await session.WriteLineAsync("Welcome to SharpMud.", cts.Token);
await session.WriteLineAsync(string.Empty, cts.Token);

var startingRoom = world.GetRoom(startingRoomId)!;
await LookCommand.SendRoomDescriptionAsync(player, startingRoom, world, session, cts.Token);

while (session.IsConnected && !cts.IsCancellationRequested)
{
    await session.WriteAsync("> ", cts.Token);

    var input = await session.ReadLineAsync(cts.Token);
    if (input is null)
        break;

    var parsed = parser.Parse(input);
    if (parsed.Verb.Length == 0)
        continue;

    var currentRoom = world.GetRoom(player.CurrentRoomId);
    if (currentRoom is null)
        break;

    if (!registry.TryResolve(parsed.Verb, out var command))
    {
        await session.WriteLineAsync("Huh?", cts.Token);
        continue;
    }

    var context = new CommandContext(player, currentRoom, parsed.Args, world, session);
    await command.ExecuteAsync(context, cts.Token);
}

world.Disconnect(player.Id);
