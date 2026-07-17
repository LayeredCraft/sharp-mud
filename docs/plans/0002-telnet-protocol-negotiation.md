# [PLAN-0002] Telnet Protocol Negotiation (IAC/Q-Method core + NAWS)

**Implements:** [ADR-0002](../adr/0002-telnet-protocol-negotiation.md)

**Status:** Done

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

- [x] `OptionSide.cs` — `internal enum OptionSide { No, WantNo, Yes, WantYes }`
- [x] `TelnetOptionState.cs` — `internal sealed class`, `UsState`/`UsQueuedOpposite`/`HimState`/`HimQueuedOpposite`
- [x] `TelnetOptionCode.cs` — `internal enum TelnetOptionCode : byte { Echo = 1, Naws = 31 }` + XML doc noting future codes (SuppressGoAhead=3, TermType=24, MCCP2=86, MXP=91)
- [x] `TelnetCommandByte.cs` — telnet command byte constants (added during implementation; not in original plan, but a natural small addition alongside the enum)
- [x] `ITelnetOptionHandler.cs` — `Code`, `OnSubnegotiationAsync`
- [x] `EchoOptionHandler.cs` — no-op subnegotiation (RFC 857)
- [x] `NawsOptionHandler.cs` — `Width`/`Height`, defaults 80×20, clamp floor 20×6, ignore malformed/short payloads; also takes a `sessionId`/`ILogger<TelnetSession>` to log negotiated size changes
- [x] `ITelnetByteSink.cs` — `WriteAsync(byte[], CancellationToken)`
- [x] `TelnetOptionNegotiator.cs` — `HandleIacAsync`, `RequestLocalAsync`, `RequestRemoteAsync`; WILL/WONT/DO/DONT transition tables copied from `WheelMUD/src/Server/Telnet/TelnetOption.cs` (~lines 172–413); unregistered option code → ephemeral "don't want it" state → automatic WONT/DONT

### Existing file changes

- [x] `TelnetSession.cs` — migrated off primary constructor; deleted `SkipTelnetCommandAsync`; read loop calls `_negotiator.HandleIacAsync`; `SetEchoAsync` delegates to `_negotiator.RequestLocalAsync(Echo, enable: !enabled, ct)` (note: the boolean is inverted relative to "enable this option" - `enabled` means normal client echo, i.e. the option is *off* from our side; caught by the manual smoke test, not the unit tests); added `TerminalWidth`/`TerminalHeight`; added `StartNegotiationAsync`
- [x] `TelnetListener.cs` — migrated off primary constructor; calls `session.StartNegotiationAsync(ct)` after construction; also gained an internal `LocalPort` property (needed by the new integration tests to connect a real client when constructed with port 0)
- [x] `ISession.cs` — added `int TerminalWidth { get; }` / `int TerminalHeight { get; }` with XML docs
- [x] `ConsoleSession.cs` — implemented via real `Console.WindowWidth`/`WindowHeight`, fallback to 80/20 on `IOException`/`ArgumentOutOfRangeException`
- [x] `SharpMud.Adapters.Telnet.csproj` — added `InternalsVisibleTo` for the test project, and (found during test-writing, not originally planned) `InternalsVisibleTo` for `DynamicProxyGenAssembly2` - NSubstitute needs this grant to proxy `internal` interfaces

### Logging infrastructure (new)

