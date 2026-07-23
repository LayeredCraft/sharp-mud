using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Hosting;

namespace SharpMud.Hosting.Tests;

public sealed class SessionLoopTests
{
    private static (WorldContext worldContext, World world, Thing room, Thing player, PlayerBehavior playerBehavior) MakePlayerInRoom()
    {
        var world = new World();
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = "test-hash" };
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(room);
        world.Register(player);

        var worldContext = new WorldContext();
        worldContext.Initialize(world, room, room);

        return (worldContext, world, room, player, playerBehavior);
    }

    [Fact]
    public async Task RunAsync_MarksLinkdead_WhenSessionDropsWithoutQuit()
    {
        var (worldContext, world, room, player, playerBehavior) = MakePlayerInRoom();
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        playerBehavior.Session = session;

        var parser = Substitute.For<ICommandParser>();
        var registry = Substitute.For<ICommandRegistry>();
        var repository = Substitute.For<IThingRepository>();
        var sessionLoop = new SessionLoop(worldContext, parser, registry, repository);

        await sessionLoop.RunAsync(session, player, TestContext.Current.CancellationToken);

        playerBehavior.ConnectionState.Should().Be(ConnectionState.Linkdead);
        room.Children.Should().Contain(player);
        await repository.Received(1).SaveTreeAsync(player, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DoesNotClobberState_WhenNewerSessionAlreadyReconnected()
    {
        var (worldContext, world, room, player, playerBehavior) = MakePlayerInRoom();
        var oldSession = Substitute.For<ISession>();
        oldSession.IsConnected.Returns(true);
        oldSession.ReadLineAsync(Arg.Any<CancellationToken>()).Returns((string?)null);

        // Simulate a reconnect having already raced ahead and attached a
        // newer session to this same Thing before oldSession's disconnect
        // finally block runs (PR #1 review) - PlayerBehavior.Session no
        // longer points at oldSession.
        var newSession = Substitute.For<ISession>();
        playerBehavior.Session = newSession;

        var parser = Substitute.For<ICommandParser>();
        var registry = Substitute.For<ICommandRegistry>();
        var repository = Substitute.For<IThingRepository>();
        var sessionLoop = new SessionLoop(worldContext, parser, registry, repository);

        await sessionLoop.RunAsync(oldSession, player, TestContext.Current.CancellationToken);

        // The stale disconnect must not mark the actively-reconnected
        // character Linkdead or remove it from the world.
        playerBehavior.ConnectionState.Should().Be(ConnectionState.Playing);
        playerBehavior.Session.Should().Be(newSession);
        room.Children.Should().Contain(player);
    }

    [Fact]
    public async Task RunAsync_RemovesImmediately_WhenPlayerQuits()
    {
        var (worldContext, world, room, player, playerBehavior) = MakePlayerInRoom();
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true, false);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("quit");
        playerBehavior.Session = session;

        var parser = new CommandParser();
        var registry = Substitute.For<ICommandRegistry>();
        registry.TryResolve("quit", out Arg.Any<ICommand?>()).Returns(x =>
        {
            x[1] = new StubQuitCommand();
            return true;
        });
        var repository = Substitute.For<IThingRepository>();
        var sessionLoop = new SessionLoop(worldContext, parser, registry, repository);

        await sessionLoop.RunAsync(session, player, TestContext.Current.CancellationToken);

        room.Children.Should().NotContain(player);
        world.GetThing(player.Id).Should().BeNull();
        playerBehavior.ConnectionState.Should().Be(ConnectionState.Playing);
    }

    [Fact]
    public async Task RunAsync_SavesWithParentStillSet_WhenPlayerQuits()
    {
        // PR #1 review: quit's removal must happen AFTER the save, not
        // before - ThingRepository.SaveTreeAsync persists ParentId from
        // thing.Parent at save time, so removing first would silently save
        // ParentId=null and lose the room the player quit from.
        var (worldContext, world, room, player, playerBehavior) = MakePlayerInRoom();
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true, false);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("quit");
        playerBehavior.Session = session;

        var parser = new CommandParser();
        var registry = Substitute.For<ICommandRegistry>();
        registry.TryResolve("quit", out Arg.Any<ICommand?>()).Returns(x =>
        {
            x[1] = new StubQuitCommand();
            return true;
        });

        Thing? parentAtSaveTime = null;
        var repository = Substitute.For<IThingRepository>();
        repository.SaveTreeAsync(Arg.Any<Thing>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(x => parentAtSaveTime = x.ArgAt<Thing>(0).Parent);
        var sessionLoop = new SessionLoop(worldContext, parser, registry, repository);

        await sessionLoop.RunAsync(session, player, TestContext.Current.CancellationToken);

        parentAtSaveTime.Should().Be(room);
    }

    [Fact]
    public async Task RunAsync_RemovesImmediately_WhenPlayerWasBooted()
    {
        // ADR-0005: an admin's BootCommand/BanCommand sets WasBooted on a
        // different call stack before disconnecting this session - this
        // player's own SessionLoop must treat that exactly like an
        // explicit quit (immediate removal), not Linkdead (which would let
        // the player just reconnect and undo the boot/ban).
        var (worldContext, world, room, player, playerBehavior) = MakePlayerInRoom();
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        playerBehavior.Session = session;
        playerBehavior.MarkBooted();

        var parser = Substitute.For<ICommandParser>();
        var registry = Substitute.For<ICommandRegistry>();
        var repository = Substitute.For<IThingRepository>();
        var sessionLoop = new SessionLoop(worldContext, parser, registry, repository);

        await sessionLoop.RunAsync(session, player, TestContext.Current.CancellationToken);

        room.Children.Should().NotContain(player);
        world.GetThing(player.Id).Should().BeNull();
        playerBehavior.ConnectionState.Should().Be(ConnectionState.Playing);
    }

    private sealed class StubQuitCommand : ICommand
    {
        public string Verb => "quit";
        public IReadOnlyList<string> Aliases { get; } = [];
        public Task ExecuteAsync(CommandContext ctx, CancellationToken ct) => Task.CompletedTask;
    }
}
