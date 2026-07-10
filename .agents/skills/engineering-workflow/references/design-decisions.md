# Design decisions

Before designing anything — even something that sounds like a from-scratch
feature — check whether it already exists. This codebase has grown behaviors
and managers incrementally, and a request phrased as "I want to add X" is
often actually "X already exists, here's where." Grep for the likely type
name (`Behavior`, `Manager`, `Command`) and skim the relevant `docs/*.md`
before proposing a design — a design session grounded in "here's what's
already there" is more useful than one that re-derives a solved problem from
first principles, and it's a fast check relative to the cost of designing
around a wrong assumption.

Before writing code for anything bigger than a bugfix, decide where the
decision lives:

- **`SPEC.md`** — product-level decisions: what sharp-mud *is*, world-gen
  philosophy, transport strategy, deployment target. Rare to touch.
- **`docs/*.md`** — one file per subsystem (`architecture.md`,
  `persistence.md`, `accounts-auth.md`, `engine-vs-ruleset.md`,
  `networking.md`, `character.md`, `combat.md`, `commands.md`,
  `world-model.md`, `deployment.md`). Update the relevant doc **in the same
  PR** that changes the behavior it describes — a doc that lags the code by
  even one PR starts a rot that's expensive to reverse.
- **`docs/research/*.md`** — external research (e.g. `wheelmud-findings.md`)
  and the "Decisions" reached from it. New research goes here; don't mix
  research notes into the subsystem docs above.
- **This skill** — process/standards only, not subsystem behavior.

Rules for design work:

1. State the decision and the *rejected alternatives* with one-line reasons
   — future readers need to know what was considered, not just what won
   (see `engine-vs-ruleset.md` and `wheelmud-findings.md` §Decisions for the
   model to follow).
2. Prefer composition over inheritance for game objects: every game object
   is a `Thing` differentiated by attached `Behavior`s, never a subclass.
   Don't add a new `Thing` subtype or a Player/Room/Npc/Item class hierarchy
   — add a `Behavior`.
3. `SharpMud.Engine` must never reference `SharpMud.Ruleset.Classic` or any
   other ruleset. If you're tempted to add that reference, the code you're
   writing belongs in the ruleset project, not the engine.
4. No MEF, no static `XManager.Instance` singletons. DI is
   constructor-injected via `Microsoft.Extensions.DependencyInjection`,
   wired in `src/SharpMud.Host/Program.cs`.
5. When adopting a pattern from an external reference (e.g. WheelMUD),
   record it under `docs/research/` with what was adopted, what was
   deliberately changed, and why — don't silently copy a pattern without
   that trail.

## Design dives

Not every design decision is the same size. Match the process to how settled
the problem is:

- **Light** — the problem and the shape of the solution are already clear
  (e.g. "add a Behavior for X, following the pattern Y already uses"). Skip
  straight to writing the decision record below; a brainstorm phase would
  just be ceremony.
- **Deep** — the problem is ambiguous, touches multiple subsystems, or has
  more than one plausible shape (e.g. "how should NPC AI scheduling work,"
  "do we need a separate world-persistence tier"). Start with a brainstorm:
  restate the problem in your own words, surface the constraints that
  actually bind (existing `Behavior` composition model, the no-Ruleset-
  reference rule above, DI-only wiring, no migrations — see
  `persistence.md`), then generate at least two genuinely different
  alternatives — not a strawman and a preferred option. Talk it through with
  whoever's asking before committing to one; the point of this phase is to
  surface a disagreement *before* it's baked into code, not after.

Either tier ends the same way — a decision record in the right location per
the table above (`SPEC.md`, `docs/*.md`, or `docs/research/*.md`), following
the shape in `documentation.md`: current state, the decision, and the
rejected alternatives with one-line reasons each. A deep dive that never
produces this record hasn't actually finished — the brainstorm is only
useful if it leaves a trail the next reader can follow.
