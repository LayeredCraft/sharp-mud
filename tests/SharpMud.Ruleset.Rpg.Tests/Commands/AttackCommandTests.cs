using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Ruleset.Rpg.Tests.Commands;

public sealed class AttackCommandTests
{
    [Fact]
    public async Task ExecuteAsync_StartsEncounter_WhenTargetExists()
    {
        var combatManager = Substitute.For<ICombatManager>();
        var session = Substitute.For<ISession>();
        combatManager.TryStartEncounter(Arg.Any<Thing>(), Arg.Any<Thing>()).Returns(true);

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new CombatantBehavior());
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior());
        room.Add(npc);

        var sut = new AttackCommand(combatManager);
        var ctx = new CommandContext(player, room, ["cave", "rat"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        combatManager.Received(1).TryStartEncounter(player, npc);
        await session.Received(1).WriteLineAsync("You attack cave rat!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsNotHereMessage_WhenNoMatchingCombatantInRoom()
    {
        var combatManager = Substitute.For<ICombatManager>();
        var session = Substitute.For<ISession>();

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new CombatantBehavior());
        room.Add(player);

        var sut = new AttackCommand(combatManager);
        var ctx = new CommandContext(player, room, ["dragon"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        combatManager.DidNotReceiveWithAnyArgs().TryStartEncounter(default!, default!);
        await session.Received(1).WriteLineAsync("You don't see that here.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsCannotFightMessage_WhenActorHasNoCombatantBehavior()
    {
        var combatManager = Substitute.For<ICombatManager>();
        var session = Substitute.For<ISession>();

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior());
        room.Add(npc);

        var sut = new AttackCommand(combatManager);
        var ctx = new CommandContext(player, room, ["cave", "rat"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        combatManager.DidNotReceiveWithAnyArgs().TryStartEncounter(default!, default!);
        await session.Received(1).WriteLineAsync("You have no way to fight.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsAlreadyEngagedMessage_WhenTryStartEncounterFails()
    {
        var combatManager = Substitute.For<ICombatManager>();
        var session = Substitute.For<ISession>();
        combatManager.TryStartEncounter(Arg.Any<Thing>(), Arg.Any<Thing>()).Returns(false);

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new CombatantBehavior());
        room.Add(player);

        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior());
        room.Add(npc);

        var sut = new AttackCommand(combatManager);
        var ctx = new CommandContext(player, room, ["cave", "rat"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        // TryStartEncounter is called (and is the actual source of truth for
        // this failure) rather than skipped - distinguishes this from the
        // guard-clause tests above, which never even attempt to start.
        combatManager.Received(1).TryStartEncounter(player, npc);
        await session.Received(1).WriteLineAsync("Someone else is already fighting cave rat!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsAlreadyFightingMessage_WhenActorAlreadyInCombat()
    {
        var combatManager = Substitute.For<ICombatManager>();
        var session = Substitute.For<ISession>();
        combatManager.IsInCombat(Arg.Any<ThingId>()).Returns(true);

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        room.Add(player);

        var sut = new AttackCommand(combatManager);
        var ctx = new CommandContext(player, room, ["cave", "rat"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        combatManager.DidNotReceiveWithAnyArgs().TryStartEncounter(default!, default!);
        await session.Received(1).WriteLineAsync("You are already fighting!", Arg.Any<CancellationToken>());
    }
}
