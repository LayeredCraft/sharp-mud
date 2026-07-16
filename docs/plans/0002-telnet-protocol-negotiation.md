# [PLAN-0002] Telnet Protocol Negotiation (IAC/Q-Method core + NAWS)

**Implements:** [ADR-0002](../adr/0002-telnet-protocol-negotiation.md)

**Status:** Not Started

**Last updated:** 2026-07-16

## Goal

`TelnetSession` performs real RFC-1143 option negotiation and NAWS
(window-size) negotiation instead of discarding IAC bytes; `ISession`
exposes negotiated `TerminalWidth`/`TerminalHeight`; this repo has real
logging infrastructure for the first time, exercised by this feature.

## Scope

In scope: IAC negotiation core (Q-Method state tracking), Echo (folded
into the same negotiator), NAWS. Logging infrastructure stood up as part
of this plan (Serilog + `LayeredCraft.StructuredLogging`). The
`TelnetHostContext` parameter-object fix for `HostRunner`'s pre-existing
param-count debt.

Out of scope (see ADR-0002's Decision Outcome): MCCP, MXP, TermType —
future ADRs/plans.

## Tasks

### Negotiation core (new — `src/SharpMud.Adapters.Telnet/Negotiation/`)

- [ ] `OptionSide.cs` — `internal enum OptionSide { No, WantNo, Yes, WantYes }`
- [ ] `TelnetOptionState.cs` — `internal sealed class`, `UsState`/`UsQueuedOpposite`/`HimState`/`HimQueuedOpposite`
- [ ] `TelnetOptionCode.cs` — `internal enum TelnetOptionCode : byte { Echo = 1, Naws = 31 }` + XML doc noting future codes (SuppressGoAhead=3, TermType=24, MCCP2=86, MXP=91)
- [ ] `ITelnetOptionHandler.cs` — `Code`, `OnSubnegotiationAsync`
- [ ] `EchoOptionHandler.cs` — no-op subnegotiation (RFC 857)
- [ ] `NawsOptionHandler.cs` — `Width`/`Height`, defaults 80×20, clamp floor 20×6, ignore malformed/short payloads
- [ ] `ITelnetByteSink.cs` — `WriteAsync(byte[], CancellationToken)`
- [ ] `TelnetOptionNegotiator.cs` — `HandleIacAsync`, `RequestLocalAsync`, `RequestRemoteAsync`; WILL/WONT/DO/DONT transition tables copied from `WheelMUD/src/Server/Telnet/TelnetOption.cs` (~lines 172–413); unregistered option code → ephemeral "don't want it" state → automatic WONT/DONT

### Existing file changes

- [ ] `TelnetSession.cs` — migrate off primary constructor; delete `SkipTelnetCommandAsync`; read loop calls `_negotiator.HandleIacAsync`; `SetEchoAsync` delegates to `_negotiator.RequestLocalAsync`; add `TerminalWidth`/`TerminalHeight`; add `StartNegotiationAsync`
- [ ] `TelnetListener.cs` — migrate off primary constructor; call `session.StartNegotiationAsync(ct)` after construction
- [ ] `ISession.cs` — add `int TerminalWidth { get; }` / `int TerminalHeight { get; }` with XML docs
- [ ] `ConsoleSession.cs` — implement via real `Console.WindowWidth`/`WindowHeight`, fallback to 80/20 on throw
- [ ] `SharpMud.Adapters.Telnet.csproj` — add `InternalsVisibleTo` for the test project

### Logging infrastructure (new)

- [ ] `Directory.Packages.props` — add `Serilog`, `Serilog.Sinks.Console`, `Serilog.Extensions.Logging`, `LayeredCraft.StructuredLogging`
- [ ] `SharpMud.Adapters.Telnet.csproj` — add `Microsoft.Extensions.Logging.Abstractions`
- [ ] `Program.cs` — build Serilog logger (`WriteTo.Console()`), `services.AddLogging(builder => builder.AddSerilog(...))`
- [ ] Log call sites: `TelnetOptionNegotiator` (unregistered-option refusal, Debug), `TelnetSession`/`NawsOptionHandler` (negotiated size, Information) — via `LayeredCraft.StructuredLogging` extensions only

### `TelnetHostContext` parameter object (new)

- [ ] `src/SharpMud.Host/TelnetHostContext.cs` — `sealed record` (World, Parser, Registry, Repository, StartingRoom, Port, Logger)
- [ ] `HostRunner.RunTelnetAsync(TelnetHostContext, CancellationToken)` — refactor from 7 params to 2
- [ ] `HostRunner.HandleConnectionAsync(ISession, TelnetHostContext, CancellationToken)` — refactor to 3 params
- [ ] `Program.cs` call site updated

### Docs

- [ ] `docs/networking.md` — Open Items updated (already done — links ADR-0002)
- [ ] `docs/research/wheelmud-findings.md` — Decisions section updated (already done — links ADR-0002)
- [ ] `docs/adr/README.md` / `docs/plans/README.md` index rows (already done)

## Critical files

New:
- `src/SharpMud.Adapters.Telnet/Negotiation/OptionSide.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/TelnetOptionState.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/TelnetOptionCode.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/ITelnetOptionHandler.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/EchoOptionHandler.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/NawsOptionHandler.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/ITelnetByteSink.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/TelnetOptionNegotiator.cs`
- `src/SharpMud.Host/TelnetHostContext.cs`
- `tests/SharpMud.Adapters.Telnet.Tests/Negotiation/TelnetOptionNegotiatorTests.cs`
- `tests/SharpMud.Adapters.Telnet.Tests/Negotiation/NawsOptionHandlerTests.cs`
- `tests/SharpMud.Adapters.Telnet.Tests/TelnetSessionNegotiationTests.cs`

Modified:
- `src/SharpMud.Adapters.Telnet/TelnetSession.cs`
- `src/SharpMud.Adapters.Telnet/TelnetListener.cs`
- `src/SharpMud.Adapters.Telnet/SharpMud.Adapters.Telnet.csproj`
- `src/SharpMud.Engine/Sessions/ISession.cs`
- `src/SharpMud.Adapters.Cli/ConsoleSession.cs`
- `src/SharpMud.Host/HostRunner.cs`
- `src/SharpMud.Host/Program.cs`
- `Directory.Packages.props`

## Test plan

`tests/SharpMud.Adapters.Telnet.Tests/` currently has one file
(`TelnetListenerTests.cs`) and zero existing `TelnetSession` tests:

- `Negotiation/TelnetOptionNegotiatorTests.cs` — pure unit (fake
  `ITelnetByteSink`/`ITelnetOptionHandler`, `MemoryStream`): RFC 1143
  transition table coverage (NO+enable → DO/WILL; YES+enable → no-op;
  WANTNO+enable → queues opposite; WILL received while WANTYES →
  transitions without re-sending; unregistered code → WONT/DONT).
- `Negotiation/NawsOptionHandlerTests.cs` — pure unit: normal payload;
  0-width/height → defaults; sub-minimum → clamped; short payload →
  ignored.
- `TelnetSessionNegotiationTests.cs` — real-loopback integration
  (extends `TelnetListenerTests`'s pattern): end-to-end NAWS negotiation
  updates `TerminalWidth`/`TerminalHeight`; plain text still reads
  correctly interleaved with IAC bytes; unknown option → refusal;
  `SetEchoAsync` wire-byte regression test.

## Verification

- [ ] `dotnet test` (all projects) green, including new tests above.
- [ ] Manual smoke test with a real telnet client against `--telnet`
      mode: connect, confirm NAWS negotiation happens, confirm normal
      play (movement/look/chat) unaffected, confirm password-prompt echo
      suppression still works, confirm Serilog console output appears.
- [ ] `docs/networking.md` Open Items reflect final state.
