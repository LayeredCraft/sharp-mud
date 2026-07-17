using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Behaviors;

public sealed class PlayerBehaviorConnectionStateTests
{
    private static PlayerBehavior MakePlayer() => new() { Username = "TestUser", PasswordHash = "test-hash" };

    [Fact]
    public void EnterLinkdead_SetsStateAndTimestamp_WhenPlaying()
    {
        var player = MakePlayer();
        var now = DateTimeOffset.UtcNow;

        player.EnterLinkdead(now);

        player.ConnectionState.Should().Be(ConnectionState.Linkdead);
        player.LinkdeadSinceUtc.Should().Be(now);
    }

    [Fact]
    public void EnterLinkdead_Throws_WhenAlreadyLinkdead()
    {
        var player = MakePlayer();
        player.EnterLinkdead(DateTimeOffset.UtcNow);

        var act = () => player.EnterLinkdead(DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reconnect_ClearsStateAndTimestamp_WhenLinkdead()
    {
        var player = MakePlayer();
        player.EnterLinkdead(DateTimeOffset.UtcNow);

        player.Reconnect();

        player.ConnectionState.Should().Be(ConnectionState.Playing);
        player.LinkdeadSinceUtc.Should().BeNull();
    }

    [Fact]
    public void Reconnect_Throws_WhenAlreadyPlaying()
    {
        var player = MakePlayer();

        var act = () => player.Reconnect();

        act.Should().Throw<InvalidOperationException>();
    }
}
