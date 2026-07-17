using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.Logging;

namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>
/// Drives RFC 1143 ("Q-Method") telnet option negotiation for a single
/// connection. The four-state Us/Him transition tables in
/// <see cref="NegotiateWillAsync"/>/<see cref="NegotiateWontAsync"/>/
/// <see cref="NegotiateDoAsync"/>/<see cref="NegotiateDontAsync"/> are
/// adapted case-by-case from WheelMUD's TelnetOption.cs (see ADR-0002,
/// docs/adr/0002-telnet-protocol-negotiation.md) since that's the
/// correctness-critical piece that prevents infinite negotiation loops.
/// The persistent byte-parser class hierarchy WheelMUD pairs it with is
/// deliberately not adopted - this negotiator instead parses a whole IAC
/// sequence per call, since the connection's own read loop is already one
/// sequential await chain per connection.
/// </summary>
internal sealed class TelnetOptionNegotiator
{
    private readonly ITelnetByteSink _sink;
    private readonly ILogger<TelnetSession> _logger;
    private readonly Dictionary<byte, (ITelnetOptionHandler? Handler, TelnetOptionState State)> _options;

    internal TelnetOptionNegotiator(
        ITelnetByteSink sink, IReadOnlyList<ITelnetOptionHandler> handlers, ILogger<TelnetSession> logger)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(logger);

