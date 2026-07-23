using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Builder;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Builder;

public sealed class TunnelCommandTests
{
    private static Thing MakeRoom(World world, Thing area, string name)
    {
        var room = new Thing { Id = ThingId.New(), Name = name };
        room.Behaviors.Add(new RoomBehavior());
        area.Add(room);
        world.Register(room);
        return room;
    }

    [Fact]
    public async Task ExecuteAsync_ConnectsToExistingRoom_OnHappyPath()
    {
        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        var world = new World();
        var area = new Thing { Id = ThingId.New(), Name = "Area" };
        world.Register(area);

        var origin = MakeRoom(world, area, "Origin");
        var destination = MakeRoom(world, area, "Destination");

        var sut = new TunnelCommand(repository);
        var ctx = new CommandContext(origin, origin, ["east", "Destination"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        var exit = origin.Children.Select(c => c.FindBehavior<ExitBehavior>()).Single(e => e is not null);
        exit!.Direction.Should().Be(Direction.East);
        exit.Destination.Should().Be(destination);

        await repository.Received(1).SaveTreeAsync(area, Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync("You tunnel east to Destination.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNoMatch()
    {
        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        var world = new World();
        var area = new Thing { Id = ThingId.New(), Name = "Area" };
        world.Register(area);
        var origin = MakeRoom(world, area, "Origin");

        var sut = new TunnelCommand(repository);
        var ctx = new CommandContext(origin, origin, ["east", "Nowhere"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("No room named Nowhere was found.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAmbiguousMatch()
    {
        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        var world = new World();
        var area = new Thing { Id = ThingId.New(), Name = "Area" };
        world.Register(area);
        var origin = MakeRoom(world, area, "Origin");
        MakeRoom(world, area, "Duplicate");
        MakeRoom(world, area, "Duplicate");

        var sut = new TunnelCommand(repository);
        var ctx = new CommandContext(origin, origin, ["east", "Duplicate"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("Ambiguous: 2 rooms named Duplicate were found.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsWhenOriginDirectionAlreadyOccupied()
    {
        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        var world = new World();
        var area = new Thing { Id = ThingId.New(), Name = "Area" };
        world.Register(area);
        var origin = MakeRoom(world, area, "Origin");
        var alreadyConnected = MakeRoom(world, area, "AlreadyConnected");
        var destination = MakeRoom(world, area, "Destination");
        RoomConnector.Connect(world, origin, alreadyConnected, Direction.East);

        var sut = new TunnelCommand(repository);
        var ctx = new CommandContext(origin, origin, ["east", "Destination"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        origin.Children.Count(c => c.FindBehavior<ExitBehavior>() is not null).Should().Be(1);
        await session.Received(1).WriteLineAsync("There's already an exit east from here.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsWhenDestinationOppositeDirectionAlreadyOccupied()
    {
        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        var world = new World();
        var area = new Thing { Id = ThingId.New(), Name = "Area" };
        world.Register(area);
        var origin = MakeRoom(world, area, "Origin");
        var destination = MakeRoom(world, area, "Destination");
        var somewhereElse = MakeRoom(world, area, "SomewhereElse");
        RoomConnector.Connect(world, destination, somewhereElse, Direction.West);

        var sut = new TunnelCommand(repository);
        var ctx = new CommandContext(origin, origin, ["east", "Destination"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        origin.Children.Should().BeEmpty();
        await session.Received(1).WriteLineAsync("Destination already has an exit west.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsSelfTunnel()
    {
        var repository = Substitute.For<IThingRepository>();
        var session = Substitute.For<ISession>();
        var world = new World();
        var area = new Thing { Id = ThingId.New(), Name = "Area" };
        world.Register(area);
        var origin = MakeRoom(world, area, "Origin");

        var sut = new TunnelCommand(repository);
        var ctx = new CommandContext(origin, origin, ["east", "Origin"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("You can't tunnel a room to itself.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }
}
