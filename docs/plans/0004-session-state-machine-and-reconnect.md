# [PLAN-0004] Session State Machine + Linkdead Reconnect

**Implements:** [ADR-0004](../adr/0004-session-state-machine-and-reconnect.md)

**Status:** Done

**Last updated:** 2026-07-17

## Goal

A disconnected player stays in their room as `Linkdead` instead of
vanishing instantly; reconnecting with the same username/password within
`ReconnectPolicy.GraceWindow` resumes the same `Thing` in place (including
an in-progress combat encounter, which freezes instead of ending); if the
window expires unreconnected, the player is force-removed/saved exactly as
today's immediate-disconnect behavior did. "Done" = all tasks below
checked off, verified live over Telnet per Verification below.

## Scope

Per ADR-0004's Decision Outcome: `Playing`/`Linkdead` state on
`PlayerBehavior`, the sweep, `LoginFlow` reconnect, `CombatManager`
freeze/abandon. Not in scope: idle-timeout disconnection, concurrent
-connection limits, changing the existing "reject if actively
`Playing`-and-connected" rule, `(linkdead)` markers in `look`/`who` output
(nice-to-have, not required for the mechanism to work — do only if
trivial once the core is in, don't let it block the rest).

## Tasks

- [x] `SharpMud.Engine.csproj`: add `LayeredCraft.OptimizedEnums`
      `PackageReference` (already a `Directory.Packages.props` version
      entry, just not referenced from Engine yet).
- [x] New `src/SharpMud.Engine/Sessions/ConnectionState.cs` —
      `OptimizedEnum<ConnectionState, int>`, values `Playing`(1)/
      `Linkdead`(2), `CanTransitionTo(ConnectionState next)` allowing only
      `Playing→Linkdead` and `Linkdead→Playing`.
- [x] New `src/SharpMud.Engine/Sessions/ReconnectPolicy.cs` — `static
      readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(3)` (placeholder
      default, not tuned — same spirit as `LoginFlow.MaxPasswordAttempts`).
- [x] `PlayerBehavior`: add `ConnectionState ConnectionState { get; private
      set; } = ConnectionState.Playing;` and `DateTimeOffset?
      LinkdeadSinceUtc { get; private set; }`, plus `EnterLinkdead(DateTimeOffset
      now)`/`Reconnect(DateTimeOffset now)` methods that call
      `ConnectionState.CanTransitionTo` and throw
      `InvalidOperationException` on an illegal transition.
- [x] `PlayerBehaviorConfiguration`: `Ignore(x => x.ConnectionState)` and
      `Ignore(x => x.LinkdeadSinceUtc)` — runtime-only, same treatment as
      `Session` (ADR-0004 Negative Consequences: this state doesn't survive
      a restart, by design).
- [x] New `src/SharpMud.Engine/Sessions/LinkdeadSweeper.cs` — `ITickable`,
      ctor `(IWorld world, IThingRepository repository)`, each tick scans
      `world.AllWithBehavior<PlayerBehavior>()` for `ConnectionState ==
      Linkdead` past `ReconnectPolicy.GraceWindow`, then does what
      `SessionLoop`'s `finally` used to do inline: save, remove from
      parent, unregister.
- [x] `SessionLoop.finally`: keep the unconditional `SaveTreeAsync` (crash
      safety, unchanged), replace the `player.Parent?.Remove` +
      `world.Unregister` lines with
      `player.FindBehavior<PlayerBehavior>()?.EnterLinkdead(DateTimeOffset.UtcNow)`
      — the `Thing` now stays live in its room until the sweeper (or a
      reconnect) resolves it.
- [x] `LoginFlow.LoginExistingAsync`: branch on
      `playerBehavior.ConnectionState` — `Playing` keeps today's exact
      "already logged in" rejection when `Session is { IsConnected: true
      }`; `Linkdead` calls `playerBehavior.Reconnect(DateTimeOffset.UtcNow)`
      after password verification succeeds and returns the existing
      `Thing` (a short "Welcome back." message before returning).
- [x] `CombatManager.OnTickAsync`: replace the `session is null → end
      encounter immediately` stub with a check on the attacker's
      `ConnectionState` — `Linkdead` and within `ReconnectPolicy.GraceWindow`
      of `LinkdeadSinceUtc` → skip this round (frozen, no resolution, no
      removal); `Linkdead` and past the grace window → `EndEncounter`
      (grace genuinely expired, same outcome as before this slice, just
      delayed).
- [x] Host wiring (`Program.cs`/`HostRunner.cs`, wherever `WanderManager`/
      `CombatManager` are registered with `IGameLoop` today): register
      `LinkdeadSweeper` the same way.

## Docs

- [x] `docs/networking.md`: close the "reconnect grace-window duration" Open
      Item (now `ReconnectPolicy.GraceWindow`, 3 minutes), update the
      Reconnect / Session Resumption section to describe what's actually
      implemented, link ADR-0004.
- [x] `docs/combat.md`: replace "Disconnect Mid-Fight (stub)" with what's
      actually implemented (freeze on `Linkdead`, abandon past grace
      window), update the matching Open Item, link ADR-0004.
- [x] `docs/accounts-auth.md`: note the `Linkdead`-branch addition to
      `LoginExistingAsync` alongside the existing "already logged in"
      behavior it leaves unchanged.
- [x] `docs/adr/README.md` / `docs/plans/README.md`: add index rows for
      ADR-0004/PLAN-0004.
- [x] `docs/plans/0001-wheelmud-reconciliation-roadmap.md`: check off
      Slice 2.

## Critical files

New:
- `src/SharpMud.Engine/Sessions/ConnectionState.cs`
- `src/SharpMud.Engine/Sessions/ReconnectPolicy.cs`
- `src/SharpMud.Engine/Sessions/LinkdeadSweeper.cs`
- `tests/SharpMud.Engine.Tests/Sessions/LinkdeadSweeperTests.cs`
- `tests/SharpMud.Engine.Tests/Behaviors/PlayerBehaviorConnectionStateTests.cs`

Modified:
- `src/SharpMud.Engine/SharpMud.Engine.csproj`
- `src/SharpMud.Engine/Behaviors/PlayerBehavior.cs`
- `src/SharpMud.Persistence/Configurations/PlayerBehaviorConfiguration.cs`
- `src/SharpMud.Host/SessionLoop.cs`
- `src/SharpMud.Host/LoginFlow.cs`
- `src/SharpMud.Ruleset.Classic/CombatManager.cs`
- `src/SharpMud.Host/Program.cs` and/or `HostRunner.cs` (game-loop wiring)
- `docs/networking.md`, `docs/combat.md`, `docs/accounts-auth.md`,
  `docs/adr/README.md`, `docs/plans/README.md`,
  `docs/plans/0001-wheelmud-reconciliation-roadmap.md`

## Test plan

- Unit: `ConnectionState.CanTransitionTo` — both legal transitions true,
  both illegal (same-state, and any transition not explicitly listed)
  false.
- Unit: `PlayerBehavior.EnterLinkdead`/`Reconnect` — happy path mutates
  state + timestamp correctly; calling either from an illegal source state
  throws.
- Unit: `LinkdeadSweeper.OnTickAsync` — a `Linkdead` player under the grace
  window is untouched; one past the grace window gets saved, removed from
  parent, and unregistered; a `Playing` player is untouched regardless of
  timestamp.
- Unit: `CombatManager.OnTickAsync` — attacker `Linkdead` within grace
  window: encounter survives, no round resolves, no session writes; past
  grace window: encounter removed.
- Existing `LoginFlow`/`SessionLoop` test coverage (if any) extended for
  the new `Linkdead`-reconnect branch; regression-check the
  still-Playing-and-connected rejection path is unchanged.

## Verification

Real manual check over Telnet (this repo's established pattern for
session-facing changes, per `testing.md`):

1. Connect, log in as a fresh character, confirm normal play.
2. Disconnect (close the client without `quit`). Confirm via a second
   client's `look`/`who` that the character is still visibly present.
3. Reconnect within the grace window using the same username/password;
   confirm it resumes the same character/room rather than looking like a
   fresh login, and prints the "Welcome back." message.
4. Start a fight, disconnect mid-combat, reconnect within the grace
   window; confirm the encounter resumed rather than having ended.
5. Disconnect and let the grace window fully elapse (or temporarily lower
   `ReconnectPolicy.GraceWindow` for the test run); confirm the character
   is removed from the room and a subsequent login with that username
   creates/treats it as a fresh login, not a reconnect.
6. Confirm the existing "already logged in" rejection is unchanged: log in
   as A on one client, attempt to log in as A on a second client while the
   first is still actively connected (not linkdead) — still rejected.

## Open questions / blockers

- `ReconnectPolicy.GraceWindow`'s 3-minute default is a placeholder, not a
  tuned value — same caveat as `LoginFlow.MaxPasswordAttempts`. Revisit if
  real playtesting suggests otherwise.
- `(linkdead)` markers in `look`/`who` — deferred, see Scope.
