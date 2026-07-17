using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Tests.Sessions;

public sealed class ConnectionStateTests
{
    [Fact]
    public void CanTransitionTo_ReturnsTrue_FromPlayingToLinkdead() =>
        ConnectionState.Playing.CanTransitionTo(ConnectionState.Linkdead).Should().BeTrue();

    [Fact]
    public void CanTransitionTo_ReturnsTrue_FromLinkdeadToPlaying() =>
        ConnectionState.Linkdead.CanTransitionTo(ConnectionState.Playing).Should().BeTrue();

    [Fact]
    public void CanTransitionTo_ReturnsFalse_FromPlayingToPlaying() =>
        ConnectionState.Playing.CanTransitionTo(ConnectionState.Playing).Should().BeFalse();

    [Fact]
    public void CanTransitionTo_ReturnsFalse_FromLinkdeadToLinkdead() =>
        ConnectionState.Linkdead.CanTransitionTo(ConnectionState.Linkdead).Should().BeFalse();
}
