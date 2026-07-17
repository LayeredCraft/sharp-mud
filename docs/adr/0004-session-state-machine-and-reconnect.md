# [ADR-0004] Session State Machine + Linkdead Reconnect

**Status:** Accepted

**Date:** 2026-07-17

**Decision Makers:** solo

## Context

Per ADR-0001, this is Slice 2 of the WheelMUD reconciliation roadmap.
Researched WheelMUD's `src/ConnectionStates/` (1,094 lines, 15 files): a
polymorphic `SessionState` base class (`HandshakingState` →
`ConnectedState` → `LoginState`/`CreationState` → `PlayingState`), each
state a separate MEF-discovered class implementing `ProcessInput`/`Begin`/
`BuildPrompt`. Reconnect is handled by `PlayerManager.AttachPlayerToSession`:
if a character is already live under another session, the *new* session
steals it (kicks the old one) — "freshest login wins."

sharp-mud's current login/session handling (`LoginFlow.cs`, `SessionLoop.cs`)
is procedural, not stateful:

- `LoginFlow` already partially reconciles: it checks `World` for a live
  player by username before falling back to the repository, and rejects a
  second login attempt against a character with `Session is { IsConnected:
  true }` ("That character is already logged in.").
- `SessionLoop`'s `finally` block, on any disconnect, **immediately** saves,
  removes the player `Thing` from its room, and unregisters it from
  `World` — there is no window in which a disconnected player is still
  "there." This is why reconnect and combat's linkdead grace period
  (`docs/combat.md`: "`CombatState.Linkdead`... not implemented... needs
  session reconnect/resumption, which doesn't exist yet") are both stubs
  today: there's no in-world state to reconnect *to*.

`docs/networking.md`'s Open Items already names the two things this ADR
resolves: "Exact reconnect grace-window duration... not implemented — a
fresh Telnet connection always creates a new player `Thing`" and combat's
grace period being blocked on this exact gap.

## Decision Drivers

- `design-decisions.md` rule 2: no new `Thing` subtypes — this only
  concerns session/connection *state*, not entity shape, so it doesn't
  interact with that rule directly, but any new state must live on
  `PlayerBehavior` (composition), not a new `Thing` variant.
- `SharpMud.Engine` must never reference `SharpMud.Ruleset.Classic` — the
  generic "is this player currently connected, linkdead, or gone" concept
  belongs in Engine; combat's specific reaction to linkdead (freezing an
  encounter) stays in `SharpMud.Ruleset.Classic`'s `CombatManager`, which
  is already allowed to depend on Engine.
- The repo already has a working tick-driven sweep pattern for exactly
  this shape of problem: `CombatManager.OnTickAsync` scans all active
  encounters every tick; `WanderManager` does the same for wandering NPCs.
  A grace-period expiry check is the same shape of problem (scan, check a
  timestamp, act), not a new mechanism.
- The repo already has a real "smart enum as a small state machine"
  precedent: `Race`/`CharacterClass` (`SharpMud.Ruleset.Classic`) are
  `LayeredCraft.OptimizedEnums` singletons rather than plain enums,
  specifically so behavior/data can live on the enum instance itself
  (see `docs/character.md`). The user directed this ADR to use the same
  library for connection state, encoding legal transitions on the enum
  type itself (mirroring the state-machine usage pattern from
  Ardalis.SmartEnum's README, which `LayeredCraft.OptimizedEnums` is
  loosely based on) rather than a bare `enum` or a WheelMUD-style
  polymorphic class-per-state hierarchy.
- No migrations (`persistence.md`): schema is drop-and-recreate in early
  dev, so adding fields to `PlayerBehavior` has no migration ceremony.
- `PlayerBehavior.Session` is already `Ignore`d in
  `PlayerBehaviorConfiguration` as runtime-only, session-lifecycle state
  that never survives a process restart — new connection-state fields are
  the same category and follow the same treatment.

## Considered Options

1. **Do nothing further** — leave `LoginFlow`'s existing "already logged
   in" check as the entire reconnect story. Rejected: this is the status
   quo the roadmap slice exists to move past, and leaves combat's linkdead
   grace period permanently stubbed.
2. **Full polymorphic `SessionState` hierarchy**, porting WheelMUD's shape
   (separate classes per state, `ProcessInput`/`Begin`/`BuildPrompt`,
   session-driven transitions) to replace `LoginFlow`/`SessionLoop`'s
   procedural loop.
3. **Lightweight state on `PlayerBehavior`**: a `ConnectionState`
   `OptimizedEnum` (`Playing`/`Linkdead`) with transition legality encoded
   as a method on the enum type, plus a `LinkdeadSinceUtc` timestamp.
   `SessionLoop`'s disconnect path transitions to `Linkdead` instead of
   immediately tearing the `Thing` down; a new `LinkdeadSweeper`
   (`ITickable`, mirroring `WanderManager`) force-removes+saves once a
   grace window elapses; `LoginFlow` reattaches a new session to a
   `Linkdead` character instead of rejecting it; `CombatManager` freezes
   (rather than immediately ending) an encounter while the attacker is
   `Linkdead`, and ends it only once the grace window actually expires.

## Decision Outcome

Chosen option: **"3 — lightweight `OptimizedEnum` state on
`PlayerBehavior`,"** confirmed with the user over option 2. sharp-mud's
pre-`Playing` flow (username/password/create) has no sub-states remotely
as complex as WheelMUD's (no account/character-select step, no creation
wizard) — a polymorphic per-state class hierarchy would be solving a
problem sharp-mud doesn't have. The actual gap (reconnect + linkdead grace)
is a genuine two-state machine (`Playing` ⇄ `Linkdead`), which
`LayeredCraft.OptimizedEnums` already has a precedent for in this repo
(`Race`/`CharacterClass`) and fits the tick-driven-sweep pattern
(`CombatManager`, `WanderManager`) already established for "scan
world state on every tick, act past a threshold."

