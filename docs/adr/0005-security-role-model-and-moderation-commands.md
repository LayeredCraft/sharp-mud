# [ADR-0005] Security Role Model + Moderation Commands

**Status:** Accepted

**Date:** 2026-07-17

**Decision Makers:** solo (design dive conducted with the user)

## Context

Per ADR-0001, this is Slice 3 of the WheelMUD reconciliation roadmap.
Verified by direct research (grep across `src/`/`tests/`, full read of
`docs/commands.md`, `docs/accounts-auth.md`, `ICommand.cs`,
`ICommandRegistry.cs`, `CommandGuards.cs`, `PlayerBehavior.cs`): sharp-mud
has **no authorization concept at all** today. Every command any connected
player types is unconditionally resolved and executed —
`CommandGuards.cs` has exactly one guard (`RequireArgsAsync`, an
arg-presence check), nothing role-related. `SPEC.md`'s Deferred/Open Items
lists "Moderation/admin tooling... Known future need, not designed yet."

WheelMUD's own mechanism (`Core/Attributes/ActionSecurityAttribute.cs` +
`Actions/Admin/`, 16 files): a `[Flags] enum SecurityRole` (`mobile` /
`item` / `room` / `tutorialPlayer` / `player` / `helper` / `married` /
`minorBuilder` / `fullBuilder` / `minorAdmin` / `fullAdmin` / `all`,
12 real values), an `[ActionSecurity(SecurityRole.x)]` class attribute on
each Action, reflected off at registration time into a `Command
.SecurityRole` field, gated at dispatch by a bitwise-AND check
(`command.SecurityRole & user.SecurityRoles`) in `CommandGuard.cs`.
`Actions/Admin/` (`Announce`, `Ban`, `Boot`, `Buff`, `Clone`, `Control`,
`Find`, `GoTo`, `Jail`, `Locate`, `Mute`, `Relinquish`, `RoleGrant`,
`RoleRevoke`, `Spawn`, `Unmute`) are ordinary Actions decorated with
high-tier roles — no separate mechanism from any other command.

## Decision Drivers

- No new `Thing` subtype (`design-decisions.md` rule 2) — role/mute/ban
  state lives on `PlayerBehavior`, the existing identity `Behavior`.
- This repo has twice already (ADR-0002, ADR-0004) chosen a leaner
  reimplementation over a faithful WheelMUD structural port when the
  extra structure doesn't earn its keep here — evaluated directly against
  WheelMUD's attribute+reflection mechanism as part of this dive.
