using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands;

public sealed class WearCommandTests
{
    private static Thing MakePlayer(ISession session)
    {
        var player = new Thing { Id = ThingId.New(), Name = "Adventurer" };
        player.Behaviors.Add(new PlayerBehavior { Session = session });
        player.Behaviors.Add(new EquippedBehavior());
        return player;
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_EquipsItemAndRemovesFromCarried_WhenItemIsWearable([Frozen] ISession session)
    {
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = MakePlayer(session);
        room.Add(player);

        var item = new Thing { Id = ThingId.New(), Name = "rusty sword" };
        item.Behaviors.Add(new ItemBehavior());
        item.Behaviors.Add(new WearableBehavior { Slot = EquipSlot.MainHand });
        player.Add(item);

        var world = new World();
        var sut = new WearCommand();
        var ctx = new CommandContext(player, room, ["rusty", "sword"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        player.FindBehavior<EquippedBehavior>()!.Equipped[EquipSlot.MainHand].Should().Be(item);
        CarriedItems.Of(player).Should().NotContain(item);
        await session.Received(1).WriteLineAsync("You wear rusty sword.", Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsCannotWearMessage_WhenItemHasNoWearableBehavior([Frozen] ISession session)
    {
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = MakePlayer(session);
        room.Add(player);

        var item = new Thing { Id = ThingId.New(), Name = "gold coin" };
        item.Behaviors.Add(new ItemBehavior());
        player.Add(item);

        var world = new World();
        var sut = new WearCommand();
        var ctx = new CommandContext(player, room, ["gold", "coin"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("You can't wear that.", Arg.Any<CancellationToken>());
        player.FindBehavior<EquippedBehavior>()!.Equipped.Should().NotContainKey(EquipSlot.Head);
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SwapsPreviouslyEquippedItemBackToCarried_WhenSlotIsOccupied([Frozen] ISession session)
    {
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = MakePlayer(session);
        room.Add(player);

        var oldItem = new Thing { Id = ThingId.New(), Name = "old cap" };
        oldItem.Behaviors.Add(new ItemBehavior());
        oldItem.Behaviors.Add(new WearableBehavior { Slot = EquipSlot.Head });
        player.Add(oldItem);
        player.FindBehavior<EquippedBehavior>()!.Equipped[EquipSlot.Head] = oldItem;

        var newItem = new Thing { Id = ThingId.New(), Name = "leather cap" };
        newItem.Behaviors.Add(new ItemBehavior());
        newItem.Behaviors.Add(new WearableBehavior { Slot = EquipSlot.Head });
        player.Add(newItem);

        var world = new World();
        var sut = new WearCommand();
        var ctx = new CommandContext(player, room, ["leather", "cap"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        player.FindBehavior<EquippedBehavior>()!.Equipped[EquipSlot.Head].Should().Be(newItem);
        CarriedItems.Of(player).Should().Contain(oldItem);
        await session.Received(1).WriteLineAsync("You remove old cap.", Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync("You wear leather cap.", Arg.Any<CancellationToken>());
    }
}
