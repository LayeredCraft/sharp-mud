using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic.Tests;

public sealed class BasicPlayerFactoryTests
{
    [Fact]
    public void CreatePlayer_ReturnsThingWithPlayerAndBasicStatsAndCombatantBehaviors()
    {
        var options = new BasicRulesetOptions { StartingHitPoints = 15, StartingArmorClass = 9, StartingDamageMin = 2, StartingDamageMax = 5 };
        var sut = new BasicPlayerFactory(options);
        var world = new World();
        var startingRoom = new Thing { Id = ThingId.New(), Name = "Clearing" };
        startingRoom.Behaviors.Add(new RoomBehavior());

        var player = sut.CreatePlayer(world, "Adventurer", "hash", startingRoom);

        player.HasBehavior<PlayerBehavior>().Should().BeTrue();
        player.HasBehavior<BasicStatsBehavior>().Should().BeTrue();

        var combatant = player.FindBehavior<CombatantBehavior>();
        combatant.Should().NotBeNull();
        combatant!.MaxHitPoints.Should().Be(15);
        combatant.ArmorClass.Should().Be(9);
        combatant.DamageMin.Should().Be(2);
        combatant.DamageMax.Should().Be(5);

        player.Parent.Should().Be(startingRoom);
        world.GetThing(player.Id).Should().Be(player);
    }
}
