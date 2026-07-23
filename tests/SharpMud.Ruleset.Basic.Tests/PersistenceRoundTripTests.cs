using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Persistence;
using SharpMud.Ruleset.Basic.Tests.TestKit;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic.Tests;

public sealed class PersistenceRoundTripTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ThingRepository _sut;

    public PersistenceRoundTripTests()
    {
        _sut = new ThingRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    // Without BasicBehaviorMappingContributor actually being registered and
    // discovered, a Basic world/player carrying BasicStatsBehavior hits an
    // unmapped TPH discriminator subtype - only a real save/load round trip
    // catches this, not a unit test against the behavior alone.
    [Fact]
    public async Task SaveTreeAsync_ThenLoadTreeAsync_RoundTripsPlayerWithBasicStatsAndCombatant()
    {
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash" });
        player.Behaviors.Add(new BasicStatsBehavior { Level = 2, Experience = 150 });
        player.Behaviors.Add(new CombatantBehavior { MaxHitPoints = 20, CurrentHitPoints = 12, ArmorClass = 10 });

        await _sut.SaveTreeAsync(player, TestContext.Current.CancellationToken);

        var loaded = await _sut.LoadTreeAsync(player.Id, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        var stats = loaded!.FindBehavior<BasicStatsBehavior>();
        stats.Should().NotBeNull();
        stats!.Level.Should().Be(2);
        stats.Experience.Should().Be(150);

        var combatant = loaded.FindBehavior<CombatantBehavior>();
        combatant.Should().NotBeNull();
        combatant!.CurrentHitPoints.Should().Be(12);
    }
}
