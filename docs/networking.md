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

1. **`SharpMud.Adapters.Cli`** (v1) — implements `ISession` over
   `Console.In`/`Console.Out`. Single process, single local player, fast
   iteration.
2. **`SharpMud.Adapters.Telnet`** (later) — raw TCP, classic MUD client
   compatibility (Mudlet, TinTin++, etc).
3. **`SharpMud.Adapters.Ssh`** (later) — secure terminal access.
4. **`SharpMud.Adapters.WebSocket`** (later) — browser play via xterm.js.

Adding a transport is additive — a new project implementing `ISession` — and
never requires changes to game logic, command parsing, or world state (see
[architecture.md](architecture.md) for the enforced dependency direction that
guarantees this).

## Sequence: Player Disconnects Mid-Fight

(Full combat-side handling documented in [combat.md](combat.md); networking's
role in the sequence:)

1. The transport adapter detects the underlying stream closed/EOF, calls
   `ISession.DisconnectAsync`.
2. `Host`'s session-loop catches this, fires a `PlayerDisconnectedEvent`
   consumed by Engine's disconnect handler.

## Reconnect / Session Resumption

Resume-within-grace-window: reconnecting via the same OAuth identity (see
[accounts-auth.md](accounts-auth.md)) within N minutes of a disconnect
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
  as the combat linkdead grace period or configured separately.
- Exact idle-timeout duration.
- Concurrent-connection limits and backpressure once multiplayer transports
  land — not yet specified.
