# [PLAN-0005] Security Role Model + Moderation Commands

**Implements:** [ADR-0005](../adr/0005-security-role-model-and-moderation-commands.md)

**Status:** Not Started

**Last updated:** 2026-07-17

## Goal

A `MinorAdmin`/`FullAdmin`-gated set of moderation commands
(`boot`/`mute`/`unmute`/`announce`/`ban`/`unban`/`rolegrant`/`rolerevoke`)
works end-to-end over real Telnet, no command can be registered without an
explicit access-level declaration, mute/ban are enforced against the
right targets, and a `SHARPMUD_INITIAL_ADMIN` env var bootstraps the first
`FullAdmin` without an in-game path.

## Scope

Per ADR-0005's Decision Outcome. In scope: `SecurityRole` enum,
`RoleGuardedCommand`/`MuteGuardedCommand` decorators, the
`RegisterOpen`/`RegisterWithRole` registry split (and updating every
existing registration call site), `PlayerBehavior.Roles`/`IsMuted`/
`IsBanned`, the 8 moderation commands above, `LoginFlow` ban enforcement,
`SHARPMUD_INITIAL_ADMIN` bootstrap.

Explicitly deferred (per ADR-0005): `Find`/`Locate`/`GoTo`/`Control`
(need world/NPC lookup + puppeting), `Clone`/`Spawn` (need item/NPC
creation tooling), `Jail` (needs a cell-room concept), `Buff` (needs a
generic stat-modification system), `Relinquish` (Slice 4/builder-specific).
Audit logging of moderation actions — not in ADR-0005's scope, flagged as
an open item below.

## Tasks

### `SecurityRole` + registry

- [ ] New `src/SharpMud.Engine/Commands/SecurityRole.cs` — plain
      `[Flags] enum SecurityRole : uint` (`None`, `Mobile`, `Item`, `Room`,
      `TutorialPlayer`, `Player`, `Helper`, `Married`, `MinorBuilder`,
      `FullBuilder`, `MinorAdmin`, `FullAdmin`, `All`), XML doc comments
      per `documentation.md`'s new-public-member rule.
- [ ] New `src/SharpMud.Engine/Commands/RoleGuardedCommand.cs` — wraps an
      inner `ICommand`, checks `(actor.Roles & requiredRole) !=
      SecurityRole.None` before delegating; generic rejection message.
- [ ] New `src/SharpMud.Engine/Commands/MuteGuardedCommand.cs` — wraps an
      inner `ICommand`, checks the *actor's own* `IsMuted` (not a target's
      — this gates the muted player's own `say`/`emote`, not something
      they're doing to someone else) before delegating.
- [ ] `ICommandRegistry.cs` / `CommandRegistry.cs`: remove `Register
      (ICommand)` from the public surface; add `RegisterOpen(ICommand)`
      and `RegisterWithRole(ICommand, SecurityRole)` (the latter wraps in
      `RoleGuardedCommand` before storing).
- [ ] Update every existing registration call site (`BuiltinCommands
      .RegisterAll`, `ClassicCommands.RegisterAll`) from `Register` to
      `RegisterOpen` — mechanical, no behavior change.

### `PlayerBehavior` + persistence

- [ ] `PlayerBehavior.cs`: add `SecurityRole Roles { get; private set; } =
      SecurityRole.Player`, `bool IsMuted { get; private set; }`, `bool
      IsBanned { get; private set; }`, plus mutation methods
      (`GrantRole`/`RevokeRole`, `Mute`/`Unmute`, `Ban`/`Unban`) rather than
      public setters, matching `ConnectionState`'s existing
      transition-method style.
- [ ] `PlayerBehaviorConfiguration.cs`: map `Roles` with the plain-enum
      default EF conversion (matching `WearableBehaviorConfiguration`'s
      `Slot` precedent — no custom value converter needed); map `IsMuted`/
      `IsBanned` as plain persisted columns (NOT `Ignore`d — unlike
      `ConnectionState`, these must survive a restart).

### Moderation commands (`src/SharpMud.Engine/Commands/Builtin/Admin/`)

- [ ] `BootCommand` (`MinorAdmin`) — disconnects a currently-online target
      by username; "not online" message if not found live.
- [ ] `MuteCommand`/`UnmuteCommand` (`MinorAdmin`) — sets/clears
      `IsMuted` on a target (online-or-not, mirrors `LoginFlow`'s
      live-then-repository lookup), saves immediately.
