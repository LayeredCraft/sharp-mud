using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands;

public sealed class GiveCommandTests
{
    [Theory, EngineAutoData]
    public async Task ExecuteAsync_TransfersItemToRecipient_WhenRecipientIsInRoom([Frozen] ISession session)
    {
        var room = new Thing { Id = ThingId.New(), Name = "Room" };

        var giver = new Thing { Id = ThingId.New(), Name = "Giver" };
        giver.Behaviors.Add(new PlayerBehavior { Session = session });
        room.Add(giver);

        var item = new Thing { Id = ThingId.New(), Name = "gold coin" };
        item.Behaviors.Add(new ItemBehavior());
        giver.Add(item);

        var recipientSession = Substitute.For<ISession>();
        var recipient = new Thing { Id = ThingId.New(), Name = "Receiver" };
        recipient.Behaviors.Add(new PlayerBehavior { Session = recipientSession });
        room.Add(recipient);

        var world = new World();
        var sut = new GiveCommand();
        var ctx = new CommandContext(giver, room, ["gold", "coin", "to", "Receiver"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        giver.Children.Should().NotContain(item);
        recipient.Children.Should().Contain(item);
        await session.Received(1).WriteLineAsync("You give gold coin to Receiver.", Arg.Any<CancellationToken>());
        await recipientSession.Received(1).WriteLineAsync("Giver gives you gold coin.", Arg.Any<CancellationToken>());
    }

    [Theory, EngineAutoData]
    public async Task ExecuteAsync_SendsNotHereMessage_WhenRecipientIsNotInRoom([Frozen] ISession session)
    {
        var room = new Thing { Id = ThingId.New(), Name = "Room" };

        var giver = new Thing { Id = ThingId.New(), Name = "Giver" };
        giver.Behaviors.Add(new PlayerBehavior { Session = session });
        room.Add(giver);

        var item = new Thing { Id = ThingId.New(), Name = "gold coin" };
        item.Behaviors.Add(new ItemBehavior());
        giver.Add(item);

        var world = new World();
        var sut = new GiveCommand();
        var ctx = new CommandContext(giver, room, ["gold", "coin", "to", "Nobody"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("They aren't here.", Arg.Any<CancellationToken>());
        giver.Children.Should().Contain(item);
    }
}
