using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Ticking;

namespace SharpMud.Engine.Tests.Behaviors;

public sealed class WanderManagerTests
{
    private static Thing MakeRoom(string name) => new() { Id = ThingId.New(), Name = name };

    private static Thing Connect(World world, Thing a, Thing b, Direction direction)
    {
        var exit = new Thing { Id = ThingId.New(), Name = direction.ToDisplayString() };
        exit.Behaviors.Add(new ExitBehavior { Direction = direction, Destination = b });
        a.Add(exit);
        world.Register(exit);
        return exit;
    }

    [Fact]
    public async Task OnTickAsync_MovesNpcToAdjacentRoom_WhenWanderRollSucceeds()
    {
        var world = new World();
        var origin = MakeRoom("Origin");
        var destination = MakeRoom("Destination");
        world.Register(origin);
        world.Register(destination);
        Connect(world, origin, destination, Direction.North);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new WanderingBehavior { WanderChancePercent = 100 });
        origin.Add(npc);
        world.Register(npc);

        var random = Substitute.For<IRandomSource>();
        random.Next(1, 100).Returns(1); // always within the wander chance
        random.Next(0, 0).Returns(0); // only one exit available

        var sut = new WanderManager(world, random);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        npc.Parent.Should().Be(destination);
        origin.Children.Should().NotContain(npc);
    }

    [Fact]
    public async Task OnTickAsync_DoesNotMoveNpc_WhenWanderRollFails()
    {
        var world = new World();
        var origin = MakeRoom("Origin");
        var destination = MakeRoom("Destination");
        world.Register(origin);
        world.Register(destination);
        Connect(world, origin, destination, Direction.North);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new WanderingBehavior { WanderChancePercent = 25 });
        origin.Add(npc);
        world.Register(npc);

        var random = Substitute.For<IRandomSource>();
        random.Next(1, 100).Returns(99); // above the wander chance

        var sut = new WanderManager(world, random);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        npc.Parent.Should().Be(origin);
    }

    [Fact]
    public async Task OnTickAsync_DoesNothing_WhenNpcHasNoExits()
    {
        var world = new World();
        var origin = MakeRoom("Origin");
        world.Register(origin);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new WanderingBehavior { WanderChancePercent = 100 });
        origin.Add(npc);
        world.Register(npc);

        var random = Substitute.For<IRandomSource>();
        random.Next(1, 100).Returns(1);

        var sut = new WanderManager(world, random);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        npc.Parent.Should().Be(origin);
    }

    [Fact]
    public async Task OnTickAsync_IgnoresThingsWithoutWanderingBehavior()
    {
        var world = new World();
        var origin = MakeRoom("Origin");
        var stationaryNpc = new Thing { Id = ThingId.New(), Name = "statue" };
        origin.Add(stationaryNpc);
        world.Register(origin);
        world.Register(stationaryNpc);

        var random = Substitute.For<IRandomSource>();

        var sut = new WanderManager(world, random);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        stationaryNpc.Parent.Should().Be(origin);
        random.DidNotReceive().Next(Arg.Any<int>(), Arg.Any<int>());
    }
}
