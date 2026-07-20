# Networking

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [commands.md](commands.md) for how input flows once read,
and [combat.md](combat.md) for disconnect-during-combat handling.

## Transport Abstraction

```csharp
public interface ISession
{
    string SessionId { get; }
    bool IsConnected { get; }
    int TerminalWidth { get; }  // negotiated async via NAWS on Telnet; defaults to 80
    int TerminalHeight { get; } // defaults to 20
    ValueTask<string?> ReadLineAsync(CancellationToken ct);
    ValueTask WriteLineAsync(string text, CancellationToken ct);
    ValueTask WriteAsync(string text, CancellationToken ct); // no trailing newline
    ValueTask DisconnectAsync(string? reason, CancellationToken ct);
}
```

Engine depends only on `ISession` — never on `Console`, `Socket`, or
`TcpClient` directly, anywhere. Out-of-band pushes (room broadcasts, combat
messages arriving while a player is mid-input) go through `WriteLineAsync` —
the Engine doesn't need to know whether that's an interruption on a real
terminal or just a queued line on stdout for v1.

## Adapter Plan

1. **`SharpMud.Adapters.Cli`** ✅ — implements `ISession` over
   `Console.In`/`Console.Out`. Single process, single local player, fast
   iteration. `AddSharpMudCliTransport` registers `CliTransportBackgroundService`,
   used when the composition root selects CLI mode.
2. **`SharpMud.Adapters.Telnet`** ✅ — `TelnetSession` (raw TCP via
   `TcpClient`/`NetworkStream`, line-based I/O) + `TelnetListener` (accepts
   connections, yields one `ISession` per client via `IAsyncEnumerable`).
   Real IAC option negotiation (RFC 1143 "Q-Method" core, Echo, NAWS) via
   `TelnetOptionNegotiator` — see
   [ADR-0002](adr/0002-telnet-protocol-negotiation.md). **MCCP/MXP/TermType
   still not negotiated** — deferred, see Open Items; WheelMUD's
   `Server/Telnet/` (docs/research/wheelmud-findings.md) is the reference to
   consult when those are actually needed. `AddSharpMudTelnetTransport(port)`
   registers `TelnetTransportBackgroundService`, used when the composition
   root selects Telnet mode (the sample ruleset does this via `--telnet [port]`,
   default 4000).
3. **`SharpMud.Adapters.Ssh`** (later) — secure terminal access.
4. **`SharpMud.Adapters.WebSocket`** (later) — browser play via xterm.js.

Adding a transport is additive — a new project implementing `ISession` — and
never requires changes to game logic, command parsing, or world state (see
[architecture.md](architecture.md) for the enforced dependency direction that
guarantees this). Confirmed in practice: the Telnet adapter required zero
changes to `SharpMud.Engine` or the sample ruleset.

## Multi-Session Host

