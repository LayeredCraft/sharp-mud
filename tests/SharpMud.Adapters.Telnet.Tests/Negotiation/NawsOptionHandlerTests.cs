using Microsoft.Extensions.Logging;
using SharpMud.Adapters.Telnet.Negotiation;

namespace SharpMud.Adapters.Telnet.Tests.Negotiation;

public sealed class NawsOptionHandlerTests
{
    [Fact]
    public async Task OnSubnegotiationAsync_SetsWidthAndHeight_ForNormalPayload()
    {
        var handler = new NawsOptionHandler("test-session", Substitute.For<ILogger<TelnetSession>>());

        await handler.OnSubnegotiationAsync(new byte[] { 0, 100, 0, 40 }, TestContext.Current.CancellationToken);

        handler.Width.Should().Be(100);
        handler.Height.Should().Be(40);
    }

    [Fact]
    public async Task OnSubnegotiationAsync_FallsBackToDefaults_WhenClientReportsZero()
    {
        var handler = new NawsOptionHandler("test-session", Substitute.For<ILogger<TelnetSession>>());

        await handler.OnSubnegotiationAsync(new byte[] { 0, 0, 0, 0 }, TestContext.Current.CancellationToken);

        handler.Width.Should().Be(NawsOptionHandler.DefaultWidth);
        handler.Height.Should().Be(NawsOptionHandler.DefaultHeight);
    }

    [Fact]
    public async Task OnSubnegotiationAsync_ClampsToMinimum_WhenClientReportsSmallerSize()
    {
        var handler = new NawsOptionHandler("test-session", Substitute.For<ILogger<TelnetSession>>());

        await handler.OnSubnegotiationAsync(new byte[] { 0, 5, 0, 2 }, TestContext.Current.CancellationToken);

        handler.Width.Should().Be(20);
        handler.Height.Should().Be(6);
    }

    [Fact]
    public async Task OnSubnegotiationAsync_IgnoresPayload_WhenShorterThanFourBytes()
    {
        var handler = new NawsOptionHandler("test-session", Substitute.For<ILogger<TelnetSession>>());

        await handler.OnSubnegotiationAsync(new byte[] { 0, 100, 0 }, TestContext.Current.CancellationToken);

        handler.Width.Should().Be(NawsOptionHandler.DefaultWidth);
        handler.Height.Should().Be(NawsOptionHandler.DefaultHeight);
    }
}
