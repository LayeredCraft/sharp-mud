using SharpMud.Engine.Characters;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Tests.World;

public sealed class WorldTests
{
    [Fact]
    public async Task MovePlayerAsync_UpdatesCurrentRoomId()
    {
        var sut = new SharpMud.Engine.World.World();
        var origin = MakeRoom("Origin");
        var destination = MakeRoom("Destination");
        sut.RegisterRoom(origin);
        sut.RegisterRoom(destination);

        var mover = Player.CreateDefault("Mover", origin.Id);
        sut.Connect(mover, Substitute.For<ISession>());

        await sut.MovePlayerAsync(mover, origin, destination, Direction.North, TestContext.Current.CancellationToken);

        mover.CurrentRoomId.Should().Be(destination.Id);
    }

    [Fact]
    public async Task MovePlayerAsync_NotifiesOccupantsLeftBehind_ButNotTheMover()
    {
        var sut = new SharpMud.Engine.World.World();
        var origin = MakeRoom("Origin");
        var destination = MakeRoom("Destination");
        sut.RegisterRoom(origin);
        sut.RegisterRoom(destination);

        var mover = Player.CreateDefault("Mover", origin.Id);
        var moverSession = Substitute.For<ISession>();
        sut.Connect(mover, moverSession);

        var bystander = Player.CreateDefault("Bystander", origin.Id);
        var bystanderSession = Substitute.For<ISession>();
        sut.Connect(bystander, bystanderSession);

        await sut.MovePlayerAsync(mover, origin, destination, Direction.North, TestContext.Current.CancellationToken);

        await bystanderSession.Received(1).WriteLineAsync("Mover leaves north.", Arg.Any<CancellationToken>());
        await moverSession.DidNotReceive().WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MovePlayerAsync_NotifiesOccupantsAlreadyInDestination()
    {
        var sut = new SharpMud.Engine.World.World();
        var origin = MakeRoom("Origin");
        var destination = MakeRoom("Destination");
        sut.RegisterRoom(origin);
        sut.RegisterRoom(destination);

        var mover = Player.CreateDefault("Mover", origin.Id);
        sut.Connect(mover, Substitute.For<ISession>());

        var alreadyThere = Player.CreateDefault("AlreadyThere", destination.Id);
        var alreadyThereSession = Substitute.For<ISession>();
        sut.Connect(alreadyThere, alreadyThereSession);

        await sut.MovePlayerAsync(mover, origin, destination, Direction.North, TestContext.Current.CancellationToken);

        await alreadyThereSession.Received(1).WriteLineAsync("Mover arrives.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PlayersInRoom_ReturnsOnlyPlayersInThatRoom()
    {
        var sut = new SharpMud.Engine.World.World();
        var roomA = MakeRoom("A");
        var roomB = MakeRoom("B");
        sut.RegisterRoom(roomA);
        sut.RegisterRoom(roomB);

        var inA = Player.CreateDefault("InA", roomA.Id);
        var inB = Player.CreateDefault("InB", roomB.Id);
        sut.Connect(inA, Substitute.For<ISession>());
        sut.Connect(inB, Substitute.For<ISession>());

        sut.PlayersInRoom(roomA.Id).Should().ContainSingle().Which.Should().BeSameAs(inA);
    }

    private static Room MakeRoom(string name) => new()
    {
        Id = RoomId.New(),
        AreaId = AreaId.New(),
        Name = name,
        Description = $"{name} description.",
    };
}
