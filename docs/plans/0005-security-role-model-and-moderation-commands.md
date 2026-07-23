# [PLAN-0005] Security Role Model + Moderation Commands

**Implements:** [ADR-0005](../adr/0005-security-role-model-and-moderation-commands.md)

**Status:** Done

**Last updated:** 2026-07-23

**Validated against current architecture (2026-07-23), before starting
implementation:** this plan was originally written against the
pre-[ADR-0006](../adr/0006-nuget-package-distribution.md) project layout
(`src/SharpMud.Host`, `src/SharpMud.Ruleset.Classic`, `TelnetHostContext`,
`HostRunner`). None of those exist anymore — `SharpMud.Host` split into
`SharpMud.Hosting` + `SharpMud.Adapters.*`, the composition root is
`samples/SharpMud.Samples.Classic/Program.cs`, `HostRunner` became
`TelnetTransportBackgroundService`, and there's no per-connection context
object (dependencies resolve via `IServiceProvider` per connection).
[ADR-0008](../adr/0008-ruleset-scaffolding-tier.md) further extracted
combat scaffolding into `SharpMud.Ruleset.Rpg`, adding a second
`registry.Register(...)` call site (`AttackCommand`/`FleeCommand`) this
plan's registry-migration task needs to cover too. Every task/file
reference below has been re-verified against the actual current code, not
just find-and-replaced — see ADR-0005's own correction note for the
mechanism-level change (bootstrap collapses from two checkpoints to one,
now that `LoginFlow` is DI-constructed rather than manually
parameter-threaded through a context object that no longer exists). The
underlying decision (`SecurityRole`, the Decorator pattern,
`RegisterOpen`/`RegisterWithRole`, role accumulation, the 8 commands, the
bootstrap env var) is unchanged.

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

- [x] New `src/SharpMud.Engine/Commands/SecurityRole.cs` — plain
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
- [x] New `src/SharpMud.Engine/Commands/RoleGuardedCommand.cs` — wraps an
      inner `ICommand`, checks `(actor.Roles & requiredRole) !=
      SecurityRole.None` before delegating; generic rejection message.
      Exposes `public SecurityRole RequiredRole { get; }` — needed by
      `HelpCommand`'s filtering below, not just internally.
- [x] New `src/SharpMud.Engine/Commands/MuteGuardedCommand.cs` — wraps an
      inner `ICommand`, checks the *actor's own* `IsMuted` (not a target's
      — this gates the muted player's own `say`/`emote`, not something
      they're doing to someone else) before delegating.
- [x] `ICommandRegistry.cs` / `CommandRegistry.cs`
      (`src/SharpMud.Engine/Commands/`): remove `Register(ICommand)` from
      the public surface; add `RegisterOpen(ICommand)` and
      `RegisterWithRole(ICommand, SecurityRole)` (the latter wraps in
      `RoleGuardedCommand` before storing).
- [x] Update every existing registration call site from `Register` to
      `RegisterOpen` — mechanical, no behavior change. Two call sites
      today, not one (verified by grep, not assumed):
      - [x] `src/SharpMud.Engine/Commands/Builtin/BuiltinCommands.cs` — all
            17 registrations. While here: wrap the `SayCommand`/
            `EmoteCommand` registrations specifically in
            `MuteGuardedCommand` before `RegisterOpen` (mute enforcement is
            an `Engine`-level concern per ADR-0005, and `BuiltinCommands
            .RegisterAll` runs for every consumer via `Hosting`'s
            `AddSharpMudRuleset(...)`, so wrapping here — not per-consumer
            — is what actually makes mute universal).
      - [x] `src/SharpMud.Ruleset.Rpg/ServiceCollectionExtensions.cs` — the
            `AttackCommand`/`FleeCommand` registrations inside
            `AddSharpMudRpgRuleset(...)`. This call site didn't exist when
            ADR-0005 was accepted ([ADR-0008](../adr/0008-ruleset-scaffolding-tier.md)
            added it afterward) — `attack`/`flee` aren't role-gated, just
            migrated to the new intentional entry point like everything
            else.
      - [x] `samples/SharpMud.Samples.Classic/ClassicCommands.cs` no longer
            exists (removed when [ADR-0008](../adr/0008-ruleset-scaffolding-tier.md)
            landed — it had nothing left to register) — do not recreate it
            for this plan; the new admin commands register through their
            own `AdminCommands.RegisterAll`, see below.
