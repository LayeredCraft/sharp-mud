# [PLAN-0008] A Reusable RPG Scaffolding Tier Between `Engine` and Concrete Rulesets

**Implements:** [ADR-0008](../adr/0008-ruleset-scaffolding-tier.md) (`Accepted`)

**Status:** Done

**Last updated:** 2026-07-23

## Goal

A consumer can `dotnet add package SharpMud.Ruleset.Basic` (plus `Engine`/
`Hosting`/`Persistence.*`/a transport adapter), write a small `Program.cs`,
and run a real, if minimal, playable MUD with working combat — without
writing any combat/stats/encounter code themselves. Separately, a consumer
building a *different* ruleset can reference `SharpMud.Ruleset.Rpg` alone and
get the same combat/encounter scaffolding without `Basic`'s specific numbers.
`samples/SharpMud.Samples.Classic` still works exactly as it does today from
a player's perspective, now built on `SharpMud.Ruleset.Rpg` instead of owning
combat scaffolding directly.

## Scope

In scope:
- Extract `CombatantBehavior`/`ICombatResolver`/`CombatResolver`/
  `ICombatManager`/`CombatManager`/`AttackCommand`/`FleeCommand` into a new
  `SharpMud.Ruleset.Rpg` package.
- Decouple `CombatManager`'s XP-award/death-penalty logic from
  `StatsBehavior` (exact mechanism — game event vs. callback interface — is
  this plan's to decide, per ADR-0008's Open Items).
- Decouple `CombatManager`'s constructor-injected `Thing hubRoom` respawn
  destination — a Classic-specific world/respawn policy hard-coded into what
  should be generic combat logic. Plausibly the same combat-outcome
  mechanism above also owns "where does the loser respawn," rather than a
  second bespoke seam — exact shape is this plan's to decide.
- Move `CombatantBehavior`'s EF Core mapping (`CombatantBehaviorConfiguration`)
  into an `SharpMud.Ruleset.Rpg`-owned `IBehaviorMappingContributor`, since
  today's `ClassicBehaviorMappingContributor.ConfigureBehaviors` only scans
  its own (Classic) assembly for `IEntityTypeConfiguration<>` types — once
  `CombatantBehavior` lives in a different assembly, that scan silently stops
  finding its configuration and a saved world containing it hits an unmapped
  TPH discriminator subtype.
- A small dice-rolling abstraction in `SharpMud.Ruleset.Rpg`, built on the
  existing `IRandomSource` (constructor-injected, DI-registered — not a
  WheelMUD-style static singleton), covering "N dice of M sides plus a
  modifier." Replace `CombatResolver`/`FleeCommand`'s raw `random.Next(...)`
  calls with it where it's a straightforward swap.
- Build a new, minimal `SharpMud.Ruleset.Basic` package on top of
  `SharpMud.Ruleset.Rpg`: a plain numeric stat block, a default
  `IWorldBuilder` (a room or two), DI registration extension
  (`AddSharpMudBasicRuleset(...)`).
- Rebuild `samples/SharpMud.Samples.Classic` against `SharpMud.Ruleset.Rpg`
  instead of owning the extracted types directly.

