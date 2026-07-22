# [ADR-0008] A Reusable RPG Scaffolding Tier Between `Engine` and Concrete Rulesets

**Status:** Proposed

**Date:** 2026-07-21

**Decision Makers:** Nick Cipollina

## Context

[ADR-0006](0006-nuget-package-distribution.md) shipped `SharpMud.Engine`/
`SharpMud.Hosting`/`SharpMud.Persistence`(`.Sqlite`/`.DynamoDb`)/
`SharpMud.Adapters.*` as real NuGet packages, and deliberately kept ruleset
logic (stats, combat, races/classes) unpackaged, living entirely in
`samples/SharpMud.Samples.Classic` as a reference implementation a consumer
reads and reimplements from scratch in their own repo.

In practice, that reference implementation is doing more work than a
consumer should have to redo. Reading the actual code in
`samples/SharpMud.Samples.Classic` (not just its file list) shows two very
different kinds of content sitting in one project:

- **Genuinely flavor-specific content**: `Race`/`CharacterClass` (D&D-style
  enum values via `LayeredCraft.OptimizedEnums`), `StatsBehavior` (the six
  D&D attributes + mana/stamina/level/XP), `HubWorldBuilder` (hand-built
  world content), `Program.cs` (the composition root).
- **Generic-shaped RPG scaffolding that already has no coupling to the
  flavor-specific content above**: `CombatantBehavior` (HP/`ArmorClass`/
  damage range/XP reward — a `Behavior` any `Thing` can carry, already
  documented as "independent of `StatsBehavior` so an NPC doesn't need a
  full character sheet just to throw a punch"), `ICombatResolver`/
  `CombatResolver` (d20-vs-AC resolution, reads only `CombatantBehavior`),
  `ICombatManager`/`CombatManager` (tick-driven encounter tracking),
  `AttackCommand`/`FleeCommand`. Verified directly against the current
  source: none of these extracted types reference `Race`, `CharacterClass`,
  or any D&D-specific attribute. `CombatManager`'s only two touches of
  `StatsBehavior` (awarding XP on a kill, applying the death-penalty XP/HP
  cut) are already `FindBehavior<StatsBehavior>()` calls guarded by
  `if (stats is not null)` — i.e. the existing code already treats
  `StatsBehavior` as an optional companion, not a hard dependency.

That second group is the actual "core game logic" complaint: it's reusable
in shape, but stuck unpackaged in one specific flavor's sample, so a
consumer building any RPG-shaped ruleset — not just a D&D reskin —
re-derives combat/encounter-tracking/attack-command wiring from scratch.

A research pass against WheelMUD (the prior art this whole project is
modeled on, see [wheelmud-findings.md](../research/wheelmud-findings.md))
found no precedent for a three-tier split on the *rules* dimension:
`WarriorRogueMage` is WheelMUD's only ruleset, ever, and its stats/skills/
combat are one monolithic assembly with no further split — WheelMUD never
had a second ruleset to force that question. (WheelMUD does keep a real,
validated split between `Core` and `WheelMUD.Universe`, but `Universe`
turned out — checked directly against its actual file list and `.csproj`
references — to be a library of generic, ruleset-agnostic *item-type*
behaviors, `WeaponBehavior`/`PotionBehavior`/`ContainerBehavior`/etc., not
world content as this repo's own findings doc summary previously implied —
corrected in the same change as this ADR. `Universe` references only
`Core`/`Data`/`Effects`, never `WarriorRogueMage`, and vice versa — two
independent siblings composed together only at `Main`. Sharp-mud already
made this exact call at the `Engine` tier directly, via `WearableBehavior`/
`EquippedBehavior`, rather than needing a separate package for it.)

One more piece of WheelMUD prior art is directly relevant here and *is*
worth adopting, unlike the three-tier rules split: `WheelMUD.Core.DiceService`
(a `Random`-backed dice roller — `DiceService.Instance.GetDie(sides).Roll()`)
lives in WheelMUD's engine tier, not `WarriorRogueMage` — WheelMUD treats
"roll dice" as an engine-level concern, distinct from any ruleset's specific
rules. It's a static singleton (the exact anti-pattern this repo's own
research doc already rejected — see `wheelmud-findings.md` §10), has no
multi-die/modifier notation (`2d6+3`), and isn't an interface despite the
name suggesting one. sharp-mud already has the DI-friendly primitive this
should build on — `IRandomSource`, already constructor-injected into
`CombatResolver`/`FleeCommand` today — but nothing sits on top of it for
"roll N dice of M sides plus a modifier"; call sites just do raw
`random.Next(min, max)`. A small dice-rolling abstraction over `IRandomSource`
belongs in `SharpMud.Ruleset.Rpg` (generic RPG mechanic, not bare randomness,
so not `Engine`; not ruleset-flavor-specific, so not `Basic`/Classic) — see
Decision Outcome.