- [x] `Directory.Packages.props` — added `Serilog`, `Serilog.Sinks.Console`, `Serilog.Extensions.Logging`, `LayeredCraft.StructuredLogging` (dropped the originally-planned explicit `Microsoft.Extensions.Logging.Abstractions` reference - .NET 11's shared framework already includes it, NuGet warned NU1510)
- [x] `Program.cs` — build Serilog logger (`WriteTo.Console()`), `services.AddLogging(builder => builder.AddSerilog(...))`; **also added `MinimumLevel.Override("Microsoft.EntityFrameworkCore", Warning)`** - not in the original plan, but registering `ILoggerFactory` for the first time caused EF Core to start logging every SQL command at Information, flooding the console; found and fixed during the manual smoke test
- [x] Log call sites: `TelnetOptionNegotiator` (unregistered-option refusal, Debug), `NawsOptionHandler` (negotiated size, Information) — via `LayeredCraft.StructuredLogging` extensions only; verified both appear correctly in a live smoke test

### `TelnetHostContext` parameter object (new)

- [x] `src/SharpMud.Host/TelnetHostContext.cs` — `sealed record` (World, Parser, Registry, Repository, StartingRoom, Port, Logger)
- [x] `HostRunner.RunTelnetAsync(TelnetHostContext, CancellationToken)` — refactored from 7 params to 2
- [x] `HostRunner.HandleConnectionAsync(ISession, TelnetHostContext, CancellationToken)` — refactored to 3 params; also now logs session errors via `context.Logger.Error(ex, ...)` instead of `Console.Error.WriteLineAsync`
- [x] `Program.cs` call site updated

### Docs

- [x] `docs/networking.md` — `ISession` code sample updated with the two new members, Adapter Plan bullet updated, Open Items split (NAWS/Echo implemented, MCCP/MXP/TermType still deferred)
- [x] `docs/research/wheelmud-findings.md` — Decisions section updated, links ADR-0002
- [x] `docs/adr/README.md` / `docs/plans/README.md` index rows

## Critical files

New:
- `src/SharpMud.Adapters.Telnet/Negotiation/OptionSide.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/TelnetOptionState.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/TelnetOptionCode.cs`
- `src/SharpMud.Adapters.Telnet/Negotiation/TelnetCommandByte.cs`
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
- `src/SharpMud.Host/SharpMud.Host.csproj`
- `Directory.Packages.props`
- `tests/SharpMud.Adapters.Telnet.Tests/SharpMud.Adapters.Telnet.Tests.csproj` (added NSubstitute)
- `tests/SharpMud.Adapters.Telnet.Tests/TelnetListenerTests.cs` (constructor signature change)

## Test plan

`tests/SharpMud.Adapters.Telnet.Tests/` had one existing file
(`TelnetListenerTests.cs`) and zero existing `TelnetSession` tests before
this plan - all of the following is new coverage:

- `Negotiation/TelnetOptionNegotiatorTests.cs` (6 tests) — RFC 1143
  transition table coverage: sends DO on first request; doesn't resend
  once Yes; a WILL response settles without a second send; a request made
  while a prior one is unconfirmed queues and fires once that one settles;
  unregistered option codes get WONT/DONT.
- `Negotiation/NawsOptionHandlerTests.cs` (4 tests) — normal payload,
  zero-value defaults, sub-minimum clamping, short/malformed payload
  ignored.
- `TelnetSessionNegotiationTests.cs` (4 tests) — real-loopback integration
  extending `TelnetListenerTests`'s pattern: end-to-end NAWS negotiation,
  plain text still reads correctly interleaved with IAC bytes, unknown
  option refusal over a real socket, `SetEchoAsync` wire-byte regression
  (this last one required simulating a compliant client's `DO`/`WONT`
  acknowledgement between the two `SetEchoAsync` calls - an unacknowledged
  request correctly just queues per the Q-Method rather than resending,
  which the test needed to account for rather than assume away).

All 15 tests in this project pass; full solution (86 tests) passes.

## Verification

- [x] `dotnet test` (all projects) green - 86/86 passing.
- [x] Manual smoke test with a real Python-scripted telnet client against
      `--telnet` mode: confirmed `IAC DO NAWS` sent on connect, negotiated
      100×30 window size logged correctly, full username/character-creation/
      password flow worked (including `IAC WILL ECHO`/`IAC WONT ECHO` for
      password masking), `look` and normal gameplay output worked, and
      Serilog console output appeared cleanly (after fixing the EF Core
      log-level override described above).
- [x] `docs/networking.md` Open Items reflect final state.
