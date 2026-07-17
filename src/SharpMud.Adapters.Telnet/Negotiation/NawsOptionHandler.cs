using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.Logging;

namespace SharpMud.Adapters.Telnet.Negotiation;

/// <summary>
/// RFC 1073 (NAWS) - negotiates the client's terminal window size. Telnet
/// is an untrusted input surface (see
/// .agents/skills/engineering-workflow/references/security.md), so a
/// client-reported size is clamped rather than trusted outright - directly
/// ports WheelMUD's TelnetOptionNaws.ProcessSubNegotiation clamping logic
/// (see ADR-0002, docs/adr/0002-telnet-protocol-negotiation.md).
/// </summary>
internal sealed class NawsOptionHandler : ITelnetOptionHandler
{
    public int Width { get; private set; } = DefaultWidth;
    public int Height { get; private set; } = DefaultHeight;

    public TelnetOptionCode Code => TelnetOptionCode.Naws;

    public NawsOptionHandler(string sessionId, ILogger<TelnetSession> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(logger);

        _sessionId = sessionId;
        _logger = logger;
    }

    public ValueTask OnSubnegotiationAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (payload.Length < 4)
            return ValueTask.CompletedTask;

        var span = payload.Span;
        var width = (256 * span[0]) + span[1];
        var height = (256 * span[2]) + span[3];

        Width = width <= 0 ? DefaultWidth : Math.Max(width, MinimumWidth);
        Height = height <= 0 ? DefaultHeight : Math.Max(height, MinimumHeight);

        _logger.Information(
            "Negotiated terminal size {Width}x{Height} for session {SessionId}", Width, Height, _sessionId);

        return ValueTask.CompletedTask;
    }

    internal const int DefaultWidth = 80;
    internal const int DefaultHeight = 20;

    private const int MinimumWidth = 20;
    private const int MinimumHeight = 6;

    private readonly string _sessionId;
    private readonly ILogger<TelnetSession> _logger;
}
