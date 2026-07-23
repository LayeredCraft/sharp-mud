using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Hosting;

namespace SharpMud.Hosting.Tests;

public sealed class LoginFlowTests
{
    private static Thing MakeRoom() => new() { Id = ThingId.New(), Name = "Room" };

    private static (LoginFlow loginFlow, IThingRepository repository) MakeLoginFlow(World world, Thing room, string? initialAdminUsername = null)
    {
        var worldContext = new WorldContext();
        worldContext.Initialize(world, room, room);
        var repository = Substitute.For<IThingRepository>();
        var playerFactory = Substitute.For<IPlayerFactory>();
        var hostOptions = new SharpMudHostOptions("unused.db", initialAdminUsername);

        return (new LoginFlow(worldContext, repository, playerFactory, hostOptions), repository);
    }

    [Fact]
    public async Task RunAsync_ResumesCharacter_WhenLinkdeadAndPasswordCorrect()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash };
        playerBehavior.EnterLinkdead(DateTimeOffset.UtcNow);
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        var (loginFlow, _) = MakeLoginFlow(world, room);
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().Be(player);
        playerBehavior.ConnectionState.Should().Be(ConnectionState.Playing);
        playerBehavior.LinkdeadSinceUtc.Should().BeNull();
        await session.Received(1).WriteLineAsync("Welcome back.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_Rejects_WhenPlayingAndAlreadyConnected()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var liveSession = Substitute.For<ISession>();
        liveSession.IsConnected.Returns(true);
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash, Session = liveSession };
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        var (loginFlow, _) = MakeLoginFlow(world, room);
        var session = Substitute.For<ISession>();
        // First loop iteration: correct password but rejected -> RunAsync
        // checks IsConnected before looping again; simulate the client
        // disconnecting after the rejection so RunAsync returns null.
        session.IsConnected.Returns(true, false);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().BeNull();
        playerBehavior.ConnectionState.Should().Be(ConnectionState.Playing);
        await session.Received(1).WriteLineAsync("That character is already logged in.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_RejectsAsExpired_WhenLinkdeadPastGraceWindow()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash };
        playerBehavior.EnterLinkdead(DateTimeOffset.UtcNow - ReconnectPolicy.GraceWindow - TimeSpan.FromSeconds(1));
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        var (loginFlow, _) = MakeLoginFlow(world, room);
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true, false);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().BeNull();
        playerBehavior.ConnectionState.Should().Be(ConnectionState.Linkdead);
        await session.Received(1).WriteLineAsync("That session has expired. Please log in again.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_RejectsAsExpired_WhenLinkdeadThingAlreadyRemovedFromWorld()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash };
        playerBehavior.EnterLinkdead(DateTimeOffset.UtcNow); // well within the grace window...
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        // ...but LinkdeadSweeper already raced ahead and removed it from
        // its room (leaving it parentless) between the earlier live-player
        // lookup and this password check.
        room.Remove(player);

        var (loginFlow, _) = MakeLoginFlow(world, room);
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true, false);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().BeNull();
        await session.Received(1).WriteLineAsync("That session has expired. Please log in again.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_Rejects_WhenAccountIsBanned()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash };
        playerBehavior.Ban();
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        var (loginFlow, _) = MakeLoginFlow(world, room);
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true, false);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().BeNull();
        await session.Received(1).WriteLineAsync("This account has been banned.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_GrantsFullAdmin_WhenUsernameMatchesInitialAdmin()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash };
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        var (loginFlow, repository) = MakeLoginFlow(world, room, initialAdminUsername: "hero");
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().Be(player);
        playerBehavior.Roles.Should().HaveFlag(SecurityRole.FullAdmin);
        await repository.Received(1).SaveTreeAsync(player, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_GrantsNothing_WhenUsernameDoesNotMatchInitialAdmin()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash };
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        var (loginFlow, _) = MakeLoginFlow(world, room, initialAdminUsername: "SomeoneElse");
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().Be(player);
        playerBehavior.Roles.Should().Be(SecurityRole.Player);
    }

    [Fact]
    public async Task RunAsync_GrantsNothing_WhenInitialAdminUsernameIsNull()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash };
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        var (loginFlow, repository) = MakeLoginFlow(world, room);
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().Be(player);
        playerBehavior.Roles.Should().Be(SecurityRole.Player);
        await repository.DidNotReceive().SaveTreeAsync(Arg.Any<Thing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DoesNotReSave_WhenInitialAdminAlreadyHoldsFullAdmin()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var passwordHash = PasswordHashing.Hash("correct-horse");
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "Hero", PasswordHash = passwordHash };
        playerBehavior.GrantRole(SecurityRole.FullAdmin);
        player.Behaviors.Add(playerBehavior);
        room.Add(player);
        world.Register(player);

        var (loginFlow, repository) = MakeLoginFlow(world, room, initialAdminUsername: "Hero");
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await loginFlow.RunAsync(session, TestContext.Current.CancellationToken);

        result.Should().Be(player);
        await repository.DidNotReceive().SaveTreeAsync(Arg.Any<Thing>(), Arg.Any<CancellationToken>());
    }
}
