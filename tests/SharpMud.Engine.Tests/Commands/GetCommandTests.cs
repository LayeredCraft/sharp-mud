using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands;

public sealed class GetCommandTests
{
    [Theory, EngineAutoData]
    public async Task ExecuteAsync_MovesItemFromRoomToInventory_WhenItemExists([Frozen] ISession session)
    {
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var item = new Thing { Id = ThingId.New(), Name = "gold coin" };
        item.Behaviors.Add(new ItemBehavior());
        room.Add(item);

        var player = new Thing { Id = ThingId.New(), Name = "Adventurer" };
        player.Behaviors.Add(new PlayerBehavior { Session = session });
        room.Add(player);

        var world = new World();
        var sut = new GetCommand();
        var ctx = new CommandContext(player, room, ["gold", "coin"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        room.Children.Should().NotContain(item);
        player.Children.Should().Contain(item);
        await session.Received(1).WriteLineAsync("You get gold coin.", Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsNotFoundMessage_WhenItemIsNotInRoom([Frozen] ISession session)
    {
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = new Thing { Id = ThingId.New(), Name = "Adventurer" };
        player.Behaviors.Add(new PlayerBehavior { Session = session });
        room.Add(player);

        var world = new World();
        var sut = new GetCommand();
        var ctx = new CommandContext(player, room, ["sword"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("You don't see that here.", Arg.Any<CancellationToken>());
        player.Children.Should().BeEmpty();
    }
}