- [x] `HelpCommand.cs`: filter `registry.Commands` by the actor's roles
      before listing them — caught in self-review. `RoleGuardedCommand`
      passes `Verb`/`Aliases` straight through from the wrapped command, so
      without this, `help` lists every admin command (`ban`, `boot`,
      `rolegrant`, ...) to every player. Not an exploit (the gate still
      blocks execution) but a real, unpolished info leak — skip a command
      in the listing if it's a `RoleGuardedCommand` and `(ctx.Actor`'s
      `Roles & command.RequiredRole) == SecurityRole.None`; anything not
      role-guarded still lists unconditionally.

### `PlayerBehavior` + persistence + `SessionLoop`

- [x] `src/SharpMud.Engine/Behaviors/PlayerBehavior.cs`: add `SecurityRole
      Roles { get; private set; } = SecurityRole.Player`, `bool IsMuted {
      get; private set; }`, `bool IsBanned { get; private set; }`, plus
      mutation methods (`GrantRole`/`RevokeRole`, `Mute`/`Unmute`,
      `Ban`/`Unban`) rather than public setters, matching
      `ConnectionState`'s existing transition-method style.
      - [x] Also add `bool WasBooted { get; private set; }` (transient,
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
      - [x] `src/SharpMud.Hosting/SessionLoop.cs`: in the `finally` block,
            treat `WasBooted` exactly like `explicitQuit` — both mean
            "this disconnect was intentional, skip `Linkdead` and remove
            immediately" (same save-then-remove ordering `explicitQuit`
            already uses, not `EnterLinkdead`'s mutate-before-save
            ordering). Concretely: replace the bare `explicitQuit` checks
            in both branches with `explicitQuit || (playerBehavior
            ?.WasBooted ?? false)`.
      - [x] `GrantRole(SecurityRole role)`: ORs in `role` *and* every tier
            it implies (`FullAdmin` → also `MinorAdmin` + `Player`;
            `FullBuilder` → also `MinorBuilder`) per ADR-0005's
            accumulation rule — a plain `Roles |= role` is not enough on
            its own.
      - [x] A static `SecurityRole.Implies(SecurityRole role)` (or
            equivalent lookup) expressing the same hierarchy
            (`FullAdmin`→`MinorAdmin`→`Player`, `FullBuilder`→
            `MinorBuilder`) — used by both `GrantRole` (to accumulate
            downward) and `RevokeRole` (to check upward, see next) so the
            hierarchy is defined once, not duplicated between the two
            directions.
      - [x] `RevokeRole(SecurityRole role)`: **enforces the same
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
            so it must be a return value the caller checks. Signature:
            `string? RevokeRole(SecurityRole role)` — `null` on success,
            a message naming the blocking higher tier on failure ("still
            has FullAdmin, which includes MinorAdmin — revoke FullAdmin
            instead"). `RoleRevokeCommand` relays a non-null return
            straight to the admin; never wraps this call in a try/catch.
- [x] `src/SharpMud.Persistence/Configurations/PlayerBehaviorConfiguration.cs`:
      map `Roles` with the plain-enum default EF conversion (matching
      `WearableBehaviorConfiguration`'s `Slot` precedent — no custom value
      converter needed); map `IsMuted`/`IsBanned` as plain persisted
      columns (NOT `Ignore`d — unlike `ConnectionState`, these must
      survive a restart). `Ignore(x => x.WasBooted)` alongside `Session`/
      `ConnectionState` — transient, same category, never meaningful
      across a restart.

### Moderation commands (`src/SharpMud.Engine/Commands/Builtin/Admin/`)

`Mute`/`Unmute`/`Ban`/`Unban`/`RoleGrant`/`RoleRevoke` need
`IThingRepository` for offline target lookup + immediate saves —
`CommandContext` only carries `World`/`Session`, not the repository. Rather
than extending `CommandContext` (bigger blast radius, touches every
command's context), these six commands take `IThingRepository` via their
own constructor — same shape `SharpMud.Ruleset.Rpg`'s `AttackCommand`/
`FleeCommand` already use for their own dependencies. **`IThingRepository`
is defined in `SharpMud.Engine` itself** (`src/SharpMud.Engine/Core
/IThingRepository.cs` — `SharpMud.Persistence` implements it, not the
reverse), so these commands living in `SharpMud.Engine` alongside it is not
a layering violation — verified directly against the current dependency
graph, not assumed. `Boot`/`Announce` don't need it (online-only /
broadcast-only).

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
narrower one. Verified this check still reads exactly this way in the
current `src/SharpMud.Hosting/LoginFlow.cs`.

- [x] `BootCommand` (`MinorAdmin`, no repository dependency) — disconnects
      a currently-online (`ConnectionState == Playing && Session is {
      IsConnected: true }`) target by username; "not online" message
      otherwise (not found at all, found only `Linkdead`, or found but
      with a null/disconnected `Session`). **Calls
      `target.FindBehavior<PlayerBehavior>()!.MarkBooted()` before**
      `target's session.DisconnectAsync(...)` — without this the boot is
      cosmetic (see the `PlayerBehavior`/`SessionLoop` task above). Writes
      a message to the target's session first (mirrors `QuitCommand`'s
      "Goodbye!" before disconnecting), e.g. "You have been disconnected
      by an administrator."
