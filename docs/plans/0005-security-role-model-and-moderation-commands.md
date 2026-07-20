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
      Exposes `public SecurityRole RequiredRole { get; }` — needed by
      `HelpCommand`'s filtering below, not just internally.
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
- [ ] `HelpCommand.cs`: filter `registry.Commands` by the actor's roles
      before listing them — caught in self-review. `RoleGuardedCommand`
      passes `Verb`/`Aliases` straight through from the wrapped command, so
      without this, `help` lists every admin command (`ban`, `boot`,
      `rolegrant`, ...) to every player. Not an exploit (the gate still
      blocks execution) but a real, unpolished info leak — skip a command
      in the listing if it's a `RoleGuardedCommand` and `(ctx.Actor`'s
      `Roles & command.RequiredRole) == SecurityRole.None`; anything not
      role-guarded still lists unconditionally.

### `PlayerBehavior` + persistence + `SessionLoop`

- [ ] `PlayerBehavior.cs`: add `SecurityRole Roles { get; private set; } =
      SecurityRole.Player`, `bool IsMuted { get; private set; }`, `bool
      IsBanned { get; private set; }`, plus mutation methods
      (`GrantRole`/`RevokeRole`, `Mute`/`Unmute`, `Ban`/`Unban`) rather than
      public setters, matching `ConnectionState`'s existing
      transition-method style.
      - [ ] Also add `bool WasBooted { get; private set; }` (transient,
            like `Session`/`ConnectionState` — `Ignore`d in
            `PlayerBehaviorConfiguration`) and a `MarkBooted()` method.
            **Real gap caught in PR review**: `BootCommand` runs inside
            the *admin's* `SessionLoop`, calling `DisconnectAsync` on the
            *target's* session — but `SessionLoop`'s `explicitQuit` flag
            is a local variable scoped to each connection's own
            `RunAsync` call. The target's own loop never sees an
            admin-triggered disconnect as a "quit," so today it would
            take the `Linkdead` branch (per ADR-0004) — the booted player
            could just reconnect within the grace window and resume
            exactly where they were, making `boot` a no-op as a
            moderation tool. `WasBooted` is the signal that crosses that
            boundary: `BootCommand` sets it on the target's
            `PlayerBehavior` *before* calling `DisconnectAsync`; the
            target's own `SessionLoop.RunAsync` (a completely separate
            call stack) checks it in its `finally` block.
      - [ ] `SessionLoop.cs`: in the `finally` block, treat `WasBooted`
            exactly like `explicitQuit` — both mean "this disconnect was
            intentional, skip `Linkdead` and remove immediately" (same
            save-then-remove ordering `explicitQuit` already uses, not
            `EnterLinkdead`'s mutate-before-save ordering). Concretely:
            replace the bare `explicitQuit` checks in both branches with
            `explicitQuit || (playerBehavior?.WasBooted ?? false)`.
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
            implies `role`; if so, **return a failure, don't throw** —
            per `coding-standards.md`'s Error Handling section, this is a
            normal, directly-user-triggerable business-rule outcome (an
            admin typed a `rolerevoke` that doesn't make sense given the
            target's current roles), not a bug or an invariant violation,
            so it must be a return value the caller checks (caught in PR
            review — the original wording said "throw/reject," which
            contradicts the standard). Signature:
            `string? RevokeRole(SecurityRole role)` — `null` on success,
            a message naming the blocking higher tier on failure ("still
            has FullAdmin, which includes MinorAdmin — revoke FullAdmin
            instead"). Corrected during self-review: earlier wording cited
            this as mirroring "`MoveRequest.CancelReason`," but no
            `MoveRequest` type exists — the actual precedent
            (`UseExitEvent.CancelReason`, `src/SharpMud.Engine/Core
            /Events.cs`) is a *property* set via `.Cancel(reason)` on a
            published cancellable event, a different shape (pub/sub
            event-object mutation, not a direct method return). A plain
            nullable-string return is simpler and doesn't need that
            machinery here — it just needs to be a return value, per
            `coding-standards.md`, not a citation to a nonexistent type.
            `RoleRevokeCommand` relays a non-null return straight to the
            admin; never wraps this call in a try/catch.
- [ ] `PlayerBehaviorConfiguration.cs`: map `Roles` with the plain-enum
      default EF conversion (matching `WearableBehaviorConfiguration`'s
      `Slot` precedent — no custom value converter needed); map `IsMuted`/
      `IsBanned` as plain persisted columns (NOT `Ignore`d — unlike
      `ConnectionState`, these must survive a restart). `Ignore(x =>
      x.WasBooted)` alongside `Session`/`ConnectionState` — transient,
      same category, never meaningful across a restart.

### Moderation commands (`src/SharpMud.Engine/Commands/Builtin/Admin/`)

`Mute`/`Unmute`/`Ban`/`Unban`/`RoleGrant`/`RoleRevoke` need `IThingRepository`
for offline target lookup + immediate saves — `CommandContext` only carries
`World`/`Session`, not the repository, and today the repository is only
available inside `SessionLoop`. Rather than extending `CommandContext`
(bigger blast radius, touches every command's context), these six commands
take `IThingRepository` via their own constructor — the same shape
`ClassicCommands.RegisterAll` already uses for `combatManager`/`random`.
`Boot`/`Announce` don't need it (online-only / broadcast-only).

**"Online" means `ConnectionState == Playing` *and* `Session is {
IsConnected: true }` — both, not either alone.** `WhoCommand`'s iteration
(`world.AllWithBehavior<PlayerBehavior>()`, no further filter) was
originally cited here as the pattern to copy, but `WhoCommand` itself has
no liveness filter at all, and since ADR-0004 that call also returns
**`Linkdead`** players (disconnected but not yet swept) — `WhoCommand`
mislabeling those as "online" is a separate, pre-existing gap, out of
scope for this slice to fix. `BootCommand`/`AnnounceCommand` must *not*
copy that omission.

An earlier version of this note said `ConnectionState == Playing` alone
was sufficient — **wrong, caught in PR review**: `ConnectionState` is
`Ignore`d by `PlayerBehaviorConfiguration` (runtime-only, not persisted),
so it defaults back to `Playing` on any freshly-constructed
`PlayerBehavior` — including a player just reloaded from the repository
with no live session at all. This isn't hypothetical:
`LoginFlow.FindAndAttachExistingAsync` already registers a
repository-loaded player into `World` *before password verification even
runs* — during that window (and after a server restart, before that
player reconnects) the Thing sits in `World` with `ConnectionState
.Playing` and `Session == null`. Checking `ConnectionState` alone would
make `Boot`/`Announce` treat that player as online and attempt a
null-session disconnect/write. The fix: use the exact same combined check
`LoginFlow.LoginExistingAsync` already established for this
(`playerBehavior.ConnectionState == ConnectionState.Playing &&
playerBehavior.Session is { IsConnected: true }`) — don't invent a
narrower one.

- [ ] `BootCommand` (`MinorAdmin`, no repository dependency) — disconnects
      a currently-online (`ConnectionState == Playing && Session is {
      IsConnected: true }`) target by username; "not online" message
      otherwise (not found at all, found only `Linkdead`, or found but
      with a null/disconnected `Session`). **Calls
      `target.FindBehavior<PlayerBehavior>()!.MarkBooted()` before**
      `target's session.DisconnectAsync(...)` — without this the boot is
      cosmetic (caught in PR review; see the `PlayerBehavior`/
      `SessionLoop` task above for why). Writes a message to the target's
      session first (mirrors `QuitCommand`'s "Goodbye!" before
      disconnecting), e.g. "You have been disconnected by an
      administrator."
- [ ] `MuteCommand`/`UnmuteCommand` (`MinorAdmin`, `IThingRepository`) —
      sets/clears `IsMuted` on a target (online-or-not, mirrors
      `LoginFlow`'s live-then-repository lookup), saves immediately.
- [ ] `AnnounceCommand` (`MinorAdmin`, no repository dependency) —
      broadcasts to every `world.AllWithBehavior<PlayerBehavior>()` entry
      whose `ConnectionState == Playing && Session is { IsConnected: true
      }` — explicitly **not** every entry `WhoCommand`-style, and
      explicitly **not** `ConnectionState` alone (see above).
- [ ] `BanCommand` (`FullAdmin`, `IThingRepository`) — sets `IsBanned`,
      online-or-not lookup, saves immediately. **If the target is
      currently online (`ConnectionState == Playing && Session is {
      IsConnected: true }`), also disconnects them the same way
      `BootCommand` does** — `MarkBooted()` then session write + `Disconn
      ectAsync(...)` (caught in PR review: `SessionLoop` never re-checks
      `IsBanned` mid-session, so without this an already-connected banned
      player keeps issuing commands until an admin separately remembers
      to `boot` them). **Rejects self-targeting**
      (caught in self-review: `Ban` has no in-game recovery —
      `SHARPMUD_INITIAL_ADMIN` only re-grants roles, it doesn't clear
      `IsBanned` — so an admin banning themselves is locked out short of a
      manual DB edit; `boot`/`mute` self-targeting is left alone, both are
      harmless and trivially reversible by the same admin). `UnbanCommand`
      needs no such guard (undoing your own ban isn't reachable — you
      can't be logged in while banned).
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
      **`RoleRevokeCommand` rejects revoking your own `FullAdmin`** (caught
      in self-review — same class of lockout risk as self-`Ban`: a sole
      `FullAdmin` revoking their own tier has no in-game path back without
      another `FullAdmin` already present to re-grant it). Revoking any
      other role from yourself, or revoking `FullAdmin` from someone
      *else*, is unaffected. `RoleRevokeCommand` checks `RevokeRole`'s
      `string?` return (not a
      caught exception — see the `PlayerBehavior` task above) and relays
      a non-null failure straight to the admin as the rejection message.
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
- [ ] `Program.cs`: add `["SHARPMUD_INITIAL_ADMIN"] =
      Environment.GetEnvironmentVariable("SHARPMUD_INITIAL_ADMIN")` to the
      `env` dictionary built before `HostOptions.Parse(args, env)` — caught
      in PR review. `Program.cs` builds a **fixed** dictionary of only the
      three existing env vars (`SHARPMUD_MODE`/`SHARPMUD_TELNET_PORT`/
      `SHARPMUD_DB_PATH`), not a full environment pass-through, so adding
      the parse logic to `HostOptions.cs` alone isn't enough —
      `SHARPMUD_INITIAL_ADMIN` needs its own entry here or the real host
      never sees it, even with the env var actually set in production.
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
            **This needs `InitialAdminUsername` threaded all the way down
            to `MaybeCreateAsync` — not hidden global state** (caught in
            PR review): the actual Telnet call chain is
            `HostRunner.RunTelnetAsync` → `HandleConnectionAsync` →
            `LoginFlow.RunAsync(session, context.World, context.Repository,
            context.StartingRoom, ct)` → `MaybeCreateAsync`, and neither
            `TelnetHostContext` nor `LoginFlow.RunAsync`'s signature
            carries `HostOptions`/`InitialAdminUsername` today. Concretely:
            - [ ] `TelnetHostContext` (`src/SharpMud.Host
                  /TelnetHostContext.cs`) gains a `string?
                  InitialAdminUsername` field.
            - [ ] `LoginFlow.RunAsync`/`MaybeCreateAsync` gain an
                  `InitialAdminUsername` parameter (already at/near the
                  4-param limit — a small parameter object may be
                  warranted here too, matching `TelnetHostContext`'s own
                  precedent per `coding-standards.md`'s 4-param rule,
                  rather than pushing a 5th positional parameter through).
            - [ ] `HostRunner.HandleConnectionAsync` passes
                  `context.InitialAdminUsername` through to
                  `LoginFlow.RunAsync`.
            - [ ] `Program.cs`'s `TelnetHostContext` construction passes
                  `hostOptions.InitialAdminUsername`.
      Both paths call the same `PlayerBehavior.GrantRole(SecurityRole
      .FullAdmin)`, so both get the accumulation behavior (also granting
      `MinorAdmin`/`Player`) for free.

### Docs

- [ ] `docs/commands.md`: describe the new admin command set as current
      state, link ADR-0005.
- [ ] `docs/accounts-auth.md`: describe ban enforcement in the login flow
      as current state, update the existing "deferred moderation tooling"
      forward-reference to point at ADR-0005/this plan.
- [ ] `docs/deployment.md`: add `SHARPMUD_INITIAL_ADMIN` to the Runtime
      Configuration table (caught in PR review — this table is the
      documented list of every `HostOptions` env var, and
      `SHARPMUD_INITIAL_ADMIN` is the *only* bootstrap path to a
      `FullAdmin` in a fresh deployment; omitting it here means deploying
      a container with no discoverable way to administer it).
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
  `CommandRegistry.cs`, `Builtin/HelpCommand.cs`
- `src/SharpMud.Engine/Commands/BuiltinCommands.cs` (or wherever
  `RegisterAll` lives), `src/SharpMud.Ruleset.Classic/ClassicCommands.cs`
- `src/SharpMud.Engine/Behaviors/PlayerBehavior.cs`
- `src/SharpMud.Persistence/Configurations/PlayerBehaviorConfiguration.cs`
- `src/SharpMud.Host/LoginFlow.cs`, `HostOptions.cs`, `Program.cs`,
  `HostRunner.cs`, `TelnetHostContext.cs`, `SessionLoop.cs`
- `docs/commands.md`, `docs/accounts-auth.md`, `docs/deployment.md`,
  `SPEC.md`, `docs/adr/README.md`, `docs/plans/README.md`,
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
- Unit: `BootCommand`/`AnnounceCommand` — a `Linkdead` player is treated as
  not-online (`Boot` reports "not online," `Announce` doesn't attempt a
  write to their stale session); only `ConnectionState == Playing &&
  Session is { IsConnected: true }` targets/recipients count. Specifically
  cover a `Playing`-but-`Session == null` player (the repository-reload
  case — `ConnectionState` defaults to `Playing` because it isn't
  persisted) being correctly treated as *not* online — the regression test
  for both the online/live ambiguity caught in self-review and the
  reload-defaults-to-Playing gap caught in PR review.
- Unit: `SessionLoop` — a session ending with `WasBooted` set (but not
  `explicitQuit`) takes the same immediate-removal path `explicitQuit`
  takes, not `EnterLinkdead`. The regression test for the
  boot-is-cosmetic gap caught in PR review: without this, a booted player
  would just resume via the `Linkdead` reconnect path, making `boot` a
  no-op as a moderation tool.
- Unit: `HelpCommand` — a role-gated command is omitted from the listing
  for an actor without the required role, and included for one with it;
  non-gated commands always list regardless of role. The regression test
  for the admin-command-visibility gap caught in self-review.
- Unit: `BanCommand` — self-targeting is rejected with a clear message,
  `IsBanned` unchanged; targeting another player still works normally.
- Unit: `RoleRevokeCommand` — revoking your own `FullAdmin` is rejected;
  revoking a different role from yourself, or `FullAdmin` from someone
  else, still works normally. Both are the regression test for the
  self-lockout risk caught in self-review.
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
  returns a non-null failure message and leaves `Roles` unchanged — the
  regression test for the revoke-side hierarchy gap caught in PR review
  (and, separately, that the failure is a return value, not a thrown
  exception, per `coding-standards.md`'s Error Handling section).
  `RevokeRole(FullAdmin)` on that same actor returns `null` (success) and
  leaves `MinorAdmin`/`Player` intact (demotion, not a full reset —
  revoking the top tier doesn't cascade-clear what it implied).
  `RevokeRole(MinorAdmin)` on an actor who does *not* also hold
  `FullAdmin` returns `null` (success) normally.
- Unit: `HostOptions.Parse` — `SHARPMUD_INITIAL_ADMIN` parses correctly,
  absent env var leaves it null.
- Unit: bootstrap grants `FullAdmin` via both paths independently — the
  `Program.cs` existing-character path, and `LoginFlow.MaybeCreateAsync`'s
  newly-created-character path — since a boot-time-only check was the
  fresh-server gap caught in PR review.
- Unit: `LoginFlow.MaybeCreateAsync` (or its `RunAsync` entry point)
  actually receives and uses `InitialAdminUsername` — a character created
  with a username matching it gets `FullAdmin`; a character created with a
  non-matching (or null/absent) `InitialAdminUsername` does not. The
  regression test for the parameter-threading gap caught in PR review
  (`TelnetHostContext`/`LoginFlow.RunAsync` never carried this value
  before).

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
6. `boot <player>`; confirm their session is disconnected immediately, then
   **immediately reconnect as that player** (within the `Linkdead` grace
   window) and confirm login goes through the normal username/password
   prompt from scratch, not a "Welcome back" resume — the regression check
   for `WasBooted` actually crossing the `SessionLoop` boundary (PR review
   gap: without it, `boot` would be a no-op since the target could just
   reconnect and resume where they were).
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
