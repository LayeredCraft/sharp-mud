using Microsoft.Extensions.Logging;
using SharpMud.Adapters.Telnet.Negotiation;

namespace SharpMud.Adapters.Telnet.Tests.Negotiation;

public sealed class TelnetOptionNegotiatorTests
{
    private static TelnetOptionNegotiator CreateNegotiator(ITelnetByteSink sink) =>
        new(sink, [new EchoOptionHandler(), new NawsOptionHandler("test-session", Substitute.For<ILogger<TelnetSession>>())],
            Substitute.For<ILogger<TelnetSession>>());

    [Fact]
    public async Task RequestRemoteAsync_SendsDo_WhenOptionNotYetNegotiated()
    {
        var sink = Substitute.For<ITelnetByteSink>();
        var negotiator = CreateNegotiator(sink);

        await negotiator.RequestRemoteAsync(TelnetOptionCode.Naws, enable: true, TestContext.Current.CancellationToken);

        await sink.Received(1).WriteAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(new byte[] { 255, 253, 31 })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestRemoteAsync_DoesNotResend_WhenAlreadyYes()
    {
        var sink = Substitute.For<ITelnetByteSink>();
        var negotiator = CreateNegotiator(sink);
        await negotiator.RequestRemoteAsync(TelnetOptionCode.Naws, enable: true, TestContext.Current.CancellationToken);
        await FeedAsync(negotiator, [255, 251, 31]); // client responds IAC WILL NAWS -> HimState becomes Yes

        await negotiator.RequestRemoteAsync(TelnetOptionCode.Naws, enable: true, TestContext.Current.CancellationToken);

        await sink.Received(1).WriteAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NegotiateWill_TransitionsWithoutResending_WhenWeAlreadyRequestedIt()
    {
        var sink = Substitute.For<ITelnetByteSink>();
        var negotiator = CreateNegotiator(sink);
        await negotiator.RequestRemoteAsync(TelnetOptionCode.Naws, enable: true, TestContext.Current.CancellationToken);

        await FeedAsync(negotiator, [255, 251, 31]); // IAC WILL NAWS

        // Only the original DO should have been sent - the WILL response
        // settles the negotiation without triggering another round trip.
        await sink.Received(1).WriteAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestRemoteAsync_QueuesOppositeRequest_ThenFulfillsItWhenNegotiationSettles()
    {
        var sink = Substitute.For<ITelnetByteSink>();
        var negotiator = CreateNegotiator(sink);
        var ct = TestContext.Current.CancellationToken;

        await negotiator.RequestRemoteAsync(TelnetOptionCode.Naws, enable: true, ct); // No -> WantYes, sends DO
        await FeedAsync(negotiator, [255, 252, 31]); // IAC WONT NAWS -> HimState settles at No
        await negotiator.RequestRemoteAsync(TelnetOptionCode.Naws, enable: false, ct); // No -> WantNo, sends DONT
        await negotiator.RequestRemoteAsync(TelnetOptionCode.Naws, enable: true, ct); // WantNo -> queues opposite, no send yet

        await sink.Received(2).WriteAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());

        await FeedAsync(negotiator, [255, 252, 31]); // IAC WONT NAWS confirms the DONT -> queued opposite fires, sends DO

        // Three sends total: the original DO, the DONT, and the queued
        // opposite firing a second DO once the DONT settles - each of the
        // two DO sends carries identical bytes, so assert the total count
        // rather than a single Received() call for those bytes specifically.
        await sink.Received(3).WriteAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await sink.Received(2).WriteAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(new byte[] { 255, 253, 31 })), Arg.Any<CancellationToken>());
        await sink.Received(1).WriteAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(new byte[] { 255, 254, 31 })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleIacAsync_RefusesUnregisteredOption_WithDont_ForWillOffer()
    {
        var sink = Substitute.For<ITelnetByteSink>();
        var negotiator = CreateNegotiator(sink);

        await FeedAsync(negotiator, [255, 251, 99]); // IAC WILL <unregistered option 99>

        await sink.Received(1).WriteAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(new byte[] { 255, 254, 99 })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleIacAsync_RefusesUnregisteredOption_WithWont_ForDoRequest()
    {
        var sink = Substitute.For<ITelnetByteSink>();
        var negotiator = CreateNegotiator(sink);

        await FeedAsync(negotiator, [255, 253, 99]); // IAC DO <unregistered option 99>

        await sink.Received(1).WriteAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(new byte[] { 255, 252, 99 })), Arg.Any<CancellationToken>());
    }

    private static async Task FeedAsync(TelnetOptionNegotiator negotiator, byte[] iacSequence)
    {
        // iacSequence includes the leading IAC (0xFF) byte for readability;
        // HandleIacAsync expects it already consumed by the caller's read
        // loop, so skip it here to match the real call site in TelnetSession.
        using var stream = new MemoryStream(iacSequence[1..]);
        await negotiator.HandleIacAsync(stream, TestContext.Current.CancellationToken);
    }
}
