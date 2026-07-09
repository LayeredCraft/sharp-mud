using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands;

public sealed class MoveCommandTests
{
    private static Thing MakeRoom(string name) => new()
    {
        Id = ThingId.New(),
        Name = name,
        Description = $"{name} description.",
    };

    private static Thing MakePlayer(string name, ISession session)
    {
        var player = new Thing { Id = ThingId.New(), Name = name };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        return player;
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_MovesPlayerAndShowsDestination_WhenExitExists([Frozen] ISession session)
    {
        var origin = MakeRoom("Town Square");
        var destination = MakeRoom("Market Street");
        var exit = new Thing { Id = ThingId.New(), Name = "north" };
        exit.Behaviors.Add(new ExitBehavior { Direction = Direction.North, Destination = destination });
        origin.Add(exit);

        var player = MakePlayer("Adventurer", session);
        origin.Add(player);

        var world = new World();
        var sut = new MoveCommand(Direction.North, "north", ["n"]);
        var ctx = new CommandContext(player, origin, [], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        player.Parent.Should().Be(destination);
        origin.Children.Should().NotContain(player);
        await session.Received(1).WriteLineAsync(destination.Name, Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsCannotGoMessage_WhenNoExitExists([Frozen] ISession session)
    {
        var origin = MakeRoom("Town Square");
        var player = MakePlayer("Adventurer", session);
        origin.Add(player);

        var world = new World();
        var sut = new MoveCommand(Direction.North, "north", ["n"]);
        var ctx = new CommandContext(player, origin, [], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("You can't go that way.", Arg.Any<CancellationToken>());
        player.Parent.Should().Be(origin);
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsLockedMessage_WhenExitIsLocked([Frozen] ISession session)
    {
        var origin = MakeRoom("Town Square");
        var destination = MakeRoom("Market Street");
        var exit = new Thing { Id = ThingId.New(), Name = "north" };
        exit.Behaviors.Add(new ExitBehavior { Direction = Direction.North, Destination = destination });
        exit.Behaviors.Add(new LockableBehavior { IsLocked = true });
        origin.Add(exit);

        var player = MakePlayer("Adventurer", session);
        origin.Add(player);

        var world = new World();
        var sut = new MoveCommand(Direction.North, "north", ["n"]);
        var ctx = new CommandContext(player, origin, [], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("The door is locked.", Arg.Any<CancellationToken>());
        player.Parent.Should().Be(origin);
    }
}
