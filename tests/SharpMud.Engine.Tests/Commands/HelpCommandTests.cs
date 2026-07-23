using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands;

public sealed class HelpCommandTests
{
    private sealed class FakeCommand(string verb) : ICommand
    {
        public string Verb { get; } = verb;
        public IReadOnlyList<string> Aliases { get; } = [];
        public Task ExecuteAsync(CommandContext ctx, CancellationToken ct) => Task.CompletedTask;
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
    public async Task ExecuteAsync_OmitsRoleGatedCommand_WhenActorLacksTheRequiredRole()
    {
        var session = Substitute.For<ISession>();
        var registry = new CommandRegistry();
        registry.RegisterOpen(new FakeCommand("look"));
        registry.RegisterWithRole(new FakeCommand("ban"), SecurityRole.FullAdmin);

        var actor = MakeActor(SecurityRole.Player);
        var sut = new HelpCommand(registry);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.DidNotReceive().WriteLineAsync(Arg.Is<string>(s => s.Contains("ban")), Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync(Arg.Is<string>(s => s.Contains("look")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_IncludesRoleGatedCommand_WhenActorHasTheRequiredRole()
    {
        var session = Substitute.For<ISession>();
        var registry = new CommandRegistry();
        registry.RegisterWithRole(new FakeCommand("ban"), SecurityRole.FullAdmin);

        var actor = MakeActor(SecurityRole.FullAdmin);
        var sut = new HelpCommand(registry);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync(Arg.Is<string>(s => s.Contains("ban")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysIncludesNonGatedCommands_RegardlessOfRole()
    {
        var session = Substitute.For<ISession>();
        var registry = new CommandRegistry();
        registry.RegisterOpen(new FakeCommand("look"));

        var actor = MakeActor(SecurityRole.Player);
        var sut = new HelpCommand(registry);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync(Arg.Is<string>(s => s.Contains("look")), Arg.Any<CancellationToken>());
    }
}
