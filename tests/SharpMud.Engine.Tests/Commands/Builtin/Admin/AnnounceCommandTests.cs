using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class AnnounceCommandTests
{
    [Fact]
    public async Task ExecuteAsync_BroadcastsToEveryOnlinePlayer_ButNotLinkdeadOrSessionlessOnes()
    {
        var adminSession = Substitute.For<ISession>();
        adminSession.IsConnected.Returns(true);
        var onlineSession = Substitute.For<ISession>();
        onlineSession.IsConnected.Returns(true);
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash", Session = adminSession });
        world.Register(admin);

        var online = new Thing { Id = ThingId.New(), Name = "Online" };
        online.Behaviors.Add(new PlayerBehavior { Username = "Online", PasswordHash = "test-hash", Session = onlineSession });
        world.Register(online);

        var linkdeadBehavior = new PlayerBehavior { Username = "Linkdead", PasswordHash = "test-hash" };
        linkdeadBehavior.EnterLinkdead(DateTimeOffset.UtcNow);
        var linkdead = new Thing { Id = ThingId.New(), Name = "Linkdead" };
        linkdead.Behaviors.Add(linkdeadBehavior);
        world.Register(linkdead);

        var sessionless = new Thing { Id = ThingId.New(), Name = "Sessionless" };
        sessionless.Behaviors.Add(new PlayerBehavior { Username = "Sessionless", PasswordHash = "test-hash" });
        world.Register(sessionless);

        var sut = new AnnounceCommand();
        var ctx = new CommandContext(admin, admin, ["Server", "restarting", "soon"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await adminSession.Received(1).WriteLineAsync("[Announcement] Server restarting soon", Arg.Any<CancellationToken>());
        await onlineSession.Received(1).WriteLineAsync("[Announcement] Server restarting soon", Arg.Any<CancellationToken>());
    }
}
