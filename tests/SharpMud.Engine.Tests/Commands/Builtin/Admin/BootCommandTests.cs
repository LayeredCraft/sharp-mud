using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class BootCommandTests
{
    private static Thing MakePlayer(string username, ISession? session, ConnectionState? connectionState = null)
    {
        var thing = new Thing { Id = ThingId.New(), Name = username };
        var behavior = new PlayerBehavior { Username = username, PasswordHash = "test-hash", Session = session };
        if (connectionState == ConnectionState.Linkdead)
            behavior.EnterLinkdead(DateTimeOffset.UtcNow);
        thing.Behaviors.Add(behavior);
        return thing;
    }

    [Fact]
    public async Task ExecuteAsync_MarksBootedAndDisconnects_WhenTargetIsOnline()
    {
        var adminSession = Substitute.For<ISession>();
        var targetSession = Substitute.For<ISession>();
        targetSession.IsConnected.Returns(true);

        var world = new World();
        var admin = MakePlayer("Admin", adminSession);
        var target = MakePlayer("Target", targetSession);
        world.Register(admin);
        world.Register(target);

        var sut = new BootCommand();
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        target.FindBehavior<PlayerBehavior>()!.WasBooted.Should().BeTrue();
        await targetSession.Received(1).DisconnectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await adminSession.Received(1).WriteLineAsync("You booted Target.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsNotOnlineMessage_WhenTargetIsLinkdead()
    {
        var adminSession = Substitute.For<ISession>();
        var targetSession = Substitute.For<ISession>();

        var world = new World();
        var admin = MakePlayer("Admin", adminSession);
        var target = MakePlayer("Target", targetSession, ConnectionState.Linkdead);
        world.Register(admin);
        world.Register(target);

        var sut = new BootCommand();
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        target.FindBehavior<PlayerBehavior>()!.WasBooted.Should().BeFalse();
        await targetSession.DidNotReceiveWithAnyArgs().DisconnectAsync(default!, Arg.Any<CancellationToken>());
        await adminSession.Received(1).WriteLineAsync("Target is not online.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsNotOnlineMessage_WhenTargetHasNoSession()
    {
        var adminSession = Substitute.For<ISession>();

        var world = new World();
        var admin = MakePlayer("Admin", adminSession);
        var target = MakePlayer("Target", session: null);
        world.Register(admin);
        world.Register(target);

        var sut = new BootCommand();
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await adminSession.Received(1).WriteLineAsync("Target is not online.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsNotOnlineMessage_WhenTargetDoesNotExist()
    {
        var adminSession = Substitute.For<ISession>();
        var world = new World();
        var admin = MakePlayer("Admin", adminSession);
        world.Register(admin);

        var sut = new BootCommand();
        var ctx = new CommandContext(admin, admin, ["Ghost"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await adminSession.Received(1).WriteLineAsync("Ghost is not online.", Arg.Any<CancellationToken>());
    }
}