- [x] `MuteCommand`/`UnmuteCommand` (`MinorAdmin`, `IThingRepository`) —
      sets/clears `IsMuted` on a target (online-or-not, mirrors
      `LoginFlow`'s live-then-repository lookup), saves immediately.
- [x] `AnnounceCommand` (`MinorAdmin`, no repository dependency) —
      broadcasts to every `world.AllWithBehavior<PlayerBehavior>()` entry
      whose `ConnectionState == Playing && Session is { IsConnected: true
      }` — explicitly **not** every entry `WhoCommand`-style, and
      explicitly **not** `ConnectionState` alone (see above).
- [x] `BanCommand` (`FullAdmin`, `IThingRepository`) — sets `IsBanned`,
      online-or-not lookup, saves immediately. **If the target is
      currently online (`ConnectionState == Playing && Session is {
      IsConnected: true }`), also disconnects them the same way
      `BootCommand` does** — `MarkBooted()` then session write +
      `DisconnectAsync(...)` (`SessionLoop` never re-checks `IsBanned`
      mid-session, so without this an already-connected banned player
      keeps issuing commands until an admin separately remembers to
      `boot` them). **Rejects self-targeting** (`Ban` has no in-game
      recovery — `SHARPMUD_INITIAL_ADMIN` only re-grants roles, it doesn't
      clear `IsBanned` — so an admin banning themselves is locked out
      short of a manual DB edit; `boot`/`mute` self-targeting is left
      alone, both are harmless and trivially reversible by the same
      admin). `UnbanCommand` needs no such guard (undoing your own ban
      isn't reachable — you can't be logged in while banned).
- [x] `RoleGrantCommand`/`RoleRevokeCommand` (`FullAdmin`,
      `IThingRepository`) — mutates a target's `Roles` via
      `GrantRole`/`RevokeRole` (accumulation/hierarchy-invariant
      enforcement happens inside `PlayerBehavior` itself, not here),
      online-or-not lookup, saves immediately. Validate the role name
      argument against an **explicit allowlist of individually-grantable
      roles — not just "is this a real `SecurityRole` name."** A plain
      `Enum.TryParse<SecurityRole>` would also accept `All` (every current
      *and future* flag — not a real assignable tier, a severe
      over-grant) and `None` (a meaningless no-op sentinel), since both
      are literally named enum members. Reject either with a clear
      message rather than silently persisting them. **`RoleRevokeCommand`
      rejects revoking your own `FullAdmin`** (same class of lockout risk
      as self-`Ban`: a sole `FullAdmin` revoking their own tier has no
      in-game path back without another `FullAdmin` already present to
      re-grant it). Revoking any other role from yourself, or revoking
      `FullAdmin` from someone *else*, is unaffected. `RoleRevokeCommand`
      checks `RevokeRole`'s `string?` return (not a caught exception) and
      relays a non-null failure straight to the admin as the rejection
      message.
- [x] Register all 8 via `RegisterWithRole` in a new
      `AdminCommands.RegisterAll(registry, repository)`
      (`src/SharpMud.Engine/Commands/Builtin/Admin/AdminCommands.cs` —
      mirrors `BuiltinCommands`'s shape). **Wiring point, corrected from
      the pre-ADR-0006 plan**: there's no monolithic `Program.cs`
      `RegisterAll` sequence to append to anymore — `Hosting`'s
      `AddSharpMudRuleset(...)` already auto-calls `BuiltinCommands
      .RegisterAll` and then invokes a single consumer callback (calling
      it, or the underlying `AddSharpMudRuleset`, a second time
      independently would silently clobber the first registration per
      ADR-0008's Decision Outcome). Call `AdminCommands.RegisterAll` from
      inside the `registerConsumerCommands` callback already passed to
      `AddSharpMudRpgRuleset<ClassicCombatOutcomeHandler>(...)` in
      `samples/SharpMud.Samples.Classic/Program.cs`, resolving
      `IThingRepository` from that callback's own `IServiceProvider`
      (`sp.GetRequiredService<IThingRepository>()`) — same pattern
      `SharpMud.Ruleset.Rpg`'s own `ServiceCollectionExtensions.cs` uses
      to resolve its own dependencies inside that callback shape.

### Login-flow + bootstrap

**Design simplified from the original two-checkpoint plan** (see ADR-0005's
correction note) — `LoginFlow` is a DI-constructed singleton today (`src
/SharpMud.Hosting/LoginFlow.cs`, resolved via `_serviceProvider
.GetRequiredService<LoginFlow>()` per connection in `TelnetTransportBackground
Service.HandleConnectionAsync`), not manually parameter-threaded through a
per-connection context object — there is no `TelnetHostContext`/
`HostRunner` to thread a value through anymore. That makes a single
in-`LoginFlow` check both simpler and more correct than the original
two-separate-checkpoints design: it runs for every login, whether the
character is brand-new (`MaybeCreateAsync`) or pre-existing
(`LoginExistingAsync`), covering the fresh-server case, the restart case,
and "admin logs in later after the env var was set," all identically —
no separate boot-time code path to keep in sync with the login-time one.

- [x] `LoginFlow.LoginExistingAsync`: after password verification
      succeeds, check `IsBanned` → reject with a distinct message before
      the `ConnectionState` branch.
- [x] `src/SharpMud.Hosting/SharpMudHostOptions.cs`: add `string?
      InitialAdminUsername`, parsed from `SHARPMUD_INITIAL_ADMIN` in
      `SharpMudHostOptions.Parse(env)`.
- [x] `samples/SharpMud.Samples.Classic/Program.cs`: add
      `["SHARPMUD_INITIAL_ADMIN"] = Environment.GetEnvironmentVariable
      ("SHARPMUD_INITIAL_ADMIN")` to the `env` dictionary already built
      before `SharpMudHostOptions.Parse(env)` — that dictionary is a
      **fixed** set of the vars this sample actually reads (currently just
      `SHARPMUD_DB_PATH`), not a full environment pass-through, so the new
      var needs its own entry here or the real host never sees it even
      with it set in production.
- [x] `builder.Services.AddSingleton(hostOptions);` in the same
      `Program.cs`, right after `SharpMudHostOptions.Parse(env)` — **new
      requirement this plan's implementer needs that the original didn't**:
      `SharpMudHostOptions` isn't registered in DI today (`Program.cs`
      only ever uses it as a local value, e.g. `hostOptions.DbPath` passed
      directly into `AddSharpMudSqlitePersistence(...)`), but `LoginFlow`
      needs to constructor-inject it now, so it has to actually be in the
      container.
- [x] `LoginFlow`: constructor-inject `SharpMudHostOptions` (a 4th
      dependency, alongside `WorldContext`/`IThingRepository`/
      `IPlayerFactory`). In `RunAsync`, after `LoginExistingAsync`/
      `MaybeCreateAsync` produces a non-null `player`, before returning
      it: if `_hostOptions.InitialAdminUsername` is set and case-
      -insensitively equals that player's `PlayerBehavior.Username`, and
      they don't already hold `FullAdmin`, call `GrantRole(FullAdmin)`
      (which accumulates `MinorAdmin`/`Player` too, per the accumulation
      rule) and `SaveTreeAsync` immediately — same "persist immediately,
      don't wait for disconnect" reasoning `MaybeCreateAsync` already
      applies to a freshly-created character. Idempotent by construction
      (the `FullAdmin` check), so safe to run on every login for that
      username, not just the first.

### Docs

- [x] `docs/commands.md`: describe the new admin command set as current
      state, link ADR-0005.
- [x] `docs/accounts-auth.md`: describe ban enforcement in the login flow
      as current state, update the existing "deferred moderation tooling"
      forward-reference to point at ADR-0005/this plan.
- [x] `docs/deployment.md`: add `SHARPMUD_INITIAL_ADMIN` to the Runtime
      Configuration table — this table is the documented list of every
      `SharpMudHostOptions`/sample-level env var, and
      `SHARPMUD_INITIAL_ADMIN` is the *only* bootstrap path to a
      `FullAdmin` in a fresh deployment; omitting it here means deploying
      a container with no discoverable way to administer it.
- [x] `SPEC.md`: update the "Moderation/admin tooling" Deferred/Open Item
      to reflect what's actually implemented vs. still deferred (Find/
      GoTo/Control/Clone/Spawn/Jail/Buff/Relinquish, audit logging).
- [x] `docs/adr/README.md` / `docs/plans/README.md`: index rows for
      ADR-0005/PLAN-0005 flip to `Accepted`/in-progress-then-`Done`.
- [x] `docs/plans/0001-wheelmud-reconciliation-roadmap.md`: check off
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
- `src/SharpMud.Engine/Commands/ICommandRegistry.cs`, `CommandRegistry.cs`,
  `Builtin/HelpCommand.cs`, `Builtin/BuiltinCommands.cs`
- `src/SharpMud.Ruleset.Rpg/ServiceCollectionExtensions.cs` (`Register` →
  `RegisterOpen` for `AttackCommand`/`FleeCommand` — call site added by
  ADR-0008, after this plan was first written)
- `src/SharpMud.Engine/Behaviors/PlayerBehavior.cs`
- `src/SharpMud.Persistence/Configurations/PlayerBehaviorConfiguration.cs`
- `src/SharpMud.Hosting/LoginFlow.cs`, `SharpMudHostOptions.cs`,
  `SessionLoop.cs`
- `samples/SharpMud.Samples.Classic/Program.cs`
- `tests/SharpMud.Hosting.Tests/*` (`LoginFlow`/`SharpMudHostOptions`
  bootstrap coverage)
- `docs/commands.md`, `docs/accounts-auth.md`, `docs/deployment.md`,
  `SPEC.md`, `docs/adr/README.md`, `docs/plans/README.md`,
  `docs/plans/0001-wheelmud-reconciliation-roadmap.md`

## Test plan

- Unit: `SecurityRole` — every named member (excluding `None`/`All`) is a
  distinct power of two, and no two members share a bit
  (`Enum.GetValues<SecurityRole>()` pairwise-AND'd should all be `None`
  except `All`/self-comparisons). `All` equals the OR of every individual
  flag. This is the one test that would immediately catch the
  undefined-values gap if the enum were ever implemented with
  auto-numbered members.
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
  persisted) being correctly treated as *not* online.