So this ADR is genuinely new ground for this project, not an adoption of
already-proven prior art, and is scoped narrowly to the rules dimension:
should there be a packaged tier between `SharpMud.Engine` and a concrete
ruleset, specifically for RPG-shaped scaffolding (stats/combat/leveling),
the way `Microsoft.EntityFrameworkCore.Relational` sits between
`Microsoft.EntityFrameworkCore` and a concrete provider (`.Sqlite`/
`.SqlServer`/...)?

## Decision Drivers

- Primary goal (stated directly): make the engine genuinely easy to extend
  for someone building *their own* ruleset — not just technically possible
  via raw `Thing`/`Behavior` primitives, but with real combat/encounter
  scaffolding available off the shelf.
- Secondary, related goal: a true "`dotnet add package`, a few lines in
  `Program.cs`, run a basic game" quick-start — the same shape as EF Core's
  `UseSqlite(...)` one-liner. Note this requires a concrete, runnable leaf
  package, not the abstract scaffolding tier alone — `Relational` isn't a
  database either.
- Don't design the scaffolding boundary against a single consumer. WheelMUD
  never had a second ruleset to validate `WarriorRogueMage`'s shape against;
  this ADR should have at least two (a new minimal package plus the
  existing Classic sample) before the contracts are treated as settled.
- Don't turn game-balance-sensitive content (dice formulas, race/class
  stat blocks, hand-built world content) into a public SemVer compatibility
  promise — that content changes far more often than infrastructure code,
  and `SharpMud.Persistence`'s "public API surface is a real compatibility
  promise" cost (ADR-0006's Negative Consequences) shouldn't extend to
  content that isn't infrastructure.
- Reuse the existing low extraction cost where it already exists —
  `CombatantBehavior`/`ICombatResolver`/`ICombatManager`/`AttackCommand`/
  `FleeCommand` are already flavor-agnostic in practice; this is largely a
  move, not a rewrite, modulo the `StatsBehavior` XP/death-penalty seam.
- Standing repo rules still apply: composition over inheritance (new
  scaffolding types are `Behavior`s, not a class hierarchy),
  `SharpMud.Engine` never references ruleset-specific types, DI-only wiring,
  no MEF/dynamic loading.

## Considered Options

1. **Status quo** — ruleset logic stays entirely unpackaged in
   `samples/SharpMud.Samples.Classic`, per ADR-0006 as originally written.
2. **Promote the generic-shaped pieces directly into `SharpMud.Engine`**,
   the same way `WanderingBehavior` earned its way in (it only needed
   `NpcBehavior`/`ExitBehavior`, no ruleset data).
3. **Package Classic itself as an installable starter/composite product**
   (`SharpMud.Ruleset.Classic` or similar) — a consumer `dotnet add
   package`s the actual D&D-flavored game and overrides pieces of it.
4. **A dedicated scaffolding package (`SharpMud.Ruleset.Rpg`) between
   `Engine` and concrete rulesets, plus a new minimal concrete leaf package
   (`SharpMud.Ruleset.Basic`) built on it, with Classic remaining an
   unpackaged sample rebuilt on the same scaffolding** (chosen).

## Decision Outcome

Chosen option: **4**. Three real tiers:

```
SharpMud.Engine            unchanged — Thing/Behavior/events, zero ruleset knowledge

SharpMud.Ruleset.Rpg       NEW, packaged — CombatantBehavior, ICombatResolver/CombatResolver,
                           ICombatManager/CombatManager, AttackCommand/FleeCommand, moved
                           in from samples/SharpMud.Samples.Classic (see Context — these
                           these extracted types' actual combat logic has no flavor-specific coupling
                           today, but CombatManager itself carries two seams that must be
                           decoupled before the move, not moved as-is):
                           - XP-award/death-penalty touches into StatsBehavior — likely via
                             a combat-outcome game event through the existing ThingEvents
                             pipeline (Thing.Add/Remove already use this pattern for
                             cancellable events; exact mechanism is a plan-level decision).
                           - CombatManager's constructor takes a concrete `Thing hubRoom` and
                             hard-codes it as the respawn destination on defeat — a
                             Classic-specific world/respawn policy, not generic combat logic.
                             Needs the same kind of seam (plausibly the same combat-outcome
                             event above also owning "where does the loser respawn," rather
                             than a second bespoke mechanism — exact shape is a plan-level
                             decision) so Basic/a custom ruleset aren't forced to have a
                             "hub room" concept at all.
                           Once both seams are decoupled, this package has zero reference to
                           any concrete ruleset's stat/race/class/world-content types. Also
                           gains a small dice-rolling abstraction over the existing
                           Engine-level IRandomSource (DI-registered, not a WheelMUD-style
                           static singleton — see Context) covering the "N dice of M sides
                           plus a modifier" shape CombatResolver/FleeCommand currently do
                           with raw random.Next(...) calls. Ships its own DI registration
                           entry point (an AddSharpMudRpgRuleset(...)-shaped extension)
                           reproducing today's manual Program.cs wiring (ICombatResolver,
                           ICombatManager registered as both itself and ITickable off the
                           same instance, the mapping contributor, the dice service) — Basic,
                           Classic, and a custom consumer all call this instead of each
                           hand-rolling the same scaffolding verbatim. This entry point must
                           also register AttackCommand/FleeCommand in a way that composes
                           with Basic/Classic/a consumer's own commands, not replace them --
                           Hosting's AddSharpMudRuleset(...) takes a single callback, so two
                           independent calls (one for Rpg's commands, one for the consumer's)
                           would silently clobber each other via DI's last-registration-wins
                           resolution. Command registration is a real part of this package's
                           public contract, not just a plan-level implementation detail; exact
                           mechanism (forwarding callback vs. additive registry) is left to the
                           plan. Not runnable/playable on its own, same as
                           Microsoft.EntityFrameworkCore.Relational isn't a database.
                           **Persistence mapping moves with it**: `CombatantBehavior`'s EF
                           Core configuration (today `Configurations/
                           CombatantBehaviorConfiguration.cs`, applied via
                           `ClassicBehaviorMappingContributor.ConfigureBehaviors`'s
                           `ApplyConfigurationsFromAssembly(typeof(ClassicBehaviorMappingContributor).Assembly)`
                           — an assembly-scan scoped to the Classic assembly only, per
                           `docs/persistence.md`'s `IBehaviorMappingContributor` seam) has to
                           move to an `SharpMud.Ruleset.Rpg`-owned
                           `IBehaviorMappingContributor` once `CombatantBehavior` lives in a
                           different assembly, or a saved world containing `CombatantBehavior`
                           hits an unmapped TPH discriminator subtype the moment Classic stops
                           owning that configuration. **This is a real package-boundary
                           tradeoff, not a free move**: `IBehaviorMappingContributor` is
                           defined in `SharpMud.Persistence`, so `SharpMud.Ruleset.Rpg` takes a
                           dependency on `SharpMud.Persistence` to implement it — meaning a
                           consumer who wants Rpg's combat scaffolding purely in-memory, with
                           no EF Core involved at all, still pulls in `Persistence`. Accepted
                           here rather than splitting persistence mapping into a separate
                           companion package, since no consumer has asked for persistence-free
                           combat scaffolding and speculatively splitting for a hypothetical
                           one repeats the mistake this ADR's Considered Options already
                           reasoned against elsewhere — revisit if that actually comes up.

SharpMud.Ruleset.Basic     NEW, packaged, deliberately minimal — a concrete flavor built on
                           SharpMud.Ruleset.Rpg: a plain numeric stat block (no Race/
                           CharacterClass), simple d20-ish combat via the scaffolding's
                           resolver, a tiny default IWorldBuilder (a room or two, including
                           at least one Thing with CombatantBehavior as a fightable NPC —
                           the Goal above promises a fresh character can walk around and
                           fight something, which empty rooms alone don't satisfy), and its
                           own IPlayerFactory (mirrors ClassicPlayerFactory) so Hosting's
                           PlayerLogin/LoginFlow can actually create a fresh CLI/Telnet
                           player — without it the quick-start fails at first login, not
                           just "nothing to see." This is the actual "dotnet add package +
                           a few Program.cs lines = a playable game" experience, analogous
                           to UseSqlite(...).

samples/SharpMud.Samples.Classic   Stays a sample, unpackaged. Rebuilt on SharpMud.Ruleset.Rpg
                                   instead of owning CombatantBehavior/CombatResolver/etc.
                                   directly — keeps its own StatsBehavior/Race/CharacterClass/
                                   HubWorldBuilder as flavor-specific content demonstrating a
                                   richer game than Basic, on the same scaffolding.
```

