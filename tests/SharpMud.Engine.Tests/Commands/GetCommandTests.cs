using SharpMud.Engine.Characters;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Tests.Commands;

public sealed class GetCommandTests
{
    [Theory, EngineAutoData]
    public async Task ExecuteAsync_MovesItemFromRoomToInventory_WhenItemExists(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var room = new Room { Id = RoomId.New(), AreaId = AreaId.New(), Name = "Room", Description = "..." };
        var item = new Item { Id = ItemId.New(), Name = "gold coin" };
        room.ItemsOnGround.Add(item.Id);

        var player = Player.CreateDefault("Adventurer", room.Id);

        world.GetItem(item.Id).Returns(item);
        world.PlayersInRoom(room.Id).Returns([]);

        var sut = new GetCommand();
        var ctx = new CommandContext(player, room, ["gold", "coin"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        room.ItemsOnGround.Should().NotContain(item.Id);
        player.Inventory.Should().Contain(item.Id);
        await session.Received(1).WriteLineAsync("You get gold coin.", Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsNotFoundMessage_WhenItemIsNotInRoom(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var room = new Room { Id = RoomId.New(), AreaId = AreaId.New(), Name = "Room", Description = "..." };
        var player = Player.CreateDefault("Adventurer", room.Id);

        var sut = new GetCommand();
        var ctx = new CommandContext(player, room, ["sword"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("You don't see that here.", Arg.Any<CancellationToken>());
        player.Inventory.Should().BeEmpty();
    }
}