- Unit: `SessionLoop` — a session ending with `WasBooted` set (but not
  `explicitQuit`) takes the same immediate-removal path `explicitQuit`
  takes, not `EnterLinkdead`. Without this, a booted player would just
  resume via the `Linkdead` reconnect path, making `boot` a no-op as a
  moderation tool.
- Unit: `HelpCommand` — a role-gated command is omitted from the listing
  for an actor without the required role, and included for one with it;
  non-gated commands always list regardless of role.
- Unit: `BanCommand` — self-targeting is rejected with a clear message,
  `IsBanned` unchanged; targeting another player still works normally.
- Unit: `RoleRevokeCommand` — revoking your own `FullAdmin` is rejected;
  revoking a different role from yourself, or `FullAdmin` from someone
  else, still works normally.
- Unit: `RoleGrantCommand`/`RoleRevokeCommand` — `all` and `none` (any
  casing) are rejected with a clear message and never reach
  `GrantRole`/`RevokeRole`; every other individually-grantable role name
  is accepted.
- Unit: `LoginFlow` — banned user rejected at password verification with
  the correct message, not silently falling through.
- Unit: `PlayerBehavior.GrantRole`/`RevokeRole`/`Mute`/`Unmute`/`Ban`/
  `Unban` — state mutates as expected. Specifically cover accumulation:
  `GrantRole(FullAdmin)` results in `Roles` containing `FullAdmin`,
  `MinorAdmin`, *and* `Player`; `GrantRole(FullBuilder)` results in
  `FullBuilder` + `MinorBuilder`.
