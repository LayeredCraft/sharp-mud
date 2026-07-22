# [PLAN-0008] A Reusable RPG Scaffolding Tier Between `Engine` and Concrete Rulesets

**Implements:** ADR-0008 (link once that ADR is `Accepted` — do not move this
plan to `In Progress` while it still reads `Proposed`)

**Status:** Not Started

**Last updated:** 2026-07-22

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
- Correcting `wheelmud-findings.md`'s `Universe` description — small,
  independent doc fix, pick up opportunistically.

## Tasks

- [ ] New `src/SharpMud.Ruleset.Rpg` project
  - [ ] Move `CombatantBehavior`, `ICombatResolver`/`CombatResolver`,
        `ICombatManager`/`CombatManager`, `AttackCommand`/`FleeCommand` in
        from `samples/SharpMud.Samples.Classic`
  - [ ] Design and implement the `StatsBehavior` decoupling (game event
        through `ThingEvents`, or a small callback interface — pick one,
        document why in a code comment per `documentation.md`'s bar for
        non-obvious decisions)
  - [ ] Design and implement the `hubRoom`/respawn-destination decoupling —
        consider folding into the same mechanism as the `StatsBehavior`
        decoupling above rather than a second bespoke seam
  - [ ] Move `CombatantBehaviorConfiguration` + a new `SharpMud.Ruleset.Rpg`-
        owned `IBehaviorMappingContributor` in from
        `ClassicBehaviorMappingContributor`; verify `Basic`/Classic's own DI
        registration actually wires this contributor up (`Persistence`
        discovers contributors via DI, per `docs/persistence.md`)
  - [ ] Dice-rolling abstraction over `IRandomSource` (DI-registered
        interface + implementation, not a static singleton — "N dice of M
        sides plus a modifier"); swap `CombatResolver`/`FleeCommand`'s raw
        `random.Next(...)` calls to use it
  - [ ] `csproj`: `Directory.Packages.props` entry, package metadata matching
        the existing `SharpMud.*` packages
- [ ] New `src/SharpMud.Ruleset.Basic` project
  - [ ] Minimal concrete stats behavior (plain numeric attributes, no
        Race/CharacterClass)
  - [ ] Default `IWorldBuilder` implementation (small, enough to walk around)
  - [ ] `AddSharpMudBasicRuleset(...)` DI extension, options callback for the
        tunable numbers (starting HP, etc.)
- [ ] Rebuild `samples/SharpMud.Samples.Classic`
  - [ ] Remove the now-extracted types; reference `SharpMud.Ruleset.Rpg`
        instead
  - [ ] Verify `StatsBehavior`/`Race`/`CharacterClass`/`HubWorldBuilder`
        still compose correctly against the new decoupling mechanism
- [ ] Tests
  - [ ] `SharpMud.Ruleset.Rpg.Tests` — mirrors today's Classic combat test
        coverage, moved and adjusted for the decoupling change
  - [ ] `SharpMud.Ruleset.Basic.Tests` — new coverage for the minimal
        concrete ruleset
  - [ ] `SharpMud.Samples.Classic.Tests` — updated for the new dependency
        shape, same behavioral coverage as before
- [ ] Docs
  - [ ] Update `engine-vs-ruleset.md`'s project-structure listing to reflect
        the new packages (currently only has ADR-0008's forward-reference)
  - [ ] Update `combat.md`/`character.md` subsystem docs if their described
        "current state" changes
  - [ ] `docs/adr/README.md`: once implementation is complete and matches
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
  `SharpMud.Ruleset.Rpg` reference
- `tests/SharpMud.Samples.Classic.Tests/*`
- `Directory.Packages.props`, `SharpMud.slnx`
- `docs/engine-vs-ruleset.md`, `docs/combat.md`, `docs/character.md` (as
  needed), `docs/adr/README.md`

## Test plan

Unit coverage mirrors today's Classic combat tests, relocated to
`SharpMud.Ruleset.Rpg.Tests`: `CombatResolver` hit/miss/damage math,
`CombatManager` encounter lifecycle (start/end/linkdead-freeze/defeat
handling), `AttackCommand`/`FleeCommand` guard and success paths — same
`AutoFixture`/`NSubstitute` patterns already established
(`testing.md`). New coverage in `SharpMud.Ruleset.Basic.Tests` for the
minimal stat block and default world builder. `SharpMud.Samples.Classic.Tests`
should need no *behavioral* changes, only reference/namespace updates — if
it does need behavioral changes, that's a signal the extraction changed
Classic's actual behavior, not just its packaging. Add a persistence
round-trip test (save/load a `Thing` carrying `CombatantBehavior` through
`SharpMud.Persistence`) specifically to catch the TPH-mapping seam above
regressing — this is exactly the kind of gap that passes every unit test
but fails at actual save/load time.

## Verification

Manual smoke test per this repo's established pattern for anything
session/gameplay-facing (`testing.md`): run `samples/SharpMud.Samples.Classic`
end-to-end, confirm `kill`/`attack`/`flee` behave identically to before the
extraction. Separately, build a throwaway minimal `Program.cs` against just
`SharpMud.Ruleset.Basic` (+ `Engine`/`Hosting`/a persistence provider/a
transport) and confirm a fresh character can walk around and fight something
— this is the actual "few lines, runnable basic game" claim ADR-0008 makes,
and needs a real run, not just passing unit tests, to confirm.

## Open questions / blockers

- Exact decoupling mechanism for `CombatManager`'s `StatsBehavior` touches
  (game event vs. callback interface) — first task to resolve once this
  plan moves to `In Progress`; blocks everything downstream in
  `SharpMud.Ruleset.Rpg`.
- Package naming (`SharpMud.Ruleset.Rpg`/`SharpMud.Ruleset.Basic`) is a
  working name per ADR-0008 — confirm before publishing, cheap to change
  before first release, expensive after (lockstep versioning, per ADR-0006).
