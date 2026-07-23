using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Commands;

public sealed class MuteGuardedCommandTests
{
    private sealed class FakeCommand : ICommand
    {
        public bool WasExecuted { get; private set; }
        public string Verb => "say";
        public IReadOnlyList<string> Aliases { get; } = [];

        public Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
        {
            WasExecuted = true;
            return Task.CompletedTask;
        }
    }

    private static Thing MakeActor(bool muted)
    {
        var actor = new Thing { Id = ThingId.New(), Name = "Actor" };
        var behavior = new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash" };
        if (muted)
            behavior.Mute();
        actor.Behaviors.Add(behavior);
        return actor;
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesToInnerCommand_WhenActorIsNotMuted()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(muted: false);
        var inner = new FakeCommand();
        var sut = new MuteGuardedCommand(inner);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        inner.WasExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SendsRejectionMessage_WhenActorIsMuted()
    {
        var session = Substitute.For<ISession>();
        var actor = MakeActor(muted: true);
        var inner = new FakeCommand();
        var sut = new MuteGuardedCommand(inner);
        var ctx = new CommandContext(actor, actor, [], new World(), session);

        await sut.ExecuteAsync(ctx, TestContext.Current.CancellationToken);

        inner.WasExecuted.Should().BeFalse();
        await session.Received(1).WriteLineAsync("You have been muted and cannot do that.", Arg.Any<CancellationToken>());
    }
}
