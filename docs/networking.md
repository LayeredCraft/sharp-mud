# Networking

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [commands.md](commands.md) for how input flows once read,
and [combat.md](combat.md) for disconnect-during-combat handling.

## Transport Abstraction

```csharp
public interface ISession
{
    string SessionId { get; }
    ValueTask<string?> ReadLineAsync(CancellationToken ct);
    ValueTask WriteLineAsync(string text, CancellationToken ct);
    ValueTask WriteAsync(string text, CancellationToken ct); // no trailing newline
    bool IsConnected { get; }
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
   iteration. `Host` uses this when run with no arguments.
2. **`SharpMud.Adapters.Telnet`** ✅ — `TelnetSession` (raw TCP via
   `TcpClient`/`NetworkStream`, line-based I/O with IAC (0xFF) byte sequences
   stripped from input) + `TelnetListener` (accepts connections, yields one
   `ISession` per client via `IAsyncEnumerable`). **Does not negotiate MCCP/
   MXP/NAWS** — that's still deferred, see Open Items; WheelMUD's
   `Server/Telnet/` (docs/research/wheelmud-findings.md) is the reference to
   consult when it's actually needed. `Host` uses this when run with
   `--telnet [port]` (default 4000).
3. **`SharpMud.Adapters.Ssh`** (later) — secure terminal access.
4. **`SharpMud.Adapters.WebSocket`** (later) — browser play via xterm.js.

Adding a transport is additive — a new project implementing `ISession` — and
never requires changes to game logic, command parsing, or world state (see
[architecture.md](architecture.md) for the enforced dependency direction that
guarantees this). Confirmed in practice: the Telnet adapter required zero
changes to `SharpMud.Engine` or `SharpMud.Ruleset.Classic`.

## Multi-Session Host

`Host`'s per-connection read-eval loop is `SessionLoop.RunAsync` (extracted
from what used to be inline in `Program.cs`), shared by every transport. For
Telnet, `HostRunner.RunTelnetAsync` accepts connections in a loop and spawns
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
2. `Host`'s session-loop catches this, fires a `PlayerDisconnectedEvent`
   consumed by Engine's disconnect handler.

## Reconnect / Session Resumption

Resume-within-grace-window: reconnecting and logging back in with the same
username/password (see [accounts-auth.md](accounts-auth.md)) within N minutes
of a disconnect
resumes the same character/session rather than requiring a fresh login. This
is the mechanism that makes the linkdead-combat grace period in
[combat.md](combat.md) meaningful — a player who reconnects in time resumes
their `CombatEncounter` in progress. After the window expires, it's a fresh
login and any abandoned encounter has already force-ended. Exact grace-window
duration TBD — likely the same constant as (or close to) the combat linkdead
grace period, but not necessarily identical (session resumption may
reasonably outlast combat's grace period).

## Idle Timeout

Players idle (no command sent) for N minutes are disconnected — frees the
connection slot, classic MUD behavior on public servers. Exact minutes TBD.

## Open Items

- Exact reconnect grace-window duration, and whether it's the same constant
  as the combat linkdead grace period or configured separately. Not
  implemented — a fresh Telnet connection always creates a new player
  `Thing`, there's no reconnect-to-existing-character path yet.
- Exact idle-timeout duration. Not implemented — a Telnet connection stays
  open indefinitely until the client disconnects.
- Concurrent-connection limits and backpressure — not yet specified;
  `TelnetListener` currently accepts without limit.
- MCCP/MXP/NAWS telnet protocol negotiation — IAC negotiation core + NAWS
  are designed in [ADR-0002](adr/0002-telnet-protocol-negotiation.md)
  (status: Proposed, not yet implemented); MCCP/MXP/TermType remain
  deferred beyond that.
- Real login/auth on connect — currently just a name prompt, no identity
  verification; see [accounts-auth.md](accounts-auth.md).
