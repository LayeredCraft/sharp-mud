using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;

namespace SharpMud.Ruleset.Classic.Tests.Combat;

public sealed class CombatManagerTests
{
    [Fact]
    public async Task OnTickAsync_AwardsXpAndEndsEncounter_WhenPlayerDefeatsNpc()
    {
        var resolver = Substitute.For<ICombatResolver>();
        var session = Substitute.For<ISession>();
        var hubRoom = new Thing { Id = ThingId.New(), Name = "Hub" };

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        player.Behaviors.Add(new StatsBehavior());
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior { ExperienceReward = 10, CurrentHitPoints = 0 });
        room.Add(npc);

        resolver.ResolveRound(player, npc).Returns(new CombatRoundResult(true, 6, true));

        var sut = new CombatManager(resolver, hubRoom);
        sut.StartEncounter(player, npc);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        player.FindBehavior<StatsBehavior>()!.Experience.Should().Be(10);
        room.Children.Should().NotContain(npc);
        resolver.DidNotReceive().ResolveRound(npc, player);
        sut.IsInCombat(player.Id).Should().BeFalse();
    }

    [Fact]
    public async Task OnTickAsync_RespawnsPlayerWithXpLoss_WhenNpcDefeatsPlayer()
    {
        var resolver = Substitute.For<ICombatResolver>();
        var session = Substitute.For<ISession>();
        var hubRoom = new Thing { Id = ThingId.New(), Name = "Hub" };

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        player.Behaviors.Add(new StatsBehavior { Experience = 100, MaxHitPoints = 20 });
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior { ExperienceReward = 10, CurrentHitPoints = 6 });
        room.Add(npc);

        resolver.ResolveRound(player, npc).Returns(new CombatRoundResult(false, 0, false));
        resolver.ResolveRound(npc, player).Returns(new CombatRoundResult(true, 999, true));

        var sut = new CombatManager(resolver, hubRoom);
        sut.StartEncounter(player, npc);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        player.FindBehavior<StatsBehavior>()!.Experience.Should().Be(90);
        player.FindBehavior<StatsBehavior>()!.CurrentHitPoints.Should().Be(10);
        player.Parent.Should().Be(hubRoom);
        sut.IsInCombat(player.Id).Should().BeFalse();
    }
}