`ConnectionState` (new, `SharpMud.Engine.Sessions`) is a
`LayeredCraft.OptimizedEnums` `OptimizedEnum<ConnectionState, int>` with
two singleton values, `Playing` and `Linkdead`, and a
`CanTransitionTo(ConnectionState next)` method that is the actual state
machine — the only two legal transitions are `Playing → Linkdead` and
`Linkdead → Playing`. `PlayerBehavior` gains `ConnectionState
ConnectionState` (defaults `Playing`) and `DateTimeOffset? LinkdeadSinceUtc`,
plus `EnterLinkdead(DateTimeOffset)`/`Reconnect(DateTimeOffset)` methods
that call `CanTransitionTo` before mutating — the transition guard lives
on the enum, the mutation lives on the behavior that owns the state, no
transition logic is duplicated in `SessionLoop`/`LoginFlow`/`CombatManager`
call sites.

A single `ReconnectPolicy.GraceWindow` constant (`SharpMud.Engine.Sessions`,
initially 3 minutes — a concrete, revisitable default in the same spirit
as `LoginFlow.MaxPasswordAttempts`, not a tuned final answer) is shared by
`LinkdeadSweeper` and `CombatManager`, resolving `networking.md`'s open
question of whether the reconnect grace window and combat's linkdead grace
period are the same constant: they now literally are.

**Scope for this slice**: the `Playing`/`Linkdead` state pair, the sweep,
`LoginFlow` reconnect-into-`Linkdead`, and `CombatManager` freezing/
abandoning an encounter on the same grace window. Explicitly **not**
touched: the existing "reject if actively `Playing`-and-connected" behavior
(WheelMUD's "freshest login wins" session-stealing is a different, already
-accepted design choice sharp-mud made and verified — see
`accounts-auth.md` — and isn't part of this slice's gap); idle-timeout
disconnection (separate open item, unrelated mechanism); concurrent
-connection limits (unrelated); SSH/WebSocket transports (unrelated).

The full execution breakdown lives in
[PLAN-0004](../plans/0004-session-state-machine-and-reconnect.md).

### Positive Consequences

- Reconnect actually works: a disconnected player reappears in the same
  room/state on reconnect within the grace window, instead of always
  getting a brand-new character-in-the-hub experience.
- Closes `docs/combat.md`'s stub: a disconnected attacker's encounter
  freezes instead of vanishing, and resumes cleanly if they reconnect in
  time.
- Reuses two already-established repo patterns (`OptimizedEnum` as a small
  state machine, tick-driven sweep) instead of introducing a third
  approach.
- Resolves `networking.md`'s open question about whether the reconnect and
  combat grace windows are the same constant.

### Negative Consequences

- A linkdead player still visibly occupies their room (`look`/`who`) for
  up to the grace window after disconnecting — correct classic-MUD
  behavior, but a real behavior change from today's "vanishes instantly."
- `ConnectionState`/`LinkdeadSinceUtc` are runtime-only (`Ignore`d in EF
  config, like `Session`) — a process restart while players are linkdead
  loses that state entirely (they come back as `Playing` with no memory of
  being linkdead). Acceptable: a process restart already drops all live
  sessions today, and persisting linkdead state across restarts isn't a
  goal of this slice.
- `LayeredCraft.OptimizedEnums` becomes a dependency of `SharpMud.Engine`,
  not just `SharpMud.Ruleset.Classic` — a real, if small, package-boundary
  change (an Engine-level state type is no longer plain-.NET-only).

## Pros and Cons of the Options

### Option 2: Full polymorphic `SessionState` hierarchy

- Good, because it's the closest 1:1 reconciliation with WheelMUD's actual
  shape.
- Bad, because it replaces a working, simple procedural loop
  (`LoginFlow`/`SessionLoop`) with a heavier framework to solve a
  state-count problem (2 states: playing, linkdead) that doesn't need
  polymorphism — sharp-mud's pre-login flow has no analog to WheelMUD's
  character-creation wizard sub-states.

### Option 3: Lightweight `OptimizedEnum` state (chosen)

- Good, because it fits two patterns already established in this repo
  (`OptimizedEnum` as small state machine, tick-driven sweep) instead of
  introducing a new one.
- Good, because it's a minimal, reversible diff — `LoginFlow`/`SessionLoop`
  keep their existing shape, gaining state transitions rather than being
  rewritten.
- Bad, because it doesn't generalize to a future state with real
  sub-states (e.g. a multi-step character-creation wizard) — acceptable,
  since nothing in the current roadmap calls for that, and `design
  -decisions.md` says not to design for hypothetical future requirements.

## Links

- [ADR-0001](0001-wheelmud-reconciliation-roadmap.md) — WheelMUD
  Reconciliation Roadmap (this is Slice 2).
- [PLAN-0004](../plans/0004-session-state-machine-and-reconnect.md) —
  execution plan for this decision.
- `docs/networking.md` — Open Items this ADR closes (reconnect grace
  window).
- `docs/combat.md` — "Disconnect Mid-Fight (stub)" this ADR closes.
- `docs/accounts-auth.md` — the existing "already logged in" rejection
  this ADR deliberately leaves unchanged.
- `docs/character.md` — prior `OptimizedEnum` precedent (`Race`/
  `CharacterClass`) this ADR follows.
- `WheelMUD/src/ConnectionStates/` — source researched for this decision.
- Ardalis.SmartEnum README, "Smart Enums as a state machine" — the pattern
  `LayeredCraft.OptimizedEnums` usage here is modeled on.
