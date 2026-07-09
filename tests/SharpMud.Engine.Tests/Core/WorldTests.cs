using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Tests.Core;

public sealed class WorldTests
{
    private readonly World _sut = new();

    [Fact]
    public void GetThing_ReturnsRegisteredThing()
    {
        var thing = new Thing { Id = ThingId.New(), Name = "Sword" };
        _sut.Register(thing);

        _sut.GetThing(thing.Id).Should().Be(thing);
    }

    [Fact]
    public void GetThing_ReturnsNull_WhenNotRegistered()
    {
        _sut.GetThing(ThingId.New()).Should().BeNull();
    }

    [Fact]
    public void Unregister_RemovesThing()
    {
        var thing = new Thing { Id = ThingId.New(), Name = "Sword" };
        _sut.Register(thing);

        _sut.Unregister(thing.Id);

        _sut.GetThing(thing.Id).Should().BeNull();
    }

    [Fact]
    public void AllWithBehavior_ReturnsOnlyMatchingThings()
    {
        var player = new Thing { Id = ThingId.New(), Name = "Player" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash" });
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        room.Behaviors.Add(new RoomBehavior());

        _sut.Register(player);
        _sut.Register(room);

        _sut.AllWithBehavior<PlayerBehavior>().Should().ContainSingle().Which.Should().Be(player);
    }
}
