using SharpMud.Engine.Characters;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Tests.Commands;

public sealed class WearCommandTests
{
    [Theory, EngineAutoData]
    public async Task ExecuteAsync_EquipsItemAndRemovesFromInventory_WhenItemIsWearable(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var room = new Room { Id = RoomId.New(), AreaId = AreaId.New(), Name = "Room", Description = "..." };
        var item = new Item { Id = ItemId.New(), Name = "rusty sword", Slot = EquipSlot.MainHand };

        var player = Player.CreateDefault("Adventurer", room.Id);
        player.Inventory.Add(item.Id);

        world.GetItem(item.Id).Returns(item);

        var sut = new WearCommand();
        var ctx = new CommandContext(player, room, ["rusty", "sword"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        player.Inventory.Should().NotContain(item.Id);
        player.Equipped[EquipSlot.MainHand].Should().Be(item.Id);
        await session.Received(1).WriteLineAsync("You wear rusty sword.", Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsCannotWearMessage_WhenItemHasNoSlot(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var room = new Room { Id = RoomId.New(), AreaId = AreaId.New(), Name = "Room", Description = "..." };
        var item = new Item { Id = ItemId.New(), Name = "gold coin", Slot = null };

        var player = Player.CreateDefault("Adventurer", room.Id);
        player.Inventory.Add(item.Id);

        world.GetItem(item.Id).Returns(item);

        var sut = new WearCommand();
        var ctx = new CommandContext(player, room, ["gold", "coin"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("You can't wear that.", Arg.Any<CancellationToken>());
        player.Equipped.Should().NotContainKey(EquipSlot.Head);
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SwapsPreviouslyEquippedItemBackToInventory_WhenSlotIsOccupied(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var room = new Room { Id = RoomId.New(), AreaId = AreaId.New(), Name = "Room", Description = "..." };
        var oldItem = new Item { Id = ItemId.New(), Name = "old cap", Slot = EquipSlot.Head };
        var newItem = new Item { Id = ItemId.New(), Name = "leather cap", Slot = EquipSlot.Head };

        var player = Player.CreateDefault("Adventurer", room.Id);
        player.Equipped[EquipSlot.Head] = oldItem.Id;
        player.Inventory.Add(newItem.Id);

        world.GetItem(newItem.Id).Returns(newItem);
        world.GetItem(oldItem.Id).Returns(oldItem);

        var sut = new WearCommand();
        var ctx = new CommandContext(player, room, ["leather", "cap"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        player.Equipped[EquipSlot.Head].Should().Be(newItem.Id);
        player.Inventory.Should().Contain(oldItem.Id);
        await session.Received(1).WriteLineAsync("You remove old cap.", Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync("You wear leather cap.", Arg.Any<CancellationToken>());
    }
}