Package naming follows ADR-0006's convention (no `LayeredCraft.` prefix,
package ID matches project/assembly name 1:1) — `SharpMud.Ruleset.Rpg` and
`SharpMud.Ruleset.Basic` are working names, open to bikeshedding at
implementation time, not a load-bearing part of this decision.

`Ruleset.Basic` and Classic together give the `Rpg` scaffolding's contracts
(`ICombatResolver` chiefly) two genuinely different concrete consumers to
validate against before the package ships — Classic's dice-vs-AC combat and
Basic's own (deliberately similarly-shaped, since it's meant to be simple)
combat, both exercising the same `ICombatResolver` contract, is the closest
this repo can get to WheelMUD-style validation without inventing a third,
speculative ruleset just to prove a point.

### Positive Consequences

- Directly answers the original complaint: a consumer building their own
  ruleset gets working combat/encounter-tracking/attack-command scaffolding
  for free instead of re-deriving it, the same value a `Behavior`-composition
  model already provides for engine-level concerns.
- A genuine NuGet quick-start becomes possible (`Ruleset.Basic`), matching
  the "few lines in `Program.cs`, run a basic game" goal directly, without
  turning content that changes often (Classic's dice formulas, race/class
  values, hub-world content) into a versioned public API.
- Extraction cost is lower than a fresh design would suggest — most of the
  scaffolding tier's core types are already flavor-agnostic in the existing
  codebase. Real design/implementation work remains, though, and shouldn't
  be minimized: decoupling `CombatManager`'s `StatsBehavior` touches and its
  hard-coded `hubRoom` respawn destination, moving `CombatantBehavior`'s EF
  Core mapping into a `Ruleset.Rpg`-owned `IBehaviorMappingContributor`, and
  building the new dice-rolling abstraction over `IRandomSource` are all real
  work this ADR commits to, not incidental cleanup.
- Two real consumers (`Basic`, Classic) exercise `Rpg`'s contracts before
  they're locked in, rather than one.

### Negative Consequences

- Two new packages added to the already-lockstep-versioned set from
  ADR-0006/0007 — more public API surface to maintain, and an open question
  (deliberately not settled here — see Open Items below) about whether
  either belongs in the `SharpMud` meta-package, which ADR-0007 already
  narrowed once over unwanted transitive dependencies.
- `SharpMud.Ruleset.Rpg`'s contracts (`ICombatResolver`, whatever mechanism
  replaces the direct `StatsBehavior` touches) become a real compatibility
  promise the moment they're published — the same category of cost ADR-0006
  already flagged for `SharpMud.Persistence`, now extended to a third
  conceptual layer.
- `Ruleset.Basic`, however minimal, is still a concrete published ruleset —
  its default stat numbers/combat math are a smaller but real compatibility
  promise, the same category of cost Option 3 was rejected for, just
  deliberately kept small by being genuinely minimal.
- This is genuinely new territory beyond WheelMUD's validated prior art —
  even with two consumers instead of one, the risk that `Rpg`'s boundary is
  drawn in the wrong place is real, just lower than designing against Classic
  alone.
- `SharpMud.Ruleset.Rpg` taking on a `SharpMud.Persistence` dependency (see
  Decision Outcome/Open Items) means there's no persistence-free path to
  Rpg's combat scaffolding — a consumer who wants in-memory-only combat, no
  EF Core involved at all, still pulls in `Persistence` transitively.
  Accepted for now since no consumer has asked for that, but a real
  consumer-facing cost, not a free extraction.

## Pros and Cons of the Options

### Option 1 — Status quo (sample-only)

- Good, because it's zero new packages, zero new public API surface, and
  matches WheelMUD's own actual (never-needed-a-split) precedent exactly.
- Bad, because it leaves the actual, present complaint unaddressed — the
  sample is genuinely unpleasant to build a different ruleset on top of
  today, not just hypothetically.

### Option 2 — Promote generic pieces directly into `SharpMud.Engine`

- Good, because it needs no new package/project, reusing the `Engine`
  distribution channel that already exists.
