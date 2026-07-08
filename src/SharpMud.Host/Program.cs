using Microsoft.Extensions.DependencyInjection;
using SharpMud.Adapters.Cli;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;
using SharpMud.Host;
using SharpMud.Ruleset.Classic;

var (world, startingRoom) = HubWorldBuilder.Build();

var random = new RandomSource();
var combatResolver = new CombatResolver(random);
var combatManager = new CombatManager(combatResolver, startingRoom);

var gameLoop = new GameLoop(new GameLoopOptions());
gameLoop.Register(combatManager);

var services = new ServiceCollection();
services.AddSingleton<ICommandParser, CommandParser>();
services.AddSingleton<ICommandRegistry>(_ =>
{
    var registry = new CommandRegistry();
    BuiltinCommands.RegisterAll(registry);
    ClassicCommands.RegisterAll(registry, combatManager, random);
    return registry;
});

await using var provider = services.BuildServiceProvider();

var parser = provider.GetRequiredService<ICommandParser>();
var registry = provider.GetRequiredService<ICommandRegistry>();

ISession session = new ConsoleSession();
var player = HubWorldBuilder.CreatePlayer(world, "Adventurer", startingRoom);
player.FindBehavior<PlayerBehavior>()!.Session = session;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var gameLoopTask = gameLoop.RunAsync(cts.Token);

await session.WriteLineAsync("Welcome to SharpMud.", cts.Token);
await session.WriteLineAsync(string.Empty, cts.Token);

await LookCommand.SendRoomDescriptionAsync(player, startingRoom, cts.Token);

while (session.IsConnected && !cts.IsCancellationRequested)
{
    await session.WriteAsync("> ", cts.Token);

    var input = await session.ReadLineAsync(cts.Token);
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
        await session.WriteLineAsync("Huh?", cts.Token);
        continue;
    }

    var context = new CommandContext(player, currentRoom, parsed.Args, world, session);
    await command.ExecuteAsync(context, cts.Token);
}

world.Unregister(player.Id);

cts.Cancel();
try
{
    await gameLoopTask;
}
catch (OperationCanceledException)
{
    // Expected on shutdown.
}
