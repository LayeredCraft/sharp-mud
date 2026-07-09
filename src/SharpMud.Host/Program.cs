using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpMud.Adapters.Cli;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;
using SharpMud.Host;
using SharpMud.Persistence;
using SharpMud.Ruleset.Classic;

var env = new Dictionary<string, string?>
{
    ["SHARPMUD_MODE"] = Environment.GetEnvironmentVariable("SHARPMUD_MODE"),
    ["SHARPMUD_TELNET_PORT"] = Environment.GetEnvironmentVariable("SHARPMUD_TELNET_PORT"),
    ["SHARPMUD_DB_PATH"] = Environment.GetEnvironmentVariable("SHARPMUD_DB_PATH"),
};
var hostOptions = HostOptions.Parse(args, env);

var services = new ServiceCollection();
services.AddDbContextFactory<GameDbContext>(options => options.UseSqlite($"Data Source={hostOptions.DbPath}"));
services.AddSingleton<IBehaviorMappingContributor, ClassicBehaviorMappingContributor>();
services.AddSingleton<IThingRepository, ThingRepository>();
services.AddSingleton<ICommandParser, CommandParser>();

await using var provider = services.BuildServiceProvider();

// EnsureCreated only, never EnsureDeleted, at boot - creates the schema if
// missing but never wipes existing data. See docs/persistence.md Schema/
// Migrations: a genuinely changed C# model against an old .db file during
// dev means deleting the file by hand, not an automatic wipe every startup
// (which would defeat persistence entirely).
await using (var dbContext = await provider.GetRequiredService<IDbContextFactory<GameDbContext>>().CreateDbContextAsync())
    await dbContext.Database.EnsureCreatedAsync();

var repository = provider.GetRequiredService<IThingRepository>();

var loadedArea = await repository.LoadTreeAsync(HubWorldBuilder.HubAreaId, CancellationToken.None);

World world;
Thing startingRoom;
Thing hubArea;

if (loadedArea is not null)
{
    world = new World();
    PlayerLogin.RegisterSubtree(world, loadedArea);
    hubArea = loadedArea;
    startingRoom = HubWorldBuilder.FindStartingRoom(hubArea)
        ?? hubArea.Children.First(c => c.HasBehavior<RoomBehavior>());
    Console.WriteLine("Loaded persisted world.");
}
else
{
    (world, startingRoom) = HubWorldBuilder.Build();
    hubArea = world.GetThing(HubWorldBuilder.HubAreaId)!;
    await repository.SaveTreeAsync(hubArea, CancellationToken.None);
    Console.WriteLine("No persisted world found - built and saved a fresh one.");
}

var random = new RandomSource();
var combatResolver = new CombatResolver(random);
var combatManager = new CombatManager(combatResolver, startingRoom);
var wanderManager = new WanderManager(world, random);

var gameLoop = new GameLoop(new GameLoopOptions());
gameLoop.Register(combatManager);
gameLoop.Register(wanderManager);

var registry = new CommandRegistry();
BuiltinCommands.RegisterAll(registry);
ClassicCommands.RegisterAll(registry, combatManager, random);

var parser = provider.GetRequiredService<ICommandParser>();

using var cts = new CancellationTokenSource();

// PosixSignalRegistration, not Console.CancelKeyPress - CancelKeyPress only
// ever catches SIGINT (Ctrl+C) and was observed not firing reliably without
// a TTY attached. Critically, it never catches SIGTERM at all, which is
// exactly what `docker stop`/Kubernetes send on a graceful shutdown - the
// actual scenario docs/persistence.md's "save on graceful shutdown" design
// depends on. Handling both signals here is what makes that real.
void RequestShutdown(PosixSignalContext context)
{
    context.Cancel = true;
    cts.Cancel();
}

using var sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, RequestShutdown);
using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, RequestShutdown);

var gameLoopTask = gameLoop.RunAsync(cts.Token);

if (hostOptions.UseTelnet)
{
    await HostRunner.RunTelnetAsync(world, parser, registry, repository, startingRoom, hostOptions.TelnetPort, cts.Token);
}
else
{
    ISession session = new ConsoleSession();
    var player = await PlayerLogin.ResolveOrCreateAsync(world, repository, "Adventurer", startingRoom, cts.Token);
    player.FindBehavior<PlayerBehavior>()!.Session = session;

    await SessionLoop.RunAsync(world, parser, registry, session, player, repository, cts.Token);
}

// Whole-world snapshot - each disconnected player already saved themselves
// (SessionLoop's finally block), but NPCs (wandering position, live combat
// HP) aren't tied to any player session and only get captured here.
await repository.SaveTreeAsync(hubArea, CancellationToken.None);

cts.Cancel();
try
{
    await gameLoopTask;
}
catch (OperationCanceledException)
{
    // Expected on shutdown.
}
