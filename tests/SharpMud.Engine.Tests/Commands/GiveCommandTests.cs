using SharpMud.Engine.Characters;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Tests.Commands;

public sealed class GiveCommandTests
{
    [Theory, EngineAutoData]
    public async Task ExecuteAsync_TransfersItemToRecipient_WhenRecipientIsInRoom(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var room = new Room { Id = RoomId.New(), AreaId = AreaId.New(), Name = "Room", Description = "..." };
        var item = new Item { Id = ItemId.New(), Name = "gold coin" };

        var giver = Player.CreateDefault("Giver", room.Id);
        giver.Inventory.Add(item.Id);
        var recipient = Player.CreateDefault("Receiver", room.Id);
        var recipientSession = Substitute.For<ISession>();

        world.GetItem(item.Id).Returns(item);
        world.PlayersInRoom(room.Id).Returns([giver, recipient]);
        world.GetSession(recipient.Id).Returns(recipientSession);

        var sut = new GiveCommand();
        var ctx = new CommandContext(giver, room, ["gold", "coin", "to", "Receiver"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        giver.Inventory.Should().NotContain(item.Id);
        recipient.Inventory.Should().Contain(item.Id);
        await session.Received(1).WriteLineAsync("You give gold coin to Receiver.", Arg.Any<CancellationToken>());
        await recipientSession.Received(1).WriteLineAsync("Giver gives you gold coin.", Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsNotHereMessage_WhenRecipientIsNotInRoom(
        [Frozen] IWorld world,
        [Frozen] ISession session)
    {
        var room = new Room { Id = RoomId.New(), AreaId = AreaId.New(), Name = "Room", Description = "..." };
        var item = new Item { Id = ItemId.New(), Name = "gold coin" };

        var giver = Player.CreateDefault("Giver", room.Id);
        giver.Inventory.Add(item.Id);

        world.GetItem(item.Id).Returns(item);
        world.PlayersInRoom(room.Id).Returns([giver]);

        var sut = new GiveCommand();
        var ctx = new CommandContext(giver, room, ["gold", "coin", "to", "Nobody"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("They aren't here.", Arg.Any<CancellationToken>());
        giver.Inventory.Should().Contain(item.Id);
    }
}
