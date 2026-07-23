using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands;

public sealed class RoleGuardedCommandTests
{
    private sealed class FakeCommand : ICommand
    {
        public bool WasExecuted { get; private set; }
        public string Verb => "ban";
        public IReadOnlyList<string> Aliases { get; } = ["b"];

        public Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
        {
            WasExecuted = true;
            return Task.CompletedTask;
        }
    }

    private static Thing MakeActor(SecurityRole roles)
    {
        var actor = new Thing { Id = ThingId.New(), Name = "Actor" };
        var behavior = new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash" };
        behavior.GrantRole(roles);
        actor.Behaviors.Add(behavior);
        return actor;
    }

    [Fact]
    public void VerbAndAliases_PassThroughFromTheWrappedCommand()
    {
        var sut = new RoleGuardedCommand(new FakeCommand(), SecurityRole.FullAdmin);

        sut.Verb.Should().Be("ban");
        sut.Aliases.Should().Equal("b");
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToInnerCommand_WhenActorHasTheRequiredRole()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(SecurityRole.FullAdmin);
        var inner = new FakeCommand();
        var sut = new RoleGuardedCommand(inner, SecurityRole.FullAdmin);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        inner.WasExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SendsRejectionMessage_WhenActorLacksTheRequiredRole()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(SecurityRole.Player);
        var inner = new FakeCommand();
        var sut = new RoleGuardedCommand(inner, SecurityRole.FullAdmin);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        inner.WasExecuted.Should().BeFalse();
        await session.Received(1).WriteLineAsync("You don't have permission to do that.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Delegates_WhenActorHasAtLeastOneOfSeveralRequiredFlags()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(SecurityRole.MinorAdmin);
        var inner = new FakeCommand();
        var sut = new RoleGuardedCommand(inner, SecurityRole.MinorAdmin | SecurityRole.FullAdmin);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        inner.WasExecuted.Should().BeTrue();
    }
}
