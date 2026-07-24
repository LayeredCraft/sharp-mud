using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Builder;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Builder;

public sealed class DescribeCommandTests
{
    [Fact]
    public async Task ExecuteAsync_SetsDescriptionAndSaves()
    {
        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        var world = new World();
        var area = new Thing { Id = ThingId.New(), Name = "Area" };
        world.Register(area);

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        room.Behaviors.Add(new RoomBehavior());
        area.Add(room);
        world.Register(room);

        var sut = new DescribeCommand(repository);
        var ctx = new CommandContext(room, room, ["A", "dusty", "old", "room."], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        room.Description.Should().Be("A dusty old room.");
        await repository.Received(1).SaveTreeAsync(area, Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync("Description updated.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsUsageMessage_WhenMissingArguments()
    {
        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        var world = new World();
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        room.Behaviors.Add(new RoomBehavior());
        world.Register(room);

        var sut = new DescribeCommand(repository);
        var ctx = new CommandContext(room, room, [], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("Describe the room as what?", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }
}
