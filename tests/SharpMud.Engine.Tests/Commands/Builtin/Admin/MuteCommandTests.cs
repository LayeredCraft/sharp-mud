using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class MuteCommandTests
{
    [Fact]
    public async Task ExecuteAsync_MutesAndSaves_WhenTargetIsLiveInWorld()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" });
        world.Register(admin);

        var target = new Thing { Id = ThingId.New(), Name = "Target" };
        target.Behaviors.Add(new PlayerBehavior { Username = "Target", PasswordHash = "test-hash" });
        world.Register(target);

        var sut = new MuteCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        target.FindBehavior<PlayerBehavior>()!.IsMuted.Should().BeTrue();
        await repository.Received(1).SaveTreeAsync(target, Arg.Any<CancellationToken>());
        await adminSession.Received(1).WriteLineAsync("You muted Target.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MutesAndSaves_WhenTargetIsOfflineButInRepository()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" });
        world.Register(admin);

        var target = new Thing { Id = ThingId.New(), Name = "Target" };
        target.Behaviors.Add(new PlayerBehavior { Username = "Target", PasswordHash = "test-hash" });
        repository.FindPlayerByUsernameAsync("Target", Arg.Any<CancellationToken>()).Returns(target);

        var sut = new MuteCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        target.FindBehavior<PlayerBehavior>()!.IsMuted.Should().BeTrue();
        await repository.Received(1).SaveTreeAsync(target, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsNotFoundMessage_WhenTargetDoesNotExistAnywhere()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" });
        world.Register(admin);

        var sut = new MuteCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Ghost"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await adminSession.Received(1).WriteLineAsync("No player named Ghost was found.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, Arg.Any<CancellationToken>());
    }
}
