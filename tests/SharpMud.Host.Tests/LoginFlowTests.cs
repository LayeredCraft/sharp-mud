using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Host.Tests;

public sealed class LoginFlowTests
{
    private static Thing MakeRoom() => new() { Id = ThingId.New(), Name = "Room" };

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

        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        session.IsConnected.Returns(true);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await LoginFlow.RunAsync(session, world, repository, room, TestContext.Current.CancellationToken);

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

        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        // First loop iteration: correct password but rejected -> RunAsync
        // checks IsConnected before looping again; simulate the client
        // disconnecting after the rejection so RunAsync returns null.
        session.IsConnected.Returns(true, false);
        session.ReadLineAsync(Arg.Any<CancellationToken>()).Returns("Hero", "correct-horse");

        var result = await LoginFlow.RunAsync(session, world, repository, room, TestContext.Current.CancellationToken);

        result.Should().BeNull();
        playerBehavior.ConnectionState.Should().Be(ConnectionState.Playing);
        await session.Received(1).WriteLineAsync("That character is already logged in.", Arg.Any<CancellationToken>());
    }
}
