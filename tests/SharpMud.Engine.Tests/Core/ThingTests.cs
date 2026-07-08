using SharpMud.Engine.Core;

namespace SharpMud.Engine.Tests.Core;

public sealed class ThingTests
{
    private static Thing MakeThing(string name) => new() { Id = ThingId.New(), Name = name };

    [Fact]
    public void Add_SetsParentAndAddsToChildren()
    {
        var room = MakeThing("Room");
        var item = MakeThing("Sword");

        var result = room.Add(item);

        result.Should().BeTrue();
        item.Parent.Should().Be(room);
        room.Children.Should().Contain(item);
    }

    [Fact]
    public void Add_DetachesFromPreviousParent()
    {
        var roomA = MakeThing("A");
        var roomB = MakeThing("B");
        var item = MakeThing("Sword");
        roomA.Add(item);

        roomB.Add(item);

        item.Parent.Should().Be(roomB);
        roomA.Children.Should().NotContain(item);
        roomB.Children.Should().Contain(item);
    }

    [Fact]
    public void Remove_ClearsParentAndRemovesFromChildren()
    {
        var room = MakeThing("Room");
        var item = MakeThing("Sword");
        room.Add(item);

        var result = room.Remove(item);

        result.Should().BeTrue();
        item.Parent.Should().BeNull();
        room.Children.Should().NotContain(item);
    }

    [Fact]
    public void Remove_ReturnsFalse_WhenThingIsNotAChild()
    {
        var room = MakeThing("Room");
        var item = MakeThing("Sword");

        room.Remove(item).Should().BeFalse();
    }

    [Fact]
    public void Add_IsCanceled_WhenARequestHandlerCancels()
    {
        var room = MakeThing("Room");
        var item = MakeThing("Sword");
        room.Events.SubscribeRequest((_, evt) => evt.Cancel("no room"));

        var result = room.Add(item);

        result.Should().BeFalse();
        item.Parent.Should().BeNull();
        room.Children.Should().BeEmpty();
    }

    [Fact]
    public void FindBehavior_ReturnsAttachedBehavior()
    {
        var thing = MakeThing("Thing");
        var behavior = new TestBehavior();
        thing.Behaviors.Add(behavior);

        thing.FindBehavior<TestBehavior>().Should().Be(behavior);
        thing.HasBehavior<TestBehavior>().Should().BeTrue();
    }

    [Fact]
    public void HasBehavior_ReturnsFalse_WhenNotAttached()
    {
        var thing = MakeThing("Thing");

        thing.HasBehavior<TestBehavior>().Should().BeFalse();
    }

    private sealed class TestBehavior : Behavior;
}