Explicitly deferred / out of scope for this plan:
- Any additional ruleset "flavors" beyond `Basic` (`Freeform`/`Tactical`
  from ADR-0008's discussion) — speculative, not committed to.
- Deciding whether `Ruleset.Rpg`/`Ruleset.Basic` join the `SharpMud`
  meta-package (ADR-0008 Open Items) — treat as a follow-up ADR-0007-style
  narrow decision if/when it comes up, not settled here.
- A leveling/progression contract (`ILevelingStrategy` or similar) beyond
  whatever XP-award mechanism this plan lands on — only build it if the
  decoupling work actually needs it.

## Tasks

- [x] New `src/SharpMud.Ruleset.Rpg` project
  - [x] Move `CombatantBehavior`, `ICombatResolver`/`CombatResolver`,
        `ICombatManager`/`CombatManager`, `AttackCommand`/`FleeCommand` in
        from `samples/SharpMud.Samples.Classic`
  - [x] Design and implement the `StatsBehavior` decoupling (game event
        through `ThingEvents`, or a small callback interface — pick one,
        document why in a code comment per `documentation.md`'s bar for
        non-obvious decisions)
  - [x] Design and implement the `hubRoom`/respawn-destination decoupling —
        consider folding into the same mechanism as the `StatsBehavior`
        decoupling above rather than a second bespoke seam
  - [x] While touching this: today's death-penalty handling only resets
        `StatsBehavior.CurrentHitPoints`, but `CombatResolver` actually reads
        and writes damage against `CombatantBehavior.CurrentHitPoints` — a
        respawned character's `CombatantBehavior` HP stays at/below 0, so the
        very next hit lands and instantly re-triggers "defeated" regardless
        of the roll. This is a real, pre-existing bug (not introduced by this
        extraction), but the decoupled respawn mechanism must reset
        `CombatantBehavior.CurrentHitPoints` too, not just carry the
        `StatsBehavior`-only reset forward into the new package
  - [x] Move `CombatantBehaviorConfiguration` + a new `SharpMud.Ruleset.Rpg`-
        owned `IBehaviorMappingContributor` in from
        `ClassicBehaviorMappingContributor`; verify `Basic`/Classic's own DI
        registration actually wires this contributor up (`Persistence`
        discovers contributors via DI, per `docs/persistence.md`)
  - [x] Dice-rolling abstraction over `IRandomSource` (DI-registered
        interface + implementation, not a static singleton — "N dice of M
        sides plus a modifier"); swap `CombatResolver`/`FleeCommand`'s raw
        `random.Next(...)` calls to use it
  - [x] A package-level DI registration entry point (`AddSharpMudRpgRuleset(...)`
        or equivalent) that reproduces today's manual wiring in
        `samples/SharpMud.Samples.Classic/Program.cs` (`ICombatResolver`,
        `ICombatManager` registered as both itself *and* `ITickable` off the
        same instance, `IBehaviorMappingContributor`, the dice service) —
        without this, `Basic`/Classic/a custom ruleset each hand-roll the
        exact scaffolding this ADR exists to provide, verbatim
  - [x] Define how `AttackCommand`/`FleeCommand` registration composes with
        a consumer's own commands. `Hosting`'s `AddSharpMudRuleset(...)` takes
        a single callback — calling it a second time (once for Rpg's commands,
        again for `Basic`/Classic's own) registers `ICommandRegistry` as a
        singleton twice, and DI resolution returns only the *last*
        registration, silently dropping the first call's commands entirely.
        This isn't optional wiring detail — without a real answer, either
        Rpg's `attack`/`flee` or the consumer's own commands go missing.
        Plausible shapes: `AddSharpMudRpgRuleset(...)` itself takes and
        forwards a consumer callback (so there's still only one
        `AddSharpMudRuleset` call total), or `ICommandRegistry` registration
        becomes additive across multiple sources instead of one factory —
        exact mechanism is this plan's to decide, same as the other seams
        above
  - [x] `csproj`: `Directory.Packages.props` entry, package metadata matching
        the existing `SharpMud.*` packages
- [x] New `src/SharpMud.Ruleset.Basic` project
  - [x] Minimal concrete stats behavior (plain numeric attributes, no
        Race/CharacterClass)
  - [x] Default `IWorldBuilder` implementation (small, enough to walk
        around) — must include at least one `Thing` with `CombatantBehavior`
        (an NPC) as a valid `attack` target, not just empty rooms; the Goal
        above promises a fresh character can "walk around and fight
        something," which a world with nothing to fight doesn't satisfy
  - [x] `AddSharpMudBasicRuleset(...)` DI extension, options callback for the
        tunable numbers (starting HP, etc.)
  - [x] Basic's new stats behavior needs its own `IEntityTypeConfiguration<>`
        + `IBehaviorMappingContributor`, wired from `AddSharpMudBasicRuleset(...)`
        — `GameDbContext.OnModelCreating` only discovers engine configs plus
        registered contributors (`docs/persistence.md`), so without this a
        Basic world/player carrying the new behavior hits the same unmapped
        TPH subtype problem already caught for `CombatantBehavior`
  - [x] A Basic `IPlayerFactory` implementation (mirrors `ClassicPlayerFactory`
        wrapping `HubWorldBuilder.CreatePlayer`) that creates a `Thing` with
        `PlayerBehavior` plus Basic's stats/combat behaviors, wired from
        `AddSharpMudBasicRuleset(...)` via `AddSharpMudPlayerFactory<T>()` —
        `PlayerLogin`/`LoginFlow` constructor-inject `IPlayerFactory`, so
        without this a fresh CLI/Telnet player can't be created at all and
        the quick-start fails at first login, not just at "no content to see"
- [x] Rebuild `samples/SharpMud.Samples.Classic`
  - [x] Remove the now-extracted types; reference `SharpMud.Ruleset.Rpg`
        instead
  - [x] Verify `StatsBehavior`/`Race`/`CharacterClass`/`HubWorldBuilder`
        still compose correctly against the new decoupling mechanism
- [x] Tests
  - [x] `SharpMud.Ruleset.Rpg.Tests` — mirrors today's Classic combat test
        coverage, moved and adjusted for the decoupling change
  - [x] `SharpMud.Ruleset.Basic.Tests` — new coverage for the minimal
        concrete ruleset
  - [x] `SharpMud.Samples.Classic.Tests` — updated for the new dependency
        shape, same behavioral coverage as before
- [x] Docs
  - [x] Update `engine-vs-ruleset.md`'s project-structure listing to reflect
        the new packages (currently only has ADR-0008's forward-reference)
  - [x] Update `architecture.md` — it owns the solution layout and
        direct-dependency summary; the new `Ruleset.Rpg`/`Ruleset.Basic`
        projects and their dependency direction belong there too, not just
        in `engine-vs-ruleset.md`
  - [x] Update `combat.md`/`character.md` subsystem docs if their described
        "current state" changes
  - [x] Write the actual quick-start guidance the ADR's headline goal
        promises — a README/docsite getting-started page walking through
        `dotnet add package SharpMud.Ruleset.Basic` (+ `Engine`/`Hosting`/a
        persistence provider/a transport adapter) through a runnable basic
        game, plus package-consumption docs for `SharpMud.Ruleset.Rpg`
        itself for a consumer building a different ruleset on it. Without
        this, "few lines in `Program.cs`, run a basic game" is proven by an
        internal manual test (see Verification) but never actually
        documented for a real external consumer to follow.
  - [x] `docs/adr/README.md`: once implementation is complete and matches
        ADR-0008 as written (or any divergence is reconciled), confirm the
        ADR's `Status` reads `Accepted` — it must already be `Accepted`
        *before* this plan moves to `In Progress` (see this plan's header
        and `docs/plans/README.md`'s lifecycle rule); this task is a final
        consistency check, not the acceptance step itself

## Critical files

New:
- `src/SharpMud.Ruleset.Rpg/*` (project + moved types + decoupling mechanism)
- `src/SharpMud.Ruleset.Basic/*` (project + minimal stats/world/DI extension)
- `tests/SharpMud.Ruleset.Rpg.Tests/*`
- `tests/SharpMud.Ruleset.Basic.Tests/*`

Modified:
- `samples/SharpMud.Samples.Classic/*` — removes extracted types, adds
  `SharpMud.Ruleset.Rpg` reference, adds `ClassicCombatOutcomeHandler`
- `tests/SharpMud.Samples.Classic.Tests/*`, `tests/SharpMud.Persistence.Tests/*`
  (`TestDbContextFactory`/`ThingRepositoryTests` now also register
  `RpgBehaviorMappingContributor`)
- `SharpMud.slnx` (new projects added; `Directory.Packages.props` did not
  need a new entry — no new NuGet package IDs, only new project references)
- `docs/architecture.md`, `docs/engine-vs-ruleset.md`, `docs/combat.md`,
  `docs/character.md`, `docs/adr/README.md`, new `docs/getting-started.md`
- `src/SharpMud.Hosting/SharpMud.Hosting.csproj` — two `InternalsVisibleTo`
  entries added (`SharpMud.Samples.Classic.Tests`, `SharpMud.Ruleset.Basic.Tests`)
  so `ICombatOutcomeHandler` implementation tests can populate `WorldContext`
  directly, same reason `SharpMud.Hosting.Tests` already has this grant.
  `ServiceCollectionExtensions.cs`/`ICommandRegistry` itself stayed
  untouched — the forwarding-callback command-composition design was chosen
  (see Open questions below), not the additive-registry alternative.

## Test plan

Unit coverage mirrors today's Classic combat tests, relocated to
`SharpMud.Ruleset.Rpg.Tests`: `CombatResolver` hit/miss/damage math,
`CombatManager` encounter lifecycle (start/end/linkdead-freeze/defeat
handling), `AttackCommand`/`FleeCommand` guard and success paths — same
`AutoFixture`/`NSubstitute` patterns already established
(`testing.md`). The dice-rolling abstraction is new public scaffolding
behavior in its own right, not just a `CombatResolver` implementation
detail — it needs direct tests of its own (dice-count/sides/modifier
math, and validation/error behavior for invalid input like zero dice or
zero-sided dice), not incidental coverage via `CombatResolver`'s tests.
New coverage in `SharpMud.Ruleset.Basic.Tests` for the minimal stat block,
default world builder, and its own EF Core mapping's round-trip. Also add a
test proving `AddSharpMudBasicRuleset(...)` registers `IPlayerFactory` and
that it produces a `Thing` with `PlayerBehavior` plus Basic's stats/combat
behaviors — without this, a broken player factory only surfaces as a
first-login failure, not a test failure. `SharpMud.Samples.Classic.Tests`
should need no *behavioral* changes, only reference/namespace updates,
**except** for the intentional `CombatantBehavior.CurrentHitPoints`
respawn-reset fix below — that one behavioral change is expected and
required, not a signal of an extraction regression. Any *other* behavioral
change is still that signal. Add a persistence
round-trip test (save/load a `Thing` carrying `CombatantBehavior` through
`SharpMud.Persistence`) specifically to catch the TPH-mapping seam above
regressing — this is exactly the kind of gap that passes every unit test
but fails at actual save/load time. Add a defeat/respawn test asserting
`CombatantBehavior.CurrentHitPoints` is actually reset (not just
`StatsBehavior`'s) and the respawned character can be hit again without an
instant re-defeat, to catch the HP-reset bug above regressing. Add a
DI/composition test proving built-in commands, Rpg's `attack`/`flee`, and a
consumer's own registered command all end up in the same resolved
`ICommandRegistry` — this is the seam most likely to silently regress
(one registration source clobbering another) without a test that would
actually catch it. This coverage must prove `AttackCommand`'s actual public
command shape survives, not just "a command exists": today `Verb` is
`kill`, with `attack` registered as an alias, and Verification below
promises both stay compatible — assert the resolved `ICommandRegistry`
still resolves both `kill` and `attack`, not just one representative verb.

## Verification

Manual smoke test per this repo's established pattern for anything
session/gameplay-facing (`testing.md`): run `samples/SharpMud.Samples.Classic`
end-to-end, confirm `kill`/`attack`/`flee` behave identically to before the
extraction, **except** for the intentional death/respawn HP-reset fix above —
a respawned character's `CombatantBehavior.CurrentHitPoints` should now
actually reset, not stay at/below 0 and instantly re-trigger "defeated" on
the next hit; the smoke test should confirm the fix, not treat the old bug
as the expected baseline. Separately, build a throwaway minimal `Program.cs` against just
`SharpMud.Ruleset.Basic` (+ `Engine`/`Hosting`/a persistence provider/a
transport) and confirm a fresh character can log in, actually issue `attack`
against the default world's NPC and see combat resolve (hit/miss/damage,
not just "command not recognized"), and flee — this is the actual "few
lines, runnable basic game with working combat" claim ADR-0008 makes, and
needs a real run exercising the command-registration seam above, not just
passing unit tests, to confirm.

## Open questions / blockers — resolved during implementation

- **`StatsBehavior`/`hubRoom` decoupling**: a single callback interface,
  `ICombatOutcomeHandler` (`OnVictoryAsync`/`OnDefeatAsync`), resolves both
  seams at once, per the ADR's own "plausibly the same mechanism" hint.
  `CombatManager` calls it on encounter resolution; each ruleset implements
  it (`ClassicCombatOutcomeHandler`, `BasicCombatOutcomeHandler`) and
  registers it via `AddSharpMudRpgRuleset<TCombatOutcomeHandler>(...)`.
  `OnDefeatAsync` returns the respawn `Thing`, so `CombatManager` never
  needs a `hubRoom`/`IWorld` reference of its own. The pre-existing
  `CombatantBehavior.CurrentHitPoints` respawn-reset bug is fixed as part of
  this: `CombatManager` resets it unconditionally, before the outcome
  handler runs (the outcome handler only owns its own ruleset's
  stats-behavior-specific reset).
- **Package naming**: kept as `SharpMud.Ruleset.Rpg`/`SharpMud.Ruleset.Basic`
  — no objection raised during implementation.
- **`AttackCommand`/`FleeCommand` composition**: the forwarding-callback
  shape — `AddSharpMudRpgRuleset<TCombatOutcomeHandler>(registerConsumerCommands)`
  itself calls `Hosting`'s `AddSharpMudRuleset(...)` exactly once
  internally, registering `kill`/`attack`/`flee` and then invoking the
  optional consumer callback. `SharpMud.Hosting` needed no changes —
  `ICommandRegistry` stays a single-factory registration, per the plan's
  documented conditional in Critical files below.