- Unit: a `FullAdmin`-only actor (post-accumulation) successfully passes a
  `MinorAdmin`-gated `RoleGuardedCommand` — the regression test for the
  bootstrap-admin-can't-moderate gap.
- Unit: `RevokeRole(MinorAdmin)` on an actor who also holds `FullAdmin`
  returns a non-null failure message and leaves `Roles` unchanged (and
  that failure is a return value, not a thrown exception, per
  `coding-standards.md`'s Error Handling section). `RevokeRole(FullAdmin)`
  on that same actor returns `null` (success) and leaves
  `MinorAdmin`/`Player` intact (demotion, not a full reset — revoking the
  top tier doesn't cascade-clear what it implied). `RevokeRole(MinorAdmin)`
  on an actor who does *not* also hold `FullAdmin` returns `null`
  (success) normally.
- Unit: `SharpMudHostOptions.Parse` — `SHARPMUD_INITIAL_ADMIN` parses
  correctly, absent env var leaves it null.
- Unit: `LoginFlow` — a login (new-character and existing-character paths
  both) whose username matches `InitialAdminUsername` gets `FullAdmin`
  idempotently (repeat logins don't re-grant or error); a non-matching or
  null `InitialAdminUsername` grants nothing. Replaces the original plan's
  two-separate-mechanism tests (a Program.cs-level check plus a
  `MaybeCreateAsync`-level check) with one set of `LoginFlow`-level tests,
  matching the simplified single-checkpoint design.

## Verification

**Done (2026-07-23)** — all 9 steps below run manually over real Telnet
connections against the Classic sample; all passed as described. Found and
noted (not fixed, out of scope) one pre-existing, unrelated crash: the
game loop's `WanderManager` tick broadcasts to every occupant of a room
including `Linkdead` ones with a dead socket, and an unhandled
`IOException`/`SocketException` there brings down the whole process — this
predates ADR-0005 and isn't part of its mechanism.

Real manual check over Telnet (this repo's established pattern for
session/persistence-facing changes):

1. **Fresh-server case**: boot a brand-new world with
   `SHARPMUD_INITIAL_ADMIN=<username>` set, and only *then* create that
   character over Telnet — confirm the grant happens at creation time.
   Confirm the resulting admin can run *both* a `FullAdmin`-gated command
   (e.g. `ban`) *and* a `MinorAdmin`-gated one (e.g. `boot`) without a
   separate grant — validates the accumulation fix.
2. **Restart case**: restart the server (same world, same
   `SHARPMUD_INITIAL_ADMIN`); confirm the existing admin's roles are
   unaffected (idempotent, no duplicate grants/errors) after logging back
   in.
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
   for `WasBooted` actually crossing the `SessionLoop` boundary.
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
