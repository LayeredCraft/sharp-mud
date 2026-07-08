using SharpMud.Engine.Core;

namespace SharpMud.Engine.Tests.Core;

public sealed class ThingEventsTests
{
    private sealed class TestEvent : GameEvent;

    private sealed class TestRequest : CancellableGameEvent;

    private static Thing MakeThing(string name) => new() { Id = ThingId.New(), Name = name };

    [Fact]
    public void PublishEvent_SelfOnly_OnlyInvokesOwnersHandlers()
    {
        var parent = MakeThing("Parent");
        var child = MakeThing("Child");
        parent.Add(child);

        var parentInvoked = false;
        var childInvoked = false;
        parent.Events.SubscribeEvent((_, _) => parentInvoked = true);
        child.Events.SubscribeEvent((_, _) => childInvoked = true);

        parent.Events.PublishEvent(new TestEvent { ActiveThing = parent }, EventScope.SelfOnly);

        parentInvoked.Should().BeTrue();
        childInvoked.Should().BeFalse();
    }

    [Fact]
    public void PublishEvent_SelfDown_InvokesHandlersOnAllDescendants()
    {
        var room = MakeThing("Room");
        var item = MakeThing("Item");
        var subItem = MakeThing("SubItem");
        room.Add(item);
        item.Add(subItem);

        var invokedOn = new List<Thing>();
        room.Events.SubscribeEvent((t, _) => invokedOn.Add(t));
        item.Events.SubscribeEvent((t, _) => invokedOn.Add(t));
        subItem.Events.SubscribeEvent((t, _) => invokedOn.Add(t));

        room.Events.PublishEvent(new TestEvent { ActiveThing = room }, EventScope.SelfDown);

        invokedOn.Should().Contain([room, item, subItem]);
    }

    [Fact]
    public void PublishRequest_StopsPropagating_AssoonAsCanceled()
    {
        var room = MakeThing("Room");
        var item = MakeThing("Item");
        var subItem = MakeThing("SubItem");
        room.Add(item);
        item.Add(subItem);

        var subItemInvoked = false;
        room.Events.SubscribeRequest((_, evt) => evt.Cancel("blocked"));
        subItem.Events.SubscribeRequest((_, _) => subItemInvoked = true);

        var request = new TestRequest { ActiveThing = room };
        room.Events.PublishRequest(request, EventScope.SelfDown);

        request.IsCanceled.Should().BeTrue();
        subItemInvoked.Should().BeFalse();
    }

    [Fact]
    public void PublishRequest_NotCanceled_WhenNoHandlerCancels()
    {
        var thing = MakeThing("Thing");
        thing.Events.SubscribeRequest((_, _) => { });

        var request = new TestRequest { ActiveThing = thing };
        thing.Events.PublishRequest(request, EventScope.SelfOnly);

        request.IsCanceled.Should().BeFalse();
    }
}