The per-connection read-eval loop is `SessionLoop.RunAsync` (in
`SharpMud.Hosting`), shared by every transport. For Telnet,
`TelnetTransportBackgroundService` accepts connections in a loop and spawns
one `SessionLoop.RunAsync` task per connection against the same shared
`World`/`IGameLoop`/`ICommandRegistry` — this is what makes concurrent players
actually see and interact with each other (confirmed via a live two-client
smoke test: both players saw each other in `look`, received each other's
`say` output, and both received the wandering NPC's room-broadcast message).
Each connection is wrapped in a try/catch so one bad session can't take down
the listener or other connections (same exception-isolation principle as
command execution, see [architecture.md](architecture.md)).

New Telnet connections are prompted for a name (`"Name: "`) before a player
`Thing` is created — this is a placeholder for real login (see
[accounts-auth.md](accounts-auth.md)'s username/password login prompt),
not auth.

## Sequence: Player Disconnects Mid-Fight

(Full combat-side handling documented in [combat.md](combat.md); networking's
role in the sequence:)

1. The transport adapter detects the underlying stream closed/EOF, calls
   `ISession.DisconnectAsync`.
2. `SessionLoop` catches this, fires a `PlayerDisconnectedEvent`
   consumed by Engine's disconnect handler.

## Reconnect / Session Resumption ✅ (ADR-0004)

Implemented via a `ConnectionState` `OptimizedEnum` (`Playing`/`Linkdead`,
`src/SharpMud.Engine/Sessions/ConnectionState.cs`) on `PlayerBehavior`, not a
WheelMUD-style polymorphic per-state class hierarchy — see
[ADR-0004](adr/0004-session-state-machine-and-reconnect.md) for why that
lighter shape was chosen.

- A dropped connection (not an explicit `quit`) transitions the player to
  `Linkdead` (`SessionLoop`'s `finally`) instead of immediately removing
  their `Thing` from the world — they stay visibly present (`look`/`who`)
  until either a reconnect or the grace window expires.
- Reconnecting with the same username/password (see
  [accounts-auth.md](accounts-auth.md)) while `Linkdead` transitions back to
  `Playing` and resumes the same `Thing` in place (`LoginFlow
  .LoginExistingAsync`), printing `"Welcome back."` — this is the mechanism
  that makes the linkdead-combat grace period in [combat.md](combat.md)
  meaningful, a player who reconnects in time resumes their `CombatEncounter`
  in progress.
- If the window elapses without a reconnect, `LinkdeadSweeper`
  (`src/SharpMud.Engine/Sessions/LinkdeadSweeper.cs`, an `ITickable`
  registered with `IGameLoop` the same way as `CombatManager`/
  `WanderManager`) saves and force-removes the player — the same cleanup
  `SessionLoop` used to do immediately pre-ADR-0004.
- `ReconnectPolicy.GraceWindow` (`src/SharpMud.Engine/Sessions
  /ReconnectPolicy.cs`, currently 3 minutes) is the single constant shared by
  the sweeper and `CombatManager`'s linkdead handling — resolves this doc's
  former open question of whether the two grace windows are the same
  constant: they now literally are. Not a tuned final value (same caveat as
  `LoginFlow.MaxPasswordAttempts`).
- An explicit `quit` still bypasses `Linkdead` entirely and removes the
  player immediately, matching pre-ADR-0004 behavior — the grace period
  only applies to a connection that was lost, not one the player ended on
  purpose.
- Unchanged: if the character is still actively `Playing` with a live,
  connected session, a second login attempt is still rejected
  (`"That character is already logged in."`) rather than stealing the
  session — WheelMUD's "freshest login wins" behavior was deliberately not
  adopted here (see ADR-0004's Scope).

Verified live over real Telnet: a player mid-session, disconnected without
`quit`, remains visible to other players (`look`/`who`) until reconnected;
reconnecting with the same username/password resumes the same character in
place and prints `"Welcome back."`; `quit` still removes the character
immediately with no linkdead window.

## Idle Timeout

Players idle (no command sent) for N minutes are disconnected — frees the
connection slot, classic MUD behavior on public servers. Exact minutes TBD.

## Open Items

- ~~Exact reconnect grace-window duration~~ — resolved by ADR-0004:
  `ReconnectPolicy.GraceWindow`, 3 minutes, shared with combat's linkdead
  handling. Still a placeholder value, not tuned from real playtesting.
- Exact idle-timeout duration. Not implemented — a Telnet connection stays
  open indefinitely until the client disconnects.
- Concurrent-connection limits and backpressure — not yet specified;
  `TelnetListener` currently accepts without limit.
- MCCP/MXP/TermType telnet protocol negotiation — still deferred (see
  [ADR-0002](adr/0002-telnet-protocol-negotiation.md), which covers IAC
  negotiation core + Echo + NAWS only; those three are implemented,
  verified via unit tests and a live smoke test — see
  [PLAN-0002](plans/0002-telnet-protocol-negotiation.md)).
- Real login/auth on connect — currently just a name prompt, no identity
  verification; see [accounts-auth.md](accounts-auth.md).
