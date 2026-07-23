using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Tests.Behaviors;

public sealed class RoomConnectorTests
{
    [Fact]
    public void Connect_WiresBothDirectionsAndRegistersBothExits()
    {
        var world = new World();
        var a = new Thing { Id = ThingId.New(), Name = "A" };
        var b = new Thing { Id = ThingId.New(), Name = "B" };

        RoomConnector.Connect(world, a, b, Direction.North);

        var aToB = a.Children.Select(c => c.FindBehavior<ExitBehavior>()).Single(e => e is not null);
        aToB!.Direction.Should().Be(Direction.North);
        aToB.Destination.Should().Be(b);

        var bToA = b.Children.Select(c => c.FindBehavior<ExitBehavior>()).Single(e => e is not null);
        bToA!.Direction.Should().Be(Direction.South);
        bToA.Destination.Should().Be(a);

        world.GetThing(aToB.Parent!.Id).Should().Be(aToB.Parent);
        world.GetThing(bToA.Parent!.Id).Should().Be(bToA.Parent);
    }
}
