using Microsoft.Extensions.Logging;
using SharpMud.Adapters.Telnet;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;

namespace SharpMud.Host;

/// <summary>
/// Groups the dependencies <see cref="HostRunner.RunTelnetAsync"/> needs -
/// a parameter object rather than a growing parameter list, per
/// .agents/skills/engineering-workflow/references/coding-standards.md's
/// 4-parameter rule (see ADR-0002, docs/adr/0002-telnet-protocol-negotiation.md,
/// for why this was introduced now).
/// </summary>
public sealed record TelnetHostContext(
    World World,
    ICommandParser Parser,
    ICommandRegistry Registry,
    IThingRepository Repository,
    Thing StartingRoom,
    int Port,
    ILogger<TelnetSession> Logger);
