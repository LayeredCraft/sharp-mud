using Microsoft.Extensions.DependencyInjection;
using SharpMud.Adapters.Cli;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;
using SharpMud.Host;
using SharpMud.Ruleset.Classic;

var (world, startingRoom) = HubWorldBuilder.Build();

var random = new RandomSource();
var combatResolver = new CombatResolver(random);
var combatManager = new CombatManager(combatResolver, startingRoom);
var wanderManager = new WanderManager(world, random);

var gameLoop = new GameLoop(new GameLoopOptions());
gameLoop.Register(combatManager);
gameLoop.Register(wanderManager);

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

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var gameLoopTask = gameLoop.RunAsync(cts.Token);

var env = new Dictionary<string, string?>
{
    ["SHARPMUD_MODE"] = Environment.GetEnvironmentVariable("SHARPMUD_MODE"),
    ["SHARPMUD_TELNET_PORT"] = Environment.GetEnvironmentVariable("SHARPMUD_TELNET_PORT"),
};
var hostOptions = HostOptions.Parse(args, env);

if (hostOptions.UseTelnet)
{
    await HostRunner.RunTelnetAsync(world, parser, registry, startingRoom, hostOptions.TelnetPort, cts.Token);
}
else
{
    ISession session = new ConsoleSession();
    var player = HubWorldBuilder.CreatePlayer(world, "Adventurer", startingRoom);
    player.FindBehavior<PlayerBehavior>()!.Session = session;

    await SessionLoop.RunAsync(world, parser, registry, session, player, startingRoom, cts.Token);
}

cts.Cancel();
try
{
    await gameLoopTask;
}
catch (OperationCanceledException)
{
    // Expected on shutdown.
}
