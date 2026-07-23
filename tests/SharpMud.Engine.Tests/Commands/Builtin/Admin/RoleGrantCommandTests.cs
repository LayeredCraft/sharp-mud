using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class RoleGrantCommandTests
{
    private static (Thing Admin, ISession AdminSession, World World) MakeAdmin()
    {
        var adminSession = Substitute.For<ISession>();
        var world = new World();
        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" });
        world.Register(admin);
        return (admin, adminSession, world);
    }

    [Fact]
    public async Task ExecuteAsync_GrantsRoleAndSaves_WhenTargetExistsAndRoleIsValid()
    {
        var repository = Substitute.For<IThingRepository>();
        var (admin, adminSession, world) = MakeAdmin();

        var targetBehavior = new PlayerBehavior { Username = "Target", PasswordHash = "test-hash" };
        var target = new Thing { Id = ThingId.New(), Name = "Target" };
        target.Behaviors.Add(targetBehavior);
        world.Register(target);

        var sut = new RoleGrantCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target", "MinorAdmin"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        targetBehavior.Roles.Should().HaveFlag(SecurityRole.MinorAdmin);
        await repository.Received(1).SaveTreeAsync(target, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("All")]
    [InlineData("all")]
    [InlineData("None")]
    [InlineData("none")]
    public async Task ExecuteAsync_RejectsAllAndNone_RegardlessOfCasing(string roleName)
    {
        var repository = Substitute.For<IThingRepository>();
        var (admin, adminSession, world) = MakeAdmin();

        var sut = new RoleGrantCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target", roleName], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await repository.DidNotReceiveWithAnyArgs().FindPlayerByUsernameAsync(default!, Arg.Any<CancellationToken>());
        await adminSession.Received(1).WriteLineAsync($"'{roleName}' isn't a grantable role.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownRoleName()
    {
        var repository = Substitute.For<IThingRepository>();
        var (admin, adminSession, world) = MakeAdmin();

        var sut = new RoleGrantCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target", "Wizard"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await adminSession.Received(1).WriteLineAsync("'Wizard' isn't a grantable role.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsUsageMessage_WhenMissingArguments()
    {
        var repository = Substitute.For<IThingRepository>();
        var (admin, adminSession, world) = MakeAdmin();

        var sut = new RoleGrantCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await adminSession.Received(1).WriteLineAsync("Usage: rolegrant <username> <role>", Arg.Any<CancellationToken>());
    }
}