        _sink = sink;
        _logger = logger;
        _options = new Dictionary<byte, (ITelnetOptionHandler?, TelnetOptionState)>();
        foreach (var handler in handlers)
            _options[(byte)handler.Code] = (handler, new TelnetOptionState());
    }

    /// <summary>
    /// Handles one telnet command sequence, given that the leading IAC
    /// (0xFF) byte has already been consumed by the caller's read loop.
    /// </summary>
    internal async ValueTask HandleIacAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[1];
        if (await stream.ReadAsync(buffer, ct) == 0)
            return;

        var command = buffer[0];

        if (command == TelnetCommandByte.Sb)
        {
            await HandleSubnegotiationAsync(stream, ct);
            return;
        }

        if (command is not (TelnetCommandByte.Will or TelnetCommandByte.Wont or TelnetCommandByte.Do or TelnetCommandByte.Dont))
            return; // Other IAC sequences (NOP, GA, ...) carry no option byte - nothing further to read.

        if (await stream.ReadAsync(buffer, ct) == 0)
            return;
        var optionCode = buffer[0];
        var (_, state) = GetOption(optionCode);

        switch (command)
        {
            case TelnetCommandByte.Will:
                await NegotiateWillAsync(optionCode, state, ct);
                break;
            case TelnetCommandByte.Wont:
                await NegotiateWontAsync(optionCode, state, ct);
                break;
            case TelnetCommandByte.Do:
                await NegotiateDoAsync(optionCode, state, ct);
                break;
            case TelnetCommandByte.Dont:
                await NegotiateDontAsync(optionCode, state, ct);
                break;
        }
    }

    /// <summary>Offers/withdraws an option we initiate ourselves (WILL/WONT) - e.g. Echo.</summary>
    internal async ValueTask RequestLocalAsync(TelnetOptionCode code, bool enable, CancellationToken ct)
    {
        var state = _options[(byte)code].State;
        state.WantOption = enable;

        if (enable)
        {
            switch (state.UsState)
            {
                case OptionSide.No:
                    state.UsState = OptionSide.WantYes;
                    await SendAsync(TelnetCommandByte.Will, (byte)code, ct);
                    break;
                case OptionSide.Yes:
                    break;
                case OptionSide.WantNo:
                    state.UsQueuedOpposite = true;
                    break;
                case OptionSide.WantYes:
                    state.UsQueuedOpposite = false;
                    break;
            }
        }
        else
        {
            switch (state.UsState)
            {
                case OptionSide.No:
                case OptionSide.Yes:
                    state.UsState = OptionSide.WantNo;
                    await SendAsync(TelnetCommandByte.Wont, (byte)code, ct);
                    break;
                case OptionSide.WantNo:
                    state.UsQueuedOpposite = false;
                    break;
                case OptionSide.WantYes:
                    state.UsQueuedOpposite = true;
                    break;
            }
        }
    }

    /// <summary>Requests/withdraws a request that the remote side do something (DO/DONT) - e.g. NAWS.</summary>
    internal async ValueTask RequestRemoteAsync(TelnetOptionCode code, bool enable, CancellationToken ct)
    {
        var state = _options[(byte)code].State;
        state.WantOption = enable;

        if (enable)
        {
            switch (state.HimState)
            {
                case OptionSide.No:
                    state.HimState = OptionSide.WantYes;
                    await SendAsync(TelnetCommandByte.Do, (byte)code, ct);
                    break;
                case OptionSide.Yes:
                    break;
                case OptionSide.WantNo:
                    state.HimQueuedOpposite = true;
                    break;
                case OptionSide.WantYes:
                    state.HimQueuedOpposite = false;
                    break;
            }
        }
        else
        {
            switch (state.HimState)
            {
                case OptionSide.No:
                case OptionSide.Yes:
                    state.HimState = OptionSide.WantNo;
                    await SendAsync(TelnetCommandByte.Dont, (byte)code, ct);
                    break;
                case OptionSide.WantNo:
                    state.HimQueuedOpposite = false;
                    break;
                case OptionSide.WantYes:
                    state.HimQueuedOpposite = true;
                    break;
            }
        }
    }

    private (ITelnetOptionHandler? Handler, TelnetOptionState State) GetOption(byte optionCode)
    {
        if (_options.TryGetValue(optionCode, out var entry))
            return entry;

        // Not a registered option - an ephemeral, unpersisted "don't want
        // it" state naturally makes the negotiation logic below reply
        // WONT/DONT to anything we weren't asked to support (RFC 854),
        // without a separate stub type and without growing unbounded state
        // for a hostile client hammering random option codes (Telnet is
        // untrusted input - see security.md).
        _logger.Debug("Refused unrequested telnet option {OptionCode}", optionCode);
        return (null, new TelnetOptionState());
    }

    private async ValueTask HandleSubnegotiationAsync(Stream stream, CancellationToken ct)
    {
        var singleByte = new byte[1];
        if (await stream.ReadAsync(singleByte, ct) == 0)
            return;
        var optionCode = singleByte[0];

        var payload = new List<byte>();
        while (true)
        {
            if (await stream.ReadAsync(singleByte, ct) == 0)
                return;
            var b = singleByte[0];

            if (b != TelnetCommandByte.Iac)
            {
                payload.Add(b);
                continue;
            }

            if (await stream.ReadAsync(singleByte, ct) == 0)
                return;
            var next = singleByte[0];

            if (next == TelnetCommandByte.Se)
                break;

            if (next == TelnetCommandByte.Iac)
            {
                payload.Add(TelnetCommandByte.Iac); // Escaped literal 0xFF within the subnegotiation payload.
                continue;
            }

            break; // Malformed - stop buffering rather than looping on adversarial input.
        }

        var (handler, _) = GetOption(optionCode);
        if (handler is not null)
            await handler.OnSubnegotiationAsync(payload.ToArray(), ct);
    }

    // Reacts to the remote side offering WILL <code> - updates HimState.
    private async ValueTask NegotiateWillAsync(byte optionCode, TelnetOptionState state, CancellationToken ct)
    {
        switch (state.HimState)
        {
            case OptionSide.No:
                if (state.WantOption)
                {
                    state.HimState = OptionSide.Yes;
                    await SendAsync(TelnetCommandByte.Do, optionCode, ct);
                }
                else
                {
                    await SendAsync(TelnetCommandByte.Dont, optionCode, ct);
                }
                break;
            case OptionSide.Yes:
                break;
            case OptionSide.WantNo:
                state.HimState = state.HimQueuedOpposite ? OptionSide.Yes : OptionSide.No;
                state.HimQueuedOpposite = false;
                break;
            case OptionSide.WantYes:
                if (state.HimQueuedOpposite)
                {
                    state.HimState = OptionSide.No;
                    state.HimQueuedOpposite = false;
                    await SendAsync(TelnetCommandByte.Dont, optionCode, ct);
                }
                else
                {
                    state.HimState = OptionSide.Yes;
                }
                break;
        }
    }

    // Reacts to the remote side offering WONT <code>.
    private async ValueTask NegotiateWontAsync(byte optionCode, TelnetOptionState state, CancellationToken ct)
    {
        switch (state.HimState)
        {
            case OptionSide.No:
                break;
            case OptionSide.Yes:
                state.HimState = OptionSide.No;
                await SendAsync(TelnetCommandByte.Dont, optionCode, ct);
                break;
            case OptionSide.WantNo:
                if (state.HimQueuedOpposite)
                {
                    state.HimState = OptionSide.WantYes;
                    state.HimQueuedOpposite = false;
                    await SendAsync(TelnetCommandByte.Do, optionCode, ct);
                }
                else
                {
                    state.HimState = OptionSide.No;
                }
                break;
            case OptionSide.WantYes:
                state.HimState = OptionSide.No;
                state.HimQueuedOpposite = false;
                break;
        }
    }

    // Reacts to the remote side requesting DO <code> - updates UsState.
    private async ValueTask NegotiateDoAsync(byte optionCode, TelnetOptionState state, CancellationToken ct)
    {
        switch (state.UsState)
        {
            case OptionSide.No:
                if (state.WantOption)
                {
                    state.UsState = OptionSide.Yes;
                    await SendAsync(TelnetCommandByte.Will, optionCode, ct);
                }
                else
                {
                    await SendAsync(TelnetCommandByte.Wont, optionCode, ct);
                }
                break;
            case OptionSide.Yes:
                break;
            case OptionSide.WantNo:
                state.UsState = state.UsQueuedOpposite ? OptionSide.Yes : OptionSide.No;
                state.UsQueuedOpposite = false;
                break;
            case OptionSide.WantYes:
                if (state.UsQueuedOpposite)
                {
                    state.UsState = OptionSide.No;
                    state.UsQueuedOpposite = false;
                    await SendAsync(TelnetCommandByte.Wont, optionCode, ct);
                }
                else
                {
                    state.UsState = OptionSide.Yes;
                }
                break;
        }
    }

    // Reacts to the remote side requesting DONT <code>.
    private async ValueTask NegotiateDontAsync(byte optionCode, TelnetOptionState state, CancellationToken ct)
    {
        switch (state.UsState)
        {
            case OptionSide.No:
                break;
            case OptionSide.Yes:
                state.UsState = OptionSide.No;
                await SendAsync(TelnetCommandByte.Wont, optionCode, ct);
                break;
            case OptionSide.WantNo:
                if (state.UsQueuedOpposite)
                {
                    state.UsState = OptionSide.WantYes;
                    state.UsQueuedOpposite = false;
                    await SendAsync(TelnetCommandByte.Will, optionCode, ct);
                }
                else
                {
                    state.UsState = OptionSide.No;
                }
                break;
            case OptionSide.WantYes:
                state.UsState = OptionSide.No;
                state.UsQueuedOpposite = false;
                break;
        }
    }

    private ValueTask SendAsync(byte command, byte optionCode, CancellationToken ct) =>
        _sink.WriteAsync([TelnetCommandByte.Iac, command, optionCode], ct);
}
