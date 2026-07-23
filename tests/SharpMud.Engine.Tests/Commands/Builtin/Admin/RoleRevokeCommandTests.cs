using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class RoleRevokeCommandTests
{
    [Fact]
    public async Task ExecuteAsync_RejectsRevokingOwnFullAdmin()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var adminBehavior = new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" };
        adminBehavior.GrantRole(SecurityRole.FullAdmin);
        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(adminBehavior);
        world.Register(admin);

        var sut = new RoleRevokeCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Admin", "FullAdmin"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        adminBehavior.Roles.Should().HaveFlag(SecurityRole.FullAdmin);
        await adminSession.Received(1).WriteLineAsync("You cannot revoke your own FullAdmin.", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsRevokingADifferentRoleFromSelf()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var adminBehavior = new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" };
        adminBehavior.GrantRole(SecurityRole.Helper);
        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(adminBehavior);
        world.Register(admin);

        var sut = new RoleRevokeCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Admin", "Helper"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        adminBehavior.Roles.Should().NotHaveFlag(SecurityRole.Helper);
        await repository.Received(1).SaveTreeAsync(admin, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AllowsRevokingFullAdminFromSomeoneElse()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" });
        world.Register(admin);

        var targetBehavior = new PlayerBehavior { Username = "Target", PasswordHash = "test-hash" };
        targetBehavior.GrantRole(SecurityRole.FullAdmin);
        var target = new Thing { Id = ThingId.New(), Name = "Target" };
        target.Behaviors.Add(targetBehavior);
        world.Register(target);

        var sut = new RoleRevokeCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target", "FullAdmin"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        targetBehavior.Roles.Should().NotHaveFlag(SecurityRole.FullAdmin);
        await repository.Received(1).SaveTreeAsync(target, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RelaysHierarchyFailure_WithoutSaving()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" });
        world.Register(admin);

        var targetBehavior = new PlayerBehavior { Username = "Target", PasswordHash = "test-hash" };
        targetBehavior.GrantRole(SecurityRole.FullAdmin);
        var target = new Thing { Id = ThingId.New(), Name = "Target" };
        target.Behaviors.Add(targetBehavior);
        world.Register(target);

        var sut = new RoleRevokeCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target", "MinorAdmin"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        targetBehavior.Roles.Should().HaveFlag(SecurityRole.MinorAdmin);
        await repository.DidNotReceiveWithAnyArgs().SaveTreeAsync(default!, default);
        await adminSession.Received(1).WriteLineAsync(
            Arg.Is<string>(s => s!.Contains("FullAdmin")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAllAndNoneRoleNames()
    {
        var repository = Substitute.For<IThingRepository>();
        var adminSession = Substitute.For<ISession>();
        var world = new World();

        var admin = new Thing { Id = ThingId.New(), Name = "Admin" };
        admin.Behaviors.Add(new PlayerBehavior { Username = "Admin", PasswordHash = "test-hash" });
        world.Register(admin);

        var sut = new RoleRevokeCommand(repository);
        var ctx = new CommandContext(admin, admin, ["Target", "All"], world, adminSession);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await adminSession.Received(1).WriteLineAsync("'All' isn't a revocable role.", Arg.Any<CancellationToken>());
    }
}
