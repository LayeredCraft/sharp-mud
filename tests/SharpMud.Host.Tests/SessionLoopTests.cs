using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Host.Tests;

public sealed class SessionLoopTests
{
    private static (World world, Thing room, Thing player, PlayerBehavior playerBehavior) MakePlayerInRoom()
    {
        var world = new World();
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = "test-hash" };
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(room);
        world.Register(player);
        return (world, room, player, playerBehavior);
    }

    [Fact]
    public async Task RunAsync_MarksLinkdead_WhenSessionDropsWithoutQuit()
    {
        var (world, room, player, playerBehavior) = MakePlayerInRoom();
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        playerBehavior.Session = session;

        var parser = Substitute.For<ICommandParser>();
        var registry = Substitute.For<ICommandRegistry>();
        var repository = Substitute.For<IThingRepository>();

        await SessionLoop.RunAsync(world, parser, registry, session, player, repository, TestContext.Current.CancellationToken);

        playerBehavior.ConnectionState.Should().Be(ConnectionState.Linkdead);
        room.Children.Should().Contain(player);
        await repository.Received(1).SaveTreeAsync(player, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DoesNotClobberState_WhenNewerSessionAlreadyReconnected()
    {
        var (world, room, player, playerBehavior) = MakePlayerInRoom();
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

        await SessionLoop.RunAsync(world, parser, registry, oldSession, player, repository, TestContext.Current.CancellationToken);

        // The stale disconnect must not mark the actively-reconnected
        // character Linkdead or remove it from the world.
        playerBehavior.ConnectionState.Should().Be(ConnectionState.Playing);
        playerBehavior.Session.Should().Be(newSession);
        room.Children.Should().Contain(player);
    }

    [Fact]
    public async Task RunAsync_RemovesImmediately_WhenPlayerQuits()
    {
        var (world, room, player, playerBehavior) = MakePlayerInRoom();
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

        await SessionLoop.RunAsync(world, parser, registry, session, player, repository, TestContext.Current.CancellationToken);

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
