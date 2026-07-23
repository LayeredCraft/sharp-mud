using SharpMud.Engine.Behaviors;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic.Tests;

public sealed class BasicWorldBuilderTests
{
    [Fact]
    public void Build_ReturnsWorldWithAtLeastOneFightableNpc()
    {
        var sut = new BasicWorldBuilder();

        var (world, startingRoom) = sut.Build();

        var fightableNpcs = world.AllWithBehavior<NpcBehavior>().Where(t => t.HasBehavior<CombatantBehavior>());
        fightableNpcs.Should().NotBeEmpty("a fresh character must be able to walk around and fight something");
        startingRoom.HasBehavior<RoomBehavior>().Should().BeTrue();
    }

    [Fact]
    public void FindStartingRoom_ReturnsTheClearing_AfterReload()
    {
        var sut = new BasicWorldBuilder();
        var (_, startingRoom) = sut.Build();
        var area = startingRoom.Parent!;

        var found = sut.FindStartingRoom(area);

        found.Should().Be(startingRoom);
    }
}
