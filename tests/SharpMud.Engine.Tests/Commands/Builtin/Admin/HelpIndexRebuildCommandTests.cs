using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Help;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands.Builtin.Admin;

public sealed class HelpIndexRebuildCommandTests
{
    private static (Thing Actor, World World, ISession Session) MakeActor()
    {
        var world = new World();
        var actor = new Thing { Id = ThingId.New(), Name = "Builder" };
        world.Register(actor);
        return (actor, world, Substitute.For<ISession>());
    }

    [Fact]
    public async Task ExecuteAsync_ReplacesChunks_ForEveryTopic()
    {
        var (actor, world, session) = MakeActor();
        var topic = new HelpTopic { Id = HelpTopicId.New(), Key = "wizard", Body = "How to become a wizard." };
        var repository = Substitute.For<IHelpRepository>();
        repository.GetAllTopicsAsync(Arg.Any<CancellationToken>()).Returns([topic]);
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();
        embeddingProvider.ModelId.Returns("stub-hashed-bow-v1");
        embeddingProvider.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([1f, 0f]);

        var sut = new HelpIndexRebuildCommand(repository, embeddingProvider);
        var ctx = new CommandContext(actor, actor, ["rebuild"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        topic.Chunks.Should().ContainSingle();
        topic.Chunks[0].SourceContentHash.Should().Be(topic.ContentHash);
        topic.Chunks[0].EmbeddingModelId.Should().Be("stub-hashed-bow-v1");
        await repository.Received(1).SaveTopicAsync(topic, Arg.Any<CancellationToken>());
        await session.Received(1).WriteLineAsync("Rebuilt the help index: 1 topic(s), 1 chunk(s).", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsUsageMessage_WhenArgumentIsNotRebuild()
    {
        var (actor, world, session) = MakeActor();
        var repository = Substitute.For<IHelpRepository>();
        var embeddingProvider = Substitute.For<IEmbeddingProvider>();

        var sut = new HelpIndexRebuildCommand(repository, embeddingProvider);
        var ctx = new CommandContext(actor, actor, ["nonsense"], world, session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        await session.Received(1).WriteLineAsync("Usage: helpindex rebuild", Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().GetAllTopicsAsync(Arg.Any<CancellationToken>());
    }
}