- [ ] `AnnounceCommand` (`MinorAdmin`) — broadcasts to every session in
      `world.AllWithBehavior<PlayerBehavior>()` with a live session
      (`WhoCommand`'s iteration pattern).
- [ ] `BanCommand`/`UnbanCommand` (`FullAdmin`) — sets/clears `IsBanned`,
      online-or-not lookup, saves immediately.
- [ ] `RoleGrantCommand`/`RoleRevokeCommand` (`FullAdmin`) — mutates a
      target's `Roles`, online-or-not lookup, saves immediately; validate
      the role name argument against `SecurityRole`'s named values.
- [ ] Register all 8 via `RegisterWithRole` in a new
      `AdminCommands.RegisterAll(registry)` (mirrors `BuiltinCommands`/
      `ClassicCommands`'s shape), called from `Program.cs`. Wrap `say`/
      `emote`'s existing registrations in `MuteGuardedCommand` at the same
      call site.

### Login-flow + bootstrap

- [ ] `LoginFlow.LoginExistingAsync`: after password verification
      succeeds, check `IsBanned` → reject with a distinct message before
      the `ConnectionState` branch.
- [ ] `HostOptions.cs`: add `string? InitialAdminUsername`, parsed from
      `SHARPMUD_INITIAL_ADMIN`.
- [ ] `Program.cs`: after world load/build, if `InitialAdminUsername` is
      set and that username's character exists (live or via repository),
      idempotently ensure it has `FullAdmin` (grant + save if not already
      present).

### Docs

- [ ] `docs/commands.md`: describe the new admin command set as current
      state, link ADR-0005.
- [ ] `docs/accounts-auth.md`: describe ban enforcement in the login flow
      as current state, update the existing "deferred moderation tooling"
      forward-reference to point at ADR-0005/this plan.
- [ ] `SPEC.md`: update the "Moderation/admin tooling" Deferred/Open Item
      to reflect what's actually implemented vs. still deferred (Find/
      GoTo/Control/Clone/Spawn/Jail/Buff/Relinquish, audit logging).
- [ ] `docs/adr/README.md` / `docs/plans/README.md`: index rows for
      ADR-0005/PLAN-0005.
- [ ] `docs/plans/0001-wheelmud-reconciliation-roadmap.md`: check off
      Slice 3.

## Critical files

New:
- `src/SharpMud.Engine/Commands/SecurityRole.cs`
- `src/SharpMud.Engine/Commands/RoleGuardedCommand.cs`
- `src/SharpMud.Engine/Commands/MuteGuardedCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/BootCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/MuteCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/UnmuteCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/AnnounceCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/BanCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/UnbanCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/RoleGrantCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/RoleRevokeCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/AdminCommands.cs`
- Matching test files under `tests/SharpMud.Engine.Tests/Commands/...`

Modified:
- `src/SharpMud.Engine/Commands/ICommandRegistry.cs`,
  `CommandRegistry.cs`
- `src/SharpMud.Engine/Commands/BuiltinCommands.cs` (or wherever
  `RegisterAll` lives), `src/SharpMud.Ruleset.Classic/ClassicCommands.cs`
- `src/SharpMud.Engine/Behaviors/PlayerBehavior.cs`
- `src/SharpMud.Persistence/Configurations/PlayerBehaviorConfiguration.cs`
- `src/SharpMud.Host/LoginFlow.cs`, `HostOptions.cs`, `Program.cs`
- `docs/commands.md`, `docs/accounts-auth.md`, `SPEC.md`,
  `docs/adr/README.md`, `docs/plans/README.md`,
  `docs/plans/0001-wheelmud-reconciliation-roadmap.md`

## Test plan

- Unit: `RoleGuardedCommand` — actor with the required role reaches the
  inner command; actor without it doesn't, gets the rejection message.
  Cover the any-of/bitwise semantics (actor has one of several required
  flags).
- Unit: `MuteGuardedCommand` — muted actor blocked, unmuted actor passes
  through to the inner command.
- Unit: `CommandRegistry` — `RegisterOpen` resolves unconditionally;
  `RegisterWithRole` resolves to a `RoleGuardedCommand` wrapping the given
  command with the given role.
- Unit: each of the 8 admin commands — happy path (role holder, valid
  target) and the online/offline target-lookup branches.
- Unit: `LoginFlow` — banned user rejected at password verification with
  the correct message, not silently falling through.
- Unit: `PlayerBehavior.GrantRole`/`RevokeRole`/`Mute`/`Unmute`/`Ban`/
  `Unban` — state mutates as expected.
- Unit: `HostOptions.Parse` — `SHARPMUD_INITIAL_ADMIN` parses correctly,
  absent env var leaves it null.

## Verification

Real manual check over Telnet (this repo's established pattern for
session/persistence-facing changes):

1. Boot with `SHARPMUD_INITIAL_ADMIN=<username>` set, create that
   character, confirm `Roles` includes `FullAdmin` (e.g. via a debug
   `roles` self-check, or by successfully running a `FullAdmin`-gated
   command).
2. As that admin, `rolegrant <other-username> minoradmin` on a second
   character; confirm the second character can now run `boot`/`mute`/
   `announce` but not `ban`/`rolegrant`.
3. `mute <player>`; confirm that player's `say`/`emote` are blocked with a
   clear message, `unmute` restores them.
4. `ban <player>`; confirm that player can no longer log in (distinct
   message, not the generic "incorrect" one); `unban` restores login.
5. `boot <player>`; confirm their session is disconnected immediately.
6. `announce <message>`; confirm every currently-connected session
   receives it.
7. Confirm a non-admin attempting any of the above gets a clear rejection,
   not a crash or a silent no-op.
8. Restart the server; confirm `Roles`/`IsMuted`/`IsBanned` all survived
   (unlike `ConnectionState`, which is intentionally runtime-only).

## Open questions / blockers

- Audit logging of moderation actions (who banned/muted whom, when) is
  explicitly not in ADR-0005's scope — flagged here as a likely near-term
  follow-up once this lands, not designed yet.
- Exact rejection message wording for gated commands is a placeholder
  ("You don't have permission to do that.") — not a considered final
  string.
