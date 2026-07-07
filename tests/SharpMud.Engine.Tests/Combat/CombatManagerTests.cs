using SharpMud.Engine.Characters;
using SharpMud.Engine.Combat;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Tests.Combat;

public sealed class CombatManagerTests
{
    [Fact]
    public async Task OnTickAsync_AwardsXpAndEndsEncounter_WhenPlayerDefeatsNpc()
    {
        var world = Substitute.For<IWorld>();
        var resolver = Substitute.For<ICombatResolver>();
        var session = Substitute.For<ISession>();

        var hubRoomId = RoomId.New();
        var player = Player.CreateDefault("Hero", RoomId.New());
        var npc = new Npc
        {
            Id = NpcId.New(),
            Name = "cave rat",
            RoomId = player.CurrentRoomId,
            MaxHitPoints = 6,
            CurrentHitPoints = 0,
            ArmorClass = 8,
            DamageMin = 1,
            DamageMax = 3,
            ExperienceReward = 10,
        };

        world.GetSession(player.Id).Returns(session);
        resolver.ResolveRound(player, npc).Returns(new CombatRoundResult(true, 6, true));

        var sut = new CombatManager(world, resolver, hubRoomId);
        sut.StartEncounter(player, npc);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        player.Experience.Should().Be(10);
        world.Received(1).RemoveNpc(npc.Id);
        resolver.DidNotReceive().ResolveRound(npc, player);
        sut.IsInCombat(player.Id).Should().BeFalse();
    }

    [Fact]
    public async Task OnTickAsync_RespawnsPlayerWithXpLoss_WhenNpcDefeatsPlayer()
    {
        var world = Substitute.For<IWorld>();
        var resolver = Substitute.For<ICombatResolver>();
        var session = Substitute.For<ISession>();

        var hubRoomId = RoomId.New();
        var hubRoom = new Room { Id = hubRoomId, AreaId = AreaId.New(), Name = "Town Square", Description = "..." };

        var player = Player.CreateDefault("Hero", RoomId.New());
        player.Experience = 100;
        player.MaxHitPoints = 20;

        var npc = new Npc
        {
            Id = NpcId.New(),
            Name = "cave rat",
            RoomId = player.CurrentRoomId,
            MaxHitPoints = 6,
            CurrentHitPoints = 6,
            ArmorClass = 8,
            DamageMin = 1,
            DamageMax = 3,
            ExperienceReward = 10,
        };

        world.GetSession(player.Id).Returns(session);
        world.GetRoom(hubRoomId).Returns(hubRoom);
        world.PlayersInRoom(hubRoomId).Returns([]);
        resolver.ResolveRound(player, npc).Returns(new CombatRoundResult(false, 0, false));
        resolver.ResolveRound(npc, player).Returns(new CombatRoundResult(true, 999, true));

        var sut = new CombatManager(world, resolver, hubRoomId);
        sut.StartEncounter(player, npc);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        player.Experience.Should().Be(90);
        player.CurrentHitPoints.Should().Be(10);
        player.CurrentRoomId.Should().Be(hubRoomId);
        sut.IsInCombat(player.Id).Should().BeFalse();
    }
}
