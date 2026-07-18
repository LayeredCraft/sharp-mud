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
      `[Flags] enum SecurityRole : uint` with **explicit power-of-two
      values on every member** (`None = 0`, `Mobile = 1 << 0`, `Item = 1
      << 1`, `Room = 1 << 2`, `TutorialPlayer = 1 << 3`, `Player = 1 <<
      4`, `Helper = 1 << 5`, `Married = 1 << 6`, `MinorBuilder = 1 << 7`,
      `FullBuilder = 1 << 8`, `MinorAdmin = 1 << 9`, `FullAdmin = 1 <<
      10`, `All = Mobile | Item | Room | TutorialPlayer | Player | Helper
      | Married | MinorBuilder | FullBuilder | MinorAdmin | FullAdmin`).
      **Do not rely on C#'s default sequential auto-numbering** — caught
      in PR review: unnumbered members would auto-assign `0, 1, 2, 3...`,
      which are not distinct bits (e.g. `Room` would silently equal
      `Mobile | Item` combined), breaking `RoleGuardedCommand`'s bitwise
      -AND check into granting unrelated permissions on overlapping bits.
      `All` is a union expression, not a separate hardcoded value, so it
      can't drift out of sync if a flag is added later. XML doc comments
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
      - [ ] `GrantRole(SecurityRole role)`: ORs in `role` *and* every tier
            it implies (`FullAdmin` → also `MinorAdmin` + `Player`;
            `FullBuilder` → also `MinorBuilder`) per ADR-0005's
            accumulation rule — a plain `Roles |= role` is not enough on
            its own.
      - [ ] A static `SecurityRole.Implies(SecurityRole role)` (or
            equivalent lookup) expressing the same hierarchy
            (`FullAdmin`→`MinorAdmin`→`Player`, `FullBuilder`→
            `MinorBuilder`) — used by both `GrantRole` (to accumulate
            downward) and `RevokeRole` (to check upward, see next) so the
            hierarchy is defined once, not duplicated between the two
            directions.
      - [ ] `RevokeRole(SecurityRole role)`: **enforces the same
            invariant symmetrically** (caught in PR review — clearing
            only the exact bit passed in can leave a higher tier set with
            a lower tier it implies cleared, e.g. `FullAdmin` present but
            `MinorAdmin` cleared after revoking just `MinorAdmin`). Before
            clearing, check whether any *other* currently-held role
            implies `role`; if so, throw/reject (surfaced by
            `RoleRevokeCommand` as a message naming the blocking higher
            tier — "still has FullAdmin, which includes MinorAdmin —
            revoke FullAdmin instead") rather than silently applying an
            inconsistent state.
- [ ] `PlayerBehaviorConfiguration.cs`: map `Roles` with the plain-enum
      default EF conversion (matching `WearableBehaviorConfiguration`'s
      `Slot` precedent — no custom value converter needed); map `IsMuted`/
      `IsBanned` as plain persisted columns (NOT `Ignore`d — unlike
      `ConnectionState`, these must survive a restart).

### Moderation commands (`src/SharpMud.Engine/Commands/Builtin/Admin/`)

`Mute`/`Unmute`/`Ban`/`Unban`/`RoleGrant`/`RoleRevoke` need `IThingRepository`
for offline target lookup + immediate saves — `CommandContext` only carries
`World`/`Session`, not the repository, and today the repository is only
available inside `SessionLoop`. Rather than extending `CommandContext`
(bigger blast radius, touches every command's context), these six commands
take `IThingRepository` via their own constructor — the same shape
`ClassicCommands.RegisterAll` already uses for `combatManager`/`random`.
`Boot`/`Announce` don't need it (online-only / broadcast-only).

- [ ] `BootCommand` (`MinorAdmin`, no repository dependency) — disconnects
      a currently-online target by username; "not online" message if not
      found live.
- [ ] `MuteCommand`/`UnmuteCommand` (`MinorAdmin`, `IThingRepository`) —
      sets/clears `IsMuted` on a target (online-or-not, mirrors
      `LoginFlow`'s live-then-repository lookup), saves immediately.
- [ ] `AnnounceCommand` (`MinorAdmin`, no repository dependency) —
      broadcasts to every session in `world.AllWithBehavior<PlayerBehavior>()`
      with a live session (`WhoCommand`'s iteration pattern).
- [ ] `BanCommand`/`UnbanCommand` (`FullAdmin`, `IThingRepository`) —
      sets/clears `IsBanned`, online-or-not lookup, saves immediately.
- [ ] `RoleGrantCommand`/`RoleRevokeCommand` (`FullAdmin`,
      `IThingRepository`) — mutates a target's `Roles` via
      `GrantRole`/`RevokeRole` (accumulation/hierarchy-invariant
      enforcement happens inside `PlayerBehavior` itself, not here),
      online-or-not lookup, saves immediately. Validate the role name
      argument against an **explicit allowlist of individually-grantable
      roles — not just "is this a real `SecurityRole` name."** Caught in
      PR review: a plain `Enum.TryParse<SecurityRole>` would also accept
      `All` (every current *and future* flag — not a real assignable
      tier, a severe over-grant) and `None` (a meaningless no-op
      sentinel), since both are literally named enum members. Reject
      either with a clear message rather than silently persisting them.
      `RoleRevokeCommand` catches `RevokeRole`'s rejection (target still
      holds a higher tier that implies the requested one) and surfaces it
      as a clear message
      naming the blocking tier, not a crash.
- [ ] Register all 8 via `RegisterWithRole` in a new
      `AdminCommands.RegisterAll(registry, repository)` (mirrors
      `BuiltinCommands`/`ClassicCommands`'s shape — `ClassicCommands
      .RegisterAll` already takes extra constructed dependencies the same
      way), called from `Program.cs` alongside the existing `RegisterAll`
      calls, passing the already-constructed `repository`. Wrap `say`/
      `emote`'s existing registrations in `MuteGuardedCommand` at the same
      call site.

### Login-flow + bootstrap

- [ ] `LoginFlow.LoginExistingAsync`: after password verification
      succeeds, check `IsBanned` → reject with a distinct message before
      the `ConnectionState` branch.
- [ ] `HostOptions.cs`: add `string? InitialAdminUsername`, parsed from
      `SHARPMUD_INITIAL_ADMIN`.
- [ ] Bootstrap the grant in **two** places, not just one — a boot-time-only
      check is a no-op on a genuinely fresh server, since the target
      character doesn't exist yet at boot and only gets created later
      through the normal login flow (caught in PR review):
      - [ ] `Program.cs`: after world load/build, if `InitialAdminUsername`
            is set and that username's character already exists (live or
            via repository — the "restart of an existing world" case),
            idempotently `GrantRole(FullAdmin)` + save if not already
            present.
      - [ ] `LoginFlow.MaybeCreateAsync`: after a new character is created
            and saved, if its username matches `InitialAdminUsername`,
            `GrantRole(FullAdmin)` + save again (the fresh-server case).
      Both paths call the same `PlayerBehavior.GrantRole(SecurityRole
      .FullAdmin)`, so both get the accumulation behavior (also granting
      `MinorAdmin`/`Player`) for free.

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

- Unit: `SecurityRole` — every named member (excluding `None`/`All`) is a
  distinct power of two, and no two members share a bit
  (`Enum.GetValues<SecurityRole>()` pairwise-AND'd should all be `None`
  except `All`/self-comparisons). `All` equals the OR of every individual
  flag. The regression test for the undefined-values gap caught in PR
  review — this is the one test that would have caught it immediately if
  the enum were ever implemented with auto-numbered members.
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
- Unit: `RoleGrantCommand`/`RoleRevokeCommand` — `all` and `none` (any
  casing) are rejected with a clear message and never reach
  `GrantRole`/`RevokeRole`; every other individually-grantable role name
  is accepted. The regression test for the `All`/sentinel-value gap
  caught in PR review.
- Unit: `LoginFlow` — banned user rejected at password verification with
  the correct message, not silently falling through.
- Unit: `PlayerBehavior.GrantRole`/`RevokeRole`/`Mute`/`Unmute`/`Ban`/
  `Unban` — state mutates as expected. Specifically cover accumulation:
  `GrantRole(FullAdmin)` results in `Roles` containing `FullAdmin`,
  `MinorAdmin`, *and* `Player`; `GrantRole(FullBuilder)` results in
  `FullBuilder` + `MinorBuilder`.
- Unit: a `FullAdmin`-only actor (post-accumulation) successfully passes a
  `MinorAdmin`-gated `RoleGuardedCommand` — the regression test for the
  bootstrap-admin-can't-moderate gap caught in PR review.
- Unit: `RevokeRole(MinorAdmin)` on an actor who also holds `FullAdmin`
  is rejected (`Roles` unchanged, exception/rejection surfaced) — the
  regression test for the revoke-side hierarchy gap caught in PR review.
  `RevokeRole(FullAdmin)` on that same actor succeeds and leaves
  `MinorAdmin`/`Player` intact (demotion, not a full reset — revoking the
  top tier doesn't cascade-clear what it implied). `RevokeRole(MinorAdmin)`
  on an actor who does *not* also hold `FullAdmin` succeeds normally.
- Unit: `HostOptions.Parse` — `SHARPMUD_INITIAL_ADMIN` parses correctly,
  absent env var leaves it null.
- Unit: bootstrap grants `FullAdmin` via both paths independently — the
  `Program.cs` existing-character path, and `LoginFlow.MaybeCreateAsync`'s
  newly-created-character path — since a boot-time-only check was the
  fresh-server gap caught in PR review.

## Verification

Real manual check over Telnet (this repo's established pattern for
session/persistence-facing changes):

1. **Fresh-server case** (the gap caught in PR review): boot a brand-new
   world with `SHARPMUD_INITIAL_ADMIN=<username>` set, and only *then*
   create that character over Telnet — confirm the grant happens at
   creation time, not just at boot. Confirm the resulting admin can run
   *both* a `FullAdmin`-gated command (e.g. `ban`) *and* a
   `MinorAdmin`-gated one (e.g. `boot`) without a separate grant —
   validates the accumulation fix.
2. **Restart case**: restart the server (same world, same
   `SHARPMUD_INITIAL_ADMIN`); confirm the existing admin's roles are
   unaffected (idempotent, no duplicate grants/errors).
3. As the admin, `rolegrant <other-username> minoradmin` on a second
   character; confirm the second character can now run `boot`/`mute`/
   `announce` but not `ban`/`rolegrant`.
4. `mute <player>`; confirm that player's `say`/`emote` are blocked with a
   clear message, `unmute` restores them.
5. `ban <player>`; confirm that player can no longer log in (distinct
   message, not the generic "incorrect" one); `unban` restores login.
6. `boot <player>`; confirm their session is disconnected immediately.
7. `announce <message>`; confirm every currently-connected session
   receives it.
8. Confirm a non-admin attempting any of the above gets a clear rejection,
   not a crash or a silent no-op.
9. Restart the server again; confirm `Roles`/`IsMuted`/`IsBanned` all
   survived (unlike `ConnectionState`, which is intentionally
   runtime-only).

## Open questions / blockers

- Audit logging of moderation actions (who banned/muted whom, when) is
  explicitly not in ADR-0005's scope — flagged here as a likely near-term
  follow-up once this lands, not designed yet.
- Exact rejection message wording for gated commands is a placeholder
  ("You don't have permission to do that.") — not a considered final
  string.
