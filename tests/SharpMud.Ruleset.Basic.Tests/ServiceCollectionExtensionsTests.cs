using Microsoft.Extensions.DependencyInjection;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    // Without a real IPlayerFactory registration, LoginFlow/PlayerLogin
    // (which constructor-inject IPlayerFactory) can't create a fresh
    // CLI/Telnet player at all - the quick-start would fail at first login,
    // not just "nothing to see".
    [Fact]
    public void AddSharpMudBasicRuleset_RegistersPlayerFactory_ThatProducesAPlayableCharacter()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRandomSource>(Substitute.For<IRandomSource>());

        services.AddSharpMudBasicRuleset();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IPlayerFactory>();

        var world = new World();
        var startingRoom = new Thing { Id = ThingId.New(), Name = "Clearing" };
        startingRoom.Behaviors.Add(new RoomBehavior());

        var player = factory.CreatePlayer(world, "Adventurer", "hash", startingRoom);

        player.HasBehavior<PlayerBehavior>().Should().BeTrue();
        player.HasBehavior<BasicStatsBehavior>().Should().BeTrue();
        player.HasBehavior<CombatantBehavior>().Should().BeTrue();
    }

    [Fact]
    public void AddSharpMudBasicRuleset_AppliesConfigureOptionsCallback()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRandomSource>(Substitute.For<IRandomSource>());

        services.AddSharpMudBasicRuleset(options => options.StartingHitPoints = 42);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BasicRulesetOptions>();

        options.StartingHitPoints.Should().Be(42);
    }

    // Without this, an invalid StartingHitPoints/StartingDamageMin/Max only
    // surfaces the first time a fight actually happens - IRandomSource.Next(min, max)
    // throwing mid-combat, not a clear failure at startup.
    [Fact]
    public void AddSharpMudBasicRuleset_ThrowsAtCompositionRootTime_WhenOptionsAreInvalid()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRandomSource>(Substitute.For<IRandomSource>());

        var act = () => services.AddSharpMudBasicRuleset(options => options.StartingHitPoints = 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
