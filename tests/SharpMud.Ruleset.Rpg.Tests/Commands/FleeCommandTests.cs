using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Ruleset.Rpg.Tests.Commands;

public sealed class FleeCommandTests
{
    [Fact]
    public async Task ExecuteAsync_MovesActorAndEndsEncounter_WhenRollSucceeds()
    {
        var combatManager = Substitute.For<ICombatManager>();
        var dice = Substitute.For<IDiceRoller>();
        var random = Substitute.For<IRandomSource>();
        var session = Substitute.For<ISession>();
        var encounter = new CombatEncounter { Attacker = new Thing { Id = ThingId.New(), Name = "x" }, Defender = new Thing { Id = ThingId.New(), Name = "y" } };
        combatManager.TryGetEncounter(Arg.Any<ThingId>(), out Arg.Any<CombatEncounter?>())
            .Returns(x => { x[1] = encounter; return true; });
        dice.Roll(1, 100).Returns(1);
        random.Next(0, 0).Returns(0);

        var origin = new Thing { Id = ThingId.New(), Name = "Origin" };
        var destination = new Thing { Id = ThingId.New(), Name = "Destination", Description = "A quiet place." };
        var exit = new Thing { Id = ThingId.New(), Name = "north" };
        exit.Behaviors.Add(new ExitBehavior { Direction = Direction.North, Destination = destination });
        origin.Add(exit);

        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        origin.Add(player);

        var sut = new FleeCommand(combatManager, dice, random);
        var ctx = new CommandContext(player, origin, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        combatManager.Received(1).EndEncounter(player.Id);
        player.Parent.Should().Be(destination);
        origin.Children.Should().NotContain(player);
    }

    [Fact]
    public async Task ExecuteAsync_SendsFailureMessage_WhenRollFails()
    {
        var combatManager = Substitute.For<ICombatManager>();
        var dice = Substitute.For<IDiceRoller>();
        var random = Substitute.For<IRandomSource>();
        var session = Substitute.For<ISession>();
        var encounter = new CombatEncounter { Attacker = new Thing { Id = ThingId.New(), Name = "x" }, Defender = new Thing { Id = ThingId.New(), Name = "y" } };
        combatManager.TryGetEncounter(Arg.Any<ThingId>(), out Arg.Any<CombatEncounter?>())
            .Returns(x => { x[1] = encounter; return true; });
        dice.Roll(1, 100).Returns(100);

        var origin = new Thing { Id = ThingId.New(), Name = "Origin" };
        var destination = new Thing { Id = ThingId.New(), Name = "Destination" };
        var exit = new Thing { Id = ThingId.New(), Name = "north" };
        exit.Behaviors.Add(new ExitBehavior { Direction = Direction.North, Destination = destination });
        origin.Add(exit);

        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        origin.Add(player);

        var sut = new FleeCommand(combatManager, dice, random);
        var ctx = new CommandContext(player, origin, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        combatManager.DidNotReceiveWithAnyArgs().EndEncounter(default!);
        player.Parent.Should().Be(origin);
        await session.Received(1).WriteLineAsync("You fail to escape!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsNotFightingMessage_WhenNoActiveEncounter()
    {
        var combatManager = Substitute.For<ICombatManager>();
        var dice = Substitute.For<IDiceRoller>();
        var random = Substitute.For<IRandomSource>();
        var session = Substitute.For<ISession>();
        combatManager.TryGetEncounter(Arg.Any<ThingId>(), out Arg.Any<CombatEncounter?>()).Returns(false);

        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        room.Add(player);

        var sut = new FleeCommand(combatManager, dice, random);
        var ctx = new CommandContext(player, room, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("You aren't fighting anything.", Arg.Any<CancellationToken>());
    }
}
