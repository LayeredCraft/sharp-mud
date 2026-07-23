using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Builder;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Builder;

public sealed class DigCommandTests
{
    private static (Thing Area, Thing Room, World World, ISession Session) MakeRoom()
    {
        var world = new World();
        var area = new Thing { Id = ThingId.New(), Name = "Area" };
        world.Register(area);

        var room = new Thing { Id = ThingId.New(), Name = "Starting Room" };
        room.Behaviors.Add(new RoomBehavior());
        area.Add(room);
        world.Register(room);

        return (area, room, world, Substitute.For<ISession>());
    }

    [Fact]
    public async Task ExecuteAsync_CreatesRoomAndConnectsIt_OnHappyPath()
    {
        var repository = Substitute.For<IThingRepository>();
        var (area, room, world, session) = MakeRoom();

        var sut = new DigCommand(repository);
        var ctx = new CommandContext(room, room, ["north", "Storage", "Shed"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        var newRoom = area.Children.First(c => c.Name == "Storage Shed");
        newRoom.HasBehavior<RoomBehavior>().Should().BeTrue();
        newRoom.Parent.Should().Be(area, "a dug room is a sibling of the room it's dug from, not a child of it");

        var exit = room.Children.Select(c => c.FindBehavior<ExitBehavior>()).Single(e => e is not null);
        exit!.Direction.Should().Be(Direction.North);
        exit.Destination.Should().Be(newRoom);

        await repository.Received(1).SaveTreeAsync(area, Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync("You dig north, creating Storage Shed.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidDirection_WithoutMutating()
    {
        var repository = Substitute.For<IThingRepository>();
        var (area, room, world, session) = MakeRoom();

        var sut = new DigCommand(repository);
        var ctx = new CommandContext(room, room, ["sideways", "Storage Shed"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        area.Children.Should().ContainSingle(); // just the original room
        await session.Received(1).WriteLineAsync("'sideways' isn't a direction.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsDirectionAlreadyOccupied_WithoutMutating()
    {
        var repository = Substitute.For<IThingRepository>();
        var (area, room, world, session) = MakeRoom();
        var existingDestination = new Thing { Id = ThingId.New(), Name = "Existing" };
        existingDestination.Behaviors.Add(new RoomBehavior());
        area.Add(existingDestination);
        world.Register(existingDestination);
        RoomConnector.Connect(world, room, existingDestination, Direction.North);

        var sut = new DigCommand(repository);
        var ctx = new CommandContext(room, room, ["north", "Storage", "Shed"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        area.Children.Should().HaveCount(2, "just Starting Room and Existing - no new room was created");
        await session.Received(1).WriteLineAsync("There's already an exit north from here.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsUsageMessage_WhenMissingArguments()
    {
        var repository = Substitute.For<IThingRepository>();
        var (_, room, world, session) = MakeRoom();

        var sut = new DigCommand(repository);
        var ctx = new CommandContext(room, room, ["north"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("Usage: dig <direction> <new room name>", Arg.Any<CancellationToken>());
    }
}
