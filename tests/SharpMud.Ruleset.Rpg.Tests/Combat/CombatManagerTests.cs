using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;

namespace SharpMud.Ruleset.Rpg.Tests.Combat;

public sealed class CombatManagerTests
{
    [Fact]
    public async Task OnTickAsync_NotifiesOutcomeHandlerAndEndsEncounter_WhenPlayerDefeatsNpc()
    {
        var resolver = Substitute.For<ICombatResolver>();
        var outcomeHandler = Substitute.For<ICombatOutcomeHandler>();
        var session = Substitute.For<ISession>();

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior { ExperienceReward = 10, CurrentHitPoints = 0 });
        room.Add(npc);

        resolver.ResolveRound(player, npc).Returns(new CombatRoundResult(true, 6, true));

        var sut = new CombatManager(resolver, outcomeHandler);
        sut.StartEncounter(player, npc);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        await outcomeHandler.Received(1).OnVictoryAsync(player, npc, TestContext.Current.CancellationToken);
        room.Children.Should().NotContain(npc);
        resolver.DidNotReceive().ResolveRound(npc, player);
        sut.IsInCombat(player.Id).Should().BeFalse();
    }

    [Fact]
    public async Task OnTickAsync_ResetsCombatantHitPointsAndRespawnsAtHandlerDestination_WhenNpcDefeatsPlayer()
    {
        var resolver = Substitute.For<ICombatResolver>();
        var outcomeHandler = Substitute.For<ICombatOutcomeHandler>();
        var session = Substitute.For<ISession>();
        var hubRoom = new Thing { Id = ThingId.New(), Name = "Hub" };

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        player.Behaviors.Add(new CombatantBehavior { MaxHitPoints = 20, CurrentHitPoints = -5 });
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior { ExperienceReward = 10, CurrentHitPoints = 6 });
        room.Add(npc);

        resolver.ResolveRound(player, npc).Returns(new CombatRoundResult(false, 0, false));
        resolver.ResolveRound(npc, player).Returns(new CombatRoundResult(true, 999, true));
        outcomeHandler.OnDefeatAsync(player, npc, TestContext.Current.CancellationToken).Returns(hubRoom);

        var sut = new CombatManager(resolver, outcomeHandler);
        sut.StartEncounter(player, npc);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        // Regression coverage for the pre-existing bug: a respawned
        // character's CombatantBehavior.CurrentHitPoints must actually
        // reset, not stay at/below 0 and instantly re-trigger "defeated" on
        // the next hit.
        player.FindBehavior<CombatantBehavior>()!.CurrentHitPoints.Should().Be(20);
        player.Parent.Should().Be(hubRoom);
        sut.IsInCombat(player.Id).Should().BeFalse();
    }

    [Fact]
    public async Task OnTickAsync_FreezesEncounter_WhenAttackerLinkdeadWithinGraceWindow()
    {
        var resolver = Substitute.For<ICombatResolver>();
        var outcomeHandler = Substitute.For<ICombatOutcomeHandler>();
        var session = Substitute.For<ISession>();

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session };
        playerBehavior.EnterLinkdead(DateTimeOffset.UtcNow);
        player.Behaviors.Add(playerBehavior);
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior { ExperienceReward = 10, CurrentHitPoints = 6 });
        room.Add(npc);

        var sut = new CombatManager(resolver, outcomeHandler);
        sut.StartEncounter(player, npc);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        sut.IsInCombat(player.Id).Should().BeTrue();
        resolver.DidNotReceiveWithAnyArgs().ResolveRound(default!, default!);
        await session.DidNotReceiveWithAnyArgs().WriteLineAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnTickAsync_AbandonsEncounter_WhenAttackerLinkdeadPastGraceWindow()
    {
        var resolver = Substitute.For<ICombatResolver>();
        var outcomeHandler = Substitute.For<ICombatOutcomeHandler>();
        var session = Substitute.For<ISession>();

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        var playerBehavior = new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session };
        playerBehavior.EnterLinkdead(DateTimeOffset.UtcNow - ReconnectPolicy.GraceWindow - TimeSpan.FromSeconds(1));
        player.Behaviors.Add(playerBehavior);
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior { ExperienceReward = 10, CurrentHitPoints = 6 });
        room.Add(npc);

        var sut = new CombatManager(resolver, outcomeHandler);
        sut.StartEncounter(player, npc);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        sut.IsInCombat(player.Id).Should().BeFalse();
    }
}
