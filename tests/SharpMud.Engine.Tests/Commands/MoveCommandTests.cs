using SharpMud.Engine.Characters;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Tests.Commands;

public sealed class MoveCommandTests
{
    [Theory, EngineAutoData]
    public async Task ExecuteAsync_MovesPlayerAndShowsDestination_WhenExitExists(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var destination = new Room
        {
            Id = RoomId.New(),
            AreaId = AreaId.New(),
            Name = "Market Street",
            Description = "A busy street.",
        };
        var origin = new Room
        {
            Id = RoomId.New(),
            AreaId = AreaId.New(),
            Name = "Town Square",
            Description = "A square.",
        };
        origin.Exits.Add(new Exit { Direction = Direction.North, DestinationRoomId = destination.Id });

        var player = Player.CreateDefault("Adventurer", origin.Id);

        world.GetRoom(destination.Id).Returns(destination);
        world.PlayersInRoom(destination.Id).Returns([]);

        var sut = new MoveCommand(Direction.North, "north", ["n"]);
        var ctx = new CommandContext(player, origin, [], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await world.Received(1).MovePlayerAsync(player, origin, destination, Direction.North, Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync(destination.Name, Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsCannotGoMessage_WhenNoExitExists(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var origin = new Room
        {
            Id = RoomId.New(),
            AreaId = AreaId.New(),
            Name = "Town Square",
            Description = "A square.",
        };
        var player = Player.CreateDefault("Adventurer", origin.Id);

        var sut = new MoveCommand(Direction.North, "north", ["n"]);
        var ctx = new CommandContext(player, origin, [], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("You can't go that way.", Arg.Any<CancellationToken>());
        await world.DidNotReceive().MovePlayerAsync(
            Arg.Any<Player>(), Arg.Any<Room>(), Arg.Any<Room>(), Arg.Any<Direction?>(), Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsLockedMessage_WhenExitIsLocked(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var origin = new Room
        {
            Id = RoomId.New(),
            AreaId = AreaId.New(),
            Name = "Town Square",
            Description = "A square.",
        };
        origin.Exits.Add(new Exit
        {
            Direction = Direction.North,
            DestinationRoomId = RoomId.New(),
            Lock = new ExitLockState { IsLocked = true },
        });

        var player = Player.CreateDefault("Adventurer", origin.Id);

        var sut = new MoveCommand(Direction.North, "north", ["n"]);
        var ctx = new CommandContext(player, origin, [], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("The door is locked.", Arg.Any<CancellationToken>());
        await world.DidNotReceive().MovePlayerAsync(
            Arg.Any<Player>(), Arg.Any<Room>(), Arg.Any<Room>(), Arg.Any<Direction?>(), Arg.Any<CancellationToken>());
    }
}
