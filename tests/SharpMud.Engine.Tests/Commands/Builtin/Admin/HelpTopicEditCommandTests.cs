using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Help;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class HelpTopicEditCommandTests
{
    private static (Thing Actor, World World, ISession Session) MakeActor()
    {
        var world = new World();
        var actor = new Thing { Id = ThingId.New(), Name = "Builder" };
        world.Register(actor);
        return (actor, world, Substitute.For<ISession>());
    }

    [Fact]
    public async Task ExecuteAsync_CreatesNewTopic_WhenNoneExists()
    {
        var (actor, world, session) = MakeActor();
        var repository = Substitute.For<IHelpRepository>();
        repository.FindByNameOrAliasAsync("wizard", Arg.Any<CancellationToken>()).Returns((HelpTopic?)null);

        var sut = new HelpTopicEditCommand(repository);
        var ctx = new CommandContext(actor, actor, ["wizard", "How", "to", "become", "a", "wizard."], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await repository.Received(1).SaveTopicAsync(
            Arg.Is<HelpTopic>(t => t.Key == "wizard" && t.Body == "How to become a wizard."),
            Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync("Help topic 'wizard' saved.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OverwritesBody_WhenTopicAlreadyExists()
    {
        var (actor, world, session) = MakeActor();
        var existing = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Old text." };
        var repository = Substitute.For<IHelpRepository>();
        repository.FindByNameOrAliasAsync("wizard", Arg.Any<CancellationToken>()).Returns(existing);

        var sut = new HelpTopicEditCommand(repository);
        var ctx = new CommandContext(actor, actor, ["wizard", "New", "text."], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await repository.Received(1).SaveTopicAsync(
            Arg.Is<HelpTopic>(t => t.Id == existing.Id && t.Body == "New text."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotReassignKey_WhenExistingTopicFoundByDifferentCasing()
    {
        var (actor, world, session) = MakeActor();
        var existing = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "Old text." };
        var repository = Substitute.For<IHelpRepository>();
        // FindByNameOrAliasAsync is case-insensitive - "Wizard" finds the
        // "wizard" topic, same as it would for an alias once those are
        // settable.
        repository.FindByNameOrAliasAsync("Wizard", Arg.Any<CancellationToken>()).Returns(existing);

        var sut = new HelpTopicEditCommand(repository);
        var ctx = new CommandContext(actor, actor, ["Wizard", "New", "text."], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await repository.Received(1).SaveTopicAsync(
            Arg.Is<HelpTopic>(t => t.Key == "wizard" && t.Body == "New text."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsUsageMessage_WhenMissingBody()
    {
        var (actor, world, session) = MakeActor();
        var repository = Substitute.For<IHelpRepository>();

        var sut = new HelpTopicEditCommand(repository);
        var ctx = new CommandContext(actor, actor, ["wizard"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("Usage: helptopic <key> <body>", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().SaveTopicAsync(default!, Arg.Any<CancellationToken>());
    }
}
