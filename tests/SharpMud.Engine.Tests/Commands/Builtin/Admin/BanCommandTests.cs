using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class BanCommandTests
{
    [Fact]
    public async Task ExecuteAsync_RejectsSelfTargeting_AndLeavesIsBannedUnchanged()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var adminBehavior = new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash", Session = adminSession };
        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(adminBehavior);
        world.Register(admin);

        var sut = new BanCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Admin"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        adminBehavior.IsBanned.Should().BeFalse();
        await adminSession.Received(1).WriteLineAsync("You cannot ban yourself.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_BansAndDisconnects_WhenTargetingAnotherOnlinePlayer()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var targetSession = Substitute.For<ISession>();
        targetSession.IsConnected.Returns(true);
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash", Session = adminSession });
        world.Register(admin);

        var targetBehavior = new PlayerBehavior { Username = "Target", PasswordHash = "test-hash", Session = targetSession };
        var target = new Thing { Id = ThingId.New(), Name = "Target" };
        target.Behaviors.Add(targetBehavior);
        world.Register(target);

        var sut = new BanCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        targetBehavior.IsBanned.Should().BeTrue();
        targetBehavior.WasBooted.Should().BeTrue();
        await targetSession.Received(1).DisconnectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await repository.Received(1).SaveTreeAsync(target, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_BansWithoutDisconnecting_WhenTargetIsOffline()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash", Session = adminSession });
        world.Register(admin);

        var targetBehavior = new PlayerBehavior { Username = "Target", PasswordHash = "test-hash" };
        var target = new Thing { Id = ThingId.New(), Name = "Target" };
        target.Behaviors.Add(targetBehavior);
        repository.FindPlayerByUsernameAsync("Target", Arg.Any<CancellationToken>()).Returns(target);

        var sut = new BanCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        targetBehavior.IsBanned.Should().BeTrue();
        targetBehavior.WasBooted.Should().BeFalse();
        await repository.Received(1).SaveTreeAsync(target, Arg.Any<CancellationToken>());
    }
}