- The user has a compile-time decorator-generation package
  (`LayeredCraft.DecoWeaver`) and asked whether it fit. Verified against
  its own docs (`https://decoweaver.layeredcraft.dev/`,
  `https://github.com/LayeredCraft/decoweaver`): it only intercepts
  `Microsoft.Extensions.DependencyInjection` registration calls
  (`AddScoped<TInterface,TImpl>()` etc. via C# 11 interceptors).
  sharp-mud's commands are registered directly into a custom
  `ICommandRegistry` (`BuiltinCommands.RegisterAll`/
  `ClassicCommands.RegisterAll` call `registry.Register(...)`), never
  through `IServiceCollection` — DecoWeaver cannot see them without first
  re-plumbing command registration through the DI container, a real,
  separate architecture change with no driver of its own right now
  (`code-of-conduct.md`: don't bundle an unrelated change into this one).
- A missing guard on a security boundary is a materially worse failure
  mode than a missing guard on a UX check (the existing `RequireArgsAsync`
  precedent) — an admin command silently callable by any player is a real
  vulnerability, not a confusing error message. This pushed the decision
  toward something structurally harder to forget than "paste a guard call
  at the top of `ExecuteAsync`."
- `docs/persistence.md`/`WearableBehaviorConfiguration.cs`'s existing
  precedent: `WearableBehavior.Slot` (backed by `EquipSlot`) is a **plain**
  C# enum with EF Core's default int mapping, not a
  `LayeredCraft.OptimizedEnums` instance — confirmed, `OptimizedEnum` in
  this repo is for small non-combinable state machines (`Race`,
  `CharacterClass`, `ConnectionState`). Corrected during PR review:
  `EquipSlot` is **not** itself `[Flags]` (it's an ordinary enum, not a
  bitmask), and there is no existing `[Flags]` enum anywhere in this repo
  today — `SecurityRole` follows `Slot`'s "plain enum over `OptimizedEnum`"
  precedent, but is the first genuinely bitwise-combinable `[Flags]` enum
  in the codebase, not a second instance of an existing pattern.
- Bootstrap problem, surfaced during the dive: if granting a role itself
  requires `FullAdmin`, and every new character defaults to `Player`,
  nothing in-game can ever produce the first admin.

## Considered Options

1. **Explicit per-command guard call**, mirroring the existing
   `CommandGuards.RequireArgsAsync` pattern — each gated command's
   `ExecuteAsync` starts with `if (await CommandGuards.RequireRoleAsync(...))
   return;`.
2. **`ICommand.RequiredRole` property**, checked centrally by `SessionLoop`
   before `ExecuteAsync` ever runs — every `ICommand` implementation must
   supply it (compile error otherwise).
3. **Attribute + reflection**, a close port of WheelMUD's
   `[ActionSecurity(...)]` + registration-time reflection.
4. **Compile-time decorator via `LayeredCraft.DecoWeaver`** — requires
   first re-plumbing command registration through
   `Microsoft.Extensions.DependencyInjection`.
5. **Hand-rolled Decorator pattern**: `RoleGuardedCommand : ICommand` wraps
   an inner `ICommand`, checks the actor's roles against a required mask,
   and delegates or rejects. `ICommandRegistry`'s single generic
   `Register(ICommand)` is replaced with exactly two intentional entry
   points, `RegisterOpen(ICommand)` and `RegisterWithRole(ICommand,
   SecurityRole)` — the latter always applies the wrapper, so there is no
   third, accidental way to register a command without declaring its
   access level.

## Decision Outcome

Chosen option: **"5 — hand-rolled Decorator pattern with an intentional
two-method registry API,"** arrived at through the dive: option 1 was
rejected once framed as a security boundary specifically (forgettable by
construction); option 2 works but forces every existing `ICommand`
(~14 classes, including harmless ones like `look`) to declare a role it
doesn't need, just to satisfy the interface; option 3 repeats this repo's
now-established pattern of not faithfully porting WheelMUD's structure
when it doesn't earn its keep; option 4 doesn't fit sharp-mud's actual
command-registration model without an unrelated prerequisite change. The
Decorator pattern is the *actual* thing the user was reaching for with
DecoWeaver — DecoWeaver is one implementation strategy for it (compile-time
codegen over DI-registered services), not the pattern itself; hand-writing
it here gets the real benefit (composable, independently-testable
cross-cutting command wrappers — the next one, e.g. a cooldown check, is
just another wrapper of the same shape) without DecoWeaver's registration
-model requirement.

**Mechanism**, in full:
- `SecurityRole` (`SharpMud.Engine.Commands`): a plain `[Flags] enum`,
  full WheelMUD 12-value set ported (PascalCase: `None = 0`, `Mobile = 1
  << 0`, `Item = 1 << 1`, `Room = 1 << 2`, `TutorialPlayer = 1 << 3`,
  `Player = 1 << 4`, `Helper = 1 << 5`, `Married = 1 << 6`, `MinorBuilder
  = 1 << 7`, `FullBuilder = 1 << 8`, `MinorAdmin = 1 << 9`, `FullAdmin =
  1 << 10`, `All = Mobile | Item | Room | TutorialPlayer | Player |
  Helper | Married | MinorBuilder | FullBuilder | MinorAdmin |
  FullAdmin`). **Explicit values are load-bearing, not stylistic** —
  caught in PR review: without them, C# auto-numbers enum members
  sequentially (`0, 1, 2, 3...`), which are *not* distinct bits (`Room`
  would auto-number to `3`, silently equal to `Mobile | Item` combined,
  and the `RoleGuardedCommand` bitwise-AND check would grant unrelated
  permissions on overlapping bits). `All` is defined as the union of the
  individual flags, not a separate hardcoded value (e.g. `uint.MaxValue`)
  — so it can't drift out of sync if a flag is ever added later. Adopted
  in full even though several values (`Mobile`/`Item`/`Room`: sharp-mud
  has no non-player command issuer today; `Married`: no marriage system;
  `TutorialPlayer`/builder tiers: not yet used) are inert now, so future
  slices (world-building/OLC is Slice 4, explicitly bundled with this one
  in ADR-0001) already have a role to reach for instead of extending the
  enum again.
- `PlayerBehavior` gains `SecurityRole Roles` (persisted — unlike
  `ConnectionState`/`Session`, a role assignment must survive a restart or
  it's useless; default `Player` for new characters), plus `bool IsMuted`
  and `bool IsBanned` (also persisted; separate from `Roles` — a
  restriction, not a capability).
- **Roles accumulate at grant time, matching WheelMUD's own behavior**
  (confirmed during research: WheelMUD's `UserControlledBehavior
  .SecurityRoles` is a bitwise-OR accumulation, e.g. "a promoted builder
  keeps `player | minorBuilder | fullBuilder`," not just the newest tier's
  bit alone). `PlayerBehavior.GrantRole(SecurityRole role)` ORs in the
  requested role *and* every tier it implies: `FullAdmin` implies
  `MinorAdmin` implies `Player`; `FullBuilder` implies `MinorBuilder`.
  This matters because `RoleGuardedCommand`'s check stays a simple
  bitwise AND with no hierarchy logic of its own — without accumulation,
  a user granted only `FullAdmin` would fail every `MinorAdmin`-gated
  command (`boot`/`mute`/`unmute`/`announce`), since `FullAdmin` and
  `MinorAdmin` are independent bits with no inherent relationship. Caught
  during PR review (the bootstrap admin would otherwise be unable to run
  day-to-day moderation commands) — see `SHARPMUD_INITIAL_ADMIN` below,
  which relies on this same accumulation. The same invariant has to hold
  on the way out, not just the way in — also caught during PR review:
  `PlayerBehavior.RevokeRole(SecurityRole role)` rejects (rather than
  silently applying) a revoke of a tier still implied by a higher one the
  target currently holds — e.g. revoking `MinorAdmin` from someone who
  still has `FullAdmin` would otherwise leave `FullAdmin` set with
  `MinorAdmin` cleared, breaking "`FullAdmin` implies `MinorAdmin`" for
  that user going forward. The rejection names the blocking higher tier
  so the admin knows to revoke that one first (or instead).
- `RoleGuardedCommand` checks `(actor.Roles & requiredRole) !=
  SecurityRole.None` (any-of semantics, matching WheelMUD's own bitwise
  check) before delegating to the inner command. The same Decorator shape
  is reused for mute enforcement (`MuteGuardedCommand`, wrapping `say`/
  `emote`, checking the *actor's own* `IsMuted` — it's the speaker being
  blocked from speaking, not anything about a target) — validating the
  pattern's reusability for a cross-cutting concern that isn't role-based
  at all.
- `ICommandRegistry.Register(ICommand)` is removed from the public
  interface; `RegisterOpen(ICommand)` and `RegisterWithRole(ICommand,
  SecurityRole)` are the only ways in. Every existing command's
  registration call site changes from `Register` to `RegisterOpen`
  (mechanical, no behavior change for them).
- Ban is enforced in `LoginFlow` at successful password verification
  (`IsBanned` → reject, distinct message).

**Command set for this slice** — the subset of WheelMUD's 16 Admin
actions that map onto systems sharp-mud already has; the rest need
prerequisite infrastructure this slice doesn't build (Find/Locate/GoTo/
Control need world/NPC lookup and puppeting; Clone/Spawn need item/NPC
creation tooling; Jail needs a cell-room concept; Buff needs a generic
stat-modification system; Relinquish is builder-role-specific and tied to
Slice 4):

| Command | Required role | Notes |
|---|---|---|
| `Boot` | `MinorAdmin` | Disconnects a currently-online target. |
| `Mute` / `Unmute` | `MinorAdmin` | Sets/clears `IsMuted`; enforced on `say`/`emote` via `MuteGuardedCommand`. |
| `Announce` | `MinorAdmin` | Broadcasts to every connected session. |
| `Ban` / `Unban` | `FullAdmin` | Sets/clears `IsBanned`, enforced in `LoginFlow`. `Unban` has no WheelMUD equivalent — added here because a ban with no in-game reversal is an operability trap, not because WheelMUD has one. |
| `RoleGrant` / `RoleRevoke` | `FullAdmin` | Mutates a target's `Roles`. Gated at `FullAdmin` specifically so a `MinorAdmin` can never self-escalate. |

Day-to-day moderation (`Boot`/`Mute`/`Unmute`/`Announce`) sits at
`MinorAdmin`; harder-to-reverse or privilege-affecting actions
(`Ban`/`Unban`/`RoleGrant`/`RoleRevoke`) require `FullAdmin`. Target lookup
(online or not) mirrors `LoginFlow.FindAndAttachExistingAsync`'s existing
live-then-repository pattern; an offline target is loaded, mutated, and
saved without being attached into the live world tree (no need to, unlike
login).

**Bootstrap**: `HostOptions` gains `SHARPMUD_INITIAL_ADMIN` (env var,
matching the existing `SHARPMUD_MODE`/`SHARPMUD_TELNET_PORT`/
`SHARPMUD_DB_PATH` precedent). The grant is checked in **two** places, not
just one — caught during PR review that checking only once at boot is a
no-op on a genuinely fresh server, since the target character doesn't
exist yet at boot time and only gets created later through the normal
login flow:
1. At boot (after world load), if a character with that username already
   exists (a restart of an existing world) — the original case.
2. At the moment a character with that username is actually created
   (`LoginFlow.MaybeCreateAsync`), covering the fresh-server case.

Both paths are idempotent and go through the same `GrantRole(FullAdmin)`
call (which accumulates `MinorAdmin`/`Player` too, per the accumulation
rule above) — safe to leave `SHARPMUD_INITIAL_ADMIN` set permanently, or
unset after first use. Solves "how does the first admin ever get
`FullAdmin`" without an in-game path that would otherwise be a
chicken-and-egg dead end.

### Positive Consequences

- Closes a real, previously-total gap: no command in this codebase was
  ever gated by anything before this.
- The Decorator mechanism is reusable for the *next* cross-cutting
  command concern (a cooldown, a "must not be in combat" check) without
  inventing a new approach each time — validated within this same slice
  by reusing it for mute enforcement, not just role-gating.
- `RegisterOpen`/`RegisterWithRole` replacing a single generic `Register`
  means every command registration is a legible, intentional statement of
  its own access level — there's no third, silent way in.
- Adopting the full WheelMUD role set now means Slice 4 (world-building,
  already flagged in ADR-0001 as needing security gating) has
  `MinorBuilder`/`FullBuilder` ready rather than needing another enum
  change.

### Negative Consequences

- `RegisterWithRole` still depends on the person registering a command
  choosing the right entry point — not literally impossible to misuse
  (e.g. calling `RegisterOpen` on something that should have been gated),
  just much harder to do by accident than a scattered guard call, since
  the two entry points sit side by side at every registration call site.
- Most of the 12-value `SecurityRole` set (`Mobile`/`Item`/`Room`/
  `TutorialPlayer`/`Married`) has no consumer yet — accepted as the cost
  of not re-deriving the enum later, but it is real unused surface area
  today.
- `SHARPMUD_INITIAL_ADMIN` is a standing env var that, if left set and a
  malicious actor ever created a character with that exact username on a
  fresh world, would hand them `FullAdmin` — acceptable for a
  solo/small-group server at this stage, worth revisiting if/when this
  goes to a wider public deployment (`docs/deployment.md`).

## Pros and Cons of the Options

### Option 1: Explicit per-command guard call

- Good, because it requires no interface or registry changes at all.
- Good, because it matches an already-established repo pattern
  (`RequireArgsAsync`).
- Bad, because forgetting it is silent and the failure mode is a real
  security hole, not a UX inconvenience — the exact risk profile that
  makes this the wrong pattern to reuse here.

### Option 2: `ICommand.RequiredRole` property

- Good, because it's structurally impossible to omit — a compile error,
  not a runtime gap.
- Good, because the check lives in exactly one place (`SessionLoop`'s
  dispatch), not scattered per-command.
- Bad, because it forces every existing `ICommand` (including commands
  that need no gating at all) to declare a role, touching ~14 files for
  no functional benefit to those commands.

### Option 3: Attribute + reflection (WheelMUD-faithful)

- Good, because it's a direct, low-judgment translation of prior art.
- Bad, because this repo has already twice (ADR-0002, ADR-0004) rejected
  faithfully porting WheelMUD's structure in favor of something leaner
  fit to sharp-mud's actual needs, and the same reasoning applies here:
  reflection-based registration-time discovery solves a
  plugin-discoverability problem sharp-mud's fixed, hand-wired
  `RegisterAll` calls don't have.

### Option 4: Compile-time decorator via DecoWeaver

- Good, because it's genuinely zero-runtime-overhead and would give
  broader decoration for free if the app already used DI-container-based
  registration everywhere.
- Bad, because sharp-mud's commands aren't DI-container-registered today,
  and re-plumbing that is a separate, unrelated architecture change with
  no driver of its own yet.

### Option 5: Hand-rolled Decorator pattern (chosen)

- Good, because it needs zero `ICommand` interface changes and zero
  registration-model changes — a genuinely additive change.
- Good, because it's the actual design pattern the user was reaching for,
  independent of any specific codegen tool, and it's proven reusable
  within this same slice (mute enforcement, not just role-gating).
- Good, because the `RegisterOpen`/`RegisterWithRole` split closes almost
  all of option 1's "forgettable" risk without option 2's forced,
  unnecessary interface changes.
- Bad, because enforcement is still a registration-time discipline, not a
  compile-time guarantee — mitigated, not eliminated, by there being only
  two, clearly-named ways to register anything.

## Links

- [ADR-0001](0001-wheelmud-reconciliation-roadmap.md) — WheelMUD
  Reconciliation Roadmap (this is Slice 3; Slice 4 world-building is
  explicitly bundled with this one and will consume the same mechanism).
- [PLAN-0005](../plans/0005-security-role-model-and-moderation-commands.md)
  — execution plan for this decision.
- `SPEC.md` — "Moderation/admin tooling" Deferred/Open Item this ADR
  resolves (not yet implemented — see the plan).
- `docs/accounts-auth.md` — the existing forward-reference to "deferred
  moderation tooling" this ADR makes concrete; ban enforcement lands in
  `LoginFlow`.
- `docs/commands.md` — command pipeline (`ICommand`/`ICommandRegistry`)
  this ADR extends.
- `docs/deployment.md` — Runtime Configuration table `SHARPMUD_INITIAL_ADMIN`
  joins (caught in PR review — it's the only bootstrap path to a
  `FullAdmin` in a fresh deployment, and this table is the documented
  list of every `HostOptions` env var).
- `docs/persistence.md` / `WearableBehaviorConfiguration.cs` — the
  existing plain-enum-with-default-EF-mapping precedent `SecurityRole`
  follows for "plain enum, not `OptimizedEnum`" (`EquipSlot` itself is not
  `[Flags]` — corrected during PR review).
- `WheelMUD/src/Core/Attributes/ActionSecurityAttribute.cs`,
  `WheelMUD/src/Core/ManagerSystems/CommandManager.cs`,
  `WheelMUD/src/Core/CommandSystem/CommandGuard.cs`,
  `WheelMUD/src/Core/Behaviors/UserControlledBehavior.cs`,
  `WheelMUD/src/Actions/Admin/` — source researched for this decision.
- `https://decoweaver.layeredcraft.dev/`,
  `https://github.com/LayeredCraft/decoweaver` — DecoWeaver documentation
  consulted and ruled out for this slice (registration-model mismatch).
