using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class UnbanCommandTests
{
    [Fact]
    public async Task ExecuteAsync_UnbansAndSaves_WhenTargetExists()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" });
        world.Register(admin);

        var targetBehavior = new PlayerBehavior { Username = "Target", PasswordHash = "test-hash" };
        targetBehavior.Ban();
        var target = new Thing { Id = ThingId.New(), Name = "Target" };
        target.Behaviors.Add(targetBehavior);
        repository.FindPlayerByUsernameAsync("Target", Arg.Any<CancellationToken>()).Returns(target);

        var sut = new UnbanCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        targetBehavior.IsBanned.Should().BeFalse();
        await repository.Received(1).SaveTreeAsync(target, Arg.Any<CancellationToken>());
        await adminSession.Received(1).WriteLineAsync("You unbanned Target.", Arg.Any<CancellationToken>());
    }
}
