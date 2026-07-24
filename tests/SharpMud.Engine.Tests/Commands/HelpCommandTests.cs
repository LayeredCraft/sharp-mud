using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Help;
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

    private static HelpCommand MakeSut(
        ICommandRegistry? registry = null,
        IHelpRepository? helpRepository = null,
        IHelpSearchIndex? helpSearchIndex = null)
    {
        return new HelpCommand(
            registry ?? new CommandRegistry(),
            helpRepository ?? Substitute.For<IHelpRepository>(),
            helpSearchIndex ?? Substitute.For<IHelpSearchIndex>());
    }

    [Fact]
    public async Task ExecuteAsync_OmitsRoleGatedCommand_WhenActorLacksTheRequiredRole()
    {
        var session = Substitute.For<ISession>();
        var registry = new CommandRegistry();
        registry.RegisterOpen(new FakeCommand("look"));
        registry.RegisterWithRole(new FakeCommand("ban"), SecurityRole.FullAdmin);

        var actor = MakeActor(SecurityRole.Player);
        var sut = MakeSut(registry);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.DidNotReceive().WriteLineAsync(Arg.Is<string>(s => s!.Contains("ban")), Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync(Arg.Is<string>(s => s!.Contains("look")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_IncludesRoleGatedCommand_WhenActorHasTheRequiredRole()
    {
        var session = Substitute.For<ISession>();
        var registry = new CommandRegistry();
        registry.RegisterWithRole(new FakeCommand("ban"), SecurityRole.FullAdmin);

        var actor = MakeActor(SecurityRole.FullAdmin);
        var sut = MakeSut(registry);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync(Arg.Is<string>(s => s!.Contains("ban")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysIncludesNonGatedCommands_RegardlessOfRole()
    {
        var session = Substitute.For<ISession>();
        var registry = new CommandRegistry();
        registry.RegisterOpen(new FakeCommand("look"));

        var actor = MakeActor(SecurityRole.Player);
        var sut = MakeSut(registry);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync(Arg.Is<string>(s => s!.Contains("look")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WritesTopicBody_OnExactKeyMatch()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(SecurityRole.Player);
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "How to become a wizard." };
        var helpRepository = Substitute.For<IHelpRepository>();
        helpRepository.FindByNameOrAliasAsync("wizard", Arg.Any<CancellationToken>()).Returns(topic);

        var sut = MakeSut(helpRepository: helpRepository);
        var ctx = new CommandContext(actor, actor, ["wizard"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("How to become a wizard.", Arg.Any<CancellationToken>());
        await helpRepository.DidNotReceiveWithAnyArgs().FindByKeywordAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToKeywordMatch_WhenExactMatchMisses()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(SecurityRole.Player);
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "How to become a wizard." };
        var helpRepository = Substitute.For<IHelpRepository>();
        helpRepository.FindByNameOrAliasAsync("magic", Arg.Any<CancellationToken>()).Returns((HelpTopic?)null);
        helpRepository.FindByKeywordAsync("magic", Arg.Any<CancellationToken>()).Returns([topic]);
        var helpSearchIndex = Substitute.For<IHelpSearchIndex>();

        var sut = MakeSut(helpRepository: helpRepository, helpSearchIndex: helpSearchIndex);
        var ctx = new CommandContext(actor, actor, ["magic"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("How to become a wizard.", Arg.Any<CancellationToken>());
        await helpSearchIndex.DidNotReceiveWithAnyArgs().SearchAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToSemanticSearch_WhenExactAndKeywordMatchesMiss()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(SecurityRole.Player);
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "How to become a wizard." };
        var helpRepository = Substitute.For<IHelpRepository>();
        helpRepository.FindByNameOrAliasAsync("how do i become a wizard", Arg.Any<CancellationToken>()).Returns((HelpTopic?)null);
        helpRepository.FindByKeywordAsync("how do i become a wizard", Arg.Any<CancellationToken>()).Returns([]);
        var helpSearchIndex = Substitute.For<IHelpSearchIndex>();
        helpSearchIndex.SearchAsync("how do i become a wizard", Arg.Any<CancellationToken>()).Returns([new HelpSearchHit(topic, 0.4)]);

        var sut = MakeSut(helpRepository: helpRepository, helpSearchIndex: helpSearchIndex);
        var ctx = new CommandContext(actor, actor, ["how", "do", "i", "become", "a", "wizard"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("How to become a wizard.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReportsNoTopicFound_WhenAllThreeTiersMiss()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(SecurityRole.Player);
        var helpRepository = Substitute.For<IHelpRepository>();
        helpRepository.FindByNameOrAliasAsync("nonsense", Arg.Any<CancellationToken>()).Returns((HelpTopic?)null);
        helpRepository.FindByKeywordAsync("nonsense", Arg.Any<CancellationToken>()).Returns([]);
        var helpSearchIndex = Substitute.For<IHelpSearchIndex>();
        helpSearchIndex.SearchAsync("nonsense", Arg.Any<CancellationToken>()).Returns([]);

        var sut = MakeSut(helpRepository: helpRepository, helpSearchIndex: helpSearchIndex);
        var ctx = new CommandContext(actor, actor, ["nonsense"], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("No help topic found for 'nonsense'.", Arg.Any<CancellationToken>());
    }
}