- Bad, because `engine-vs-ruleset.md`'s "zero ruleset knowledge" line for
  `Engine` has been clean so far (`WanderingBehavior`'s promotion only
  needed `NpcBehavior`/`ExitBehavior` — no combat, no stats, no leveling
  concept at all); `CombatantBehavior`/`ICombatResolver` assume "this is an
  RPG with hit points and damage," which is a materially bigger concession
  than anything `Engine` has absorbed before, for consumers who might want
  a MUD with no combat system whatsoever.

### Option 3 — Package Classic itself as an installable starter product

- Good, because it's the simplest possible "install and get exactly this
  game" experience — no new abstraction to design at all.
- Bad, because it turns genuinely volatile content (dice formulas,
  race/class stat blocks, hand-built world content — the things most likely
  to change release-to-release) into a public SemVer compatibility promise.
- Bad, because it only serves a consumer who wants *this specific* D&D-ish
  game — it does nothing for the stated primary goal of making it easy to
  build a *different* ruleset on the engine; that consumer skips the
  package entirely and is back to the Option 1 experience.

### Option 4 — Scaffolding tier + minimal concrete leaf + Classic as sample (chosen)

See Decision Outcome above.

- Good, because it's the only option that serves both stated goals (easy to
  extend the engine for a custom ruleset, and a genuine one-line quick-start)
  without also publishing volatile game-balance content as a compatibility
  promise.
- Good, because the extraction cost is verified low against the actual
  current code, not assumed.
- Bad, because it's the most new package surface area of any option (two
  new packages, not zero or one), and the newest, least-precedented design
  decision in this project's history — no external prior art validates the
  three-tier shape specifically, only the general "shared scaffolding under
  multiple concrete leaves" idea from EF Core's own ecosystem.

## Links

- [engine-vs-ruleset.md](../engine-vs-ruleset.md) — the `Engine`/ruleset
  split this ADR adds a tier to, not replaces
- [ADR-0006](0006-nuget-package-distribution.md) — this ADR refines
  ADR-0006's "ruleset logic lives in `samples/`, not a package" framing by
  carving out a packaged scaffolding + minimal-leaf tier specifically for
  reusable RPG shape, while keeping concrete flavor content (Classic)
  unpackaged; ADR-0006's granular-package/meta-package/`Hosting` mechanics
  are otherwise unchanged and this does not supersede it
- [ADR-0007](0007-narrow-meta-package-scope.md) — the meta-package scoping
  precedent this ADR's Open Items below will need to weigh
  `Ruleset.Rpg`/`Ruleset.Basic` against
- [wheelmud-findings.md](../research/wheelmud-findings.md) — prior art
  reviewed for this decision; corrected in the same change (its `Universe`
  description, and a new Decisions entry for `DiceService`/`Die` — adopted
  in spirit as DI-based dice rolling on top of `IRandomSource`, not adopted
  as a static singleton)

## Open Items

- Whether `SharpMud.Ruleset.Rpg`/`SharpMud.Ruleset.Basic` belong in the
  `SharpMud` meta-package (narrowed by ADR-0007 to Engine + Hosting +
  Persistence) is not decided here — a consumer who wants a *different*
  ruleset shape shouldn't get RPG-specific scaffolding pulled in by default,
  echoing ADR-0007's own reasoning almost exactly. Likely answer is "no,"
  but that's a decision for whoever implements this, not asserted here.
- The exact mechanism for decoupling `CombatManager`'s XP-award/death-penalty
  logic from `StatsBehavior` (a game event through `ThingEvents`, a small
  callback interface, or something else) is left to the implementation plan,
  not fixed by this ADR.
- The exact mechanism for decoupling `CombatManager`'s hard-coded `hubRoom`
  respawn destination — just as blocking as the `StatsBehavior` seam above
  for moving `CombatManager` into a generic package, and plausibly the same
  mechanism, but left to the implementation plan, not fixed by this ADR.
- The exact mechanism for composing `AttackCommand`/`FleeCommand`
  registration with a consumer's own commands (a forwarding callback through
  `AddSharpMudRpgRuleset(...)`, or an additive `ICommandRegistry`) — command
  registration is part of `Ruleset.Rpg`'s public contract per the Decision
  Outcome above, just as blocking as the other two seams, and left to the
  implementation plan, not fixed by this ADR.
- `SharpMud.Ruleset.Rpg` taking a dependency on `SharpMud.Persistence` (to
  implement `IBehaviorMappingContributor` for `CombatantBehavior`) means
  there's no persistence-free path to Rpg's combat scaffolding — accepted
  for now (see Decision Outcome) since no consumer has asked for one;
  revisit with a companion persistence-mapping package if that changes.
