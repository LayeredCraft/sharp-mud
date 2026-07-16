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

## Where decisions live

Four places, each with a different job — don't blur them together:

- **`SPEC.md`** — product-level vision and intent: what sharp-mud *is*,
  world-gen philosophy, transport strategy, deployment target. Rare to
  touch. References the ADR that changed it rather than re-deriving the
  reasoning inline.
- **`docs/*.md`** — one file per subsystem (`architecture.md`,
  `persistence.md`, `accounts-auth.md`, `engine-vs-ruleset.md`,
  `networking.md`, `character.md`, `combat.md`, `commands.md`,
  `world-model.md`, `deployment.md`). Describes **current state** — what
  the subsystem does *today* — and links to the ADR(s) that shaped it,
  rather than carrying the decision's rationale itself. Update the
  relevant doc **in the same PR** that changes the behavior it describes —
  a doc that lags the code by even one PR starts a rot that's expensive to
  reverse.
- **`docs/adr/*.md`** — the actual decision record: numbered, permanent,
  immutable once accepted. This is where "what was decided and why, and
  what was rejected" actually lives now — see **Writing an ADR** below and
  `docs/adr/README.md` for the numbering/status mechanics.
- **`docs/research/*.md`** — external research (e.g. `wheelmud-findings.md`)
  that feeds into a decision. Its `## Decisions` section points at the ADR
  the research fed into; new research goes here, not mixed into subsystem
  docs or ADRs directly.

This skill (`.agents/skills/engineering-workflow/`) stays process/standards
only — it's where you learn *how* to make and record a decision, never
where a specific decision's outcome is recorded.

Rules for design work:

1. Every design decision gets an ADR — see **Writing an ADR** below. The
   old shape (state the decision and rejected alternatives with one-line
   reasons, inline in a subsystem doc) is what an ADR formalizes; don't
   write a new inline decision block in a subsystem doc going forward.
   `engine-vs-ruleset.md` and `wheelmud-findings.md`'s `## Decisions`
   section predate this system and are the *style* to carry forward, just
   now inside the ADR template rather than inline.
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
   deliberately changed, and why, and write the actual decision up as an
   ADR — don't silently copy a pattern without that trail.

## Design dives

Not every design decision is the same size. Match the *process* to how
settled the problem is — but both tiers end in an ADR, because a stable,
linkable identity for the decision is cheap and a light dive's ADR is just
a short one, not a skipped one:

- **Light** — the problem and the shape of the solution are already clear
  (e.g. "add a Behavior for X, following the pattern Y already uses").
  Skip straight to writing the ADR; most sections compress to a sentence
  or "N/A" (e.g. `Considered Options` might just be "the established
  pattern, and doing nothing" if there's no real alternative worth
  weighing) — a brainstorm phase would be ceremony here, but the record
  still gets written.
- **Deep** — the problem is ambiguous, touches multiple subsystems, or has
  more than one plausible shape (e.g. "how should NPC AI scheduling work,"
  "do we need a separate world-persistence tier"). Start with a brainstorm:
  restate the problem in your own words, surface the constraints that
  actually bind (existing `Behavior` composition model, the no-Ruleset-
  reference rule above, DI-only wiring, no migrations — see
  `persistence.md`), then generate at least two genuinely different
  alternatives — not a strawman and a preferred option. Talk it through
  with whoever's asking before committing to one — the point of this phase
  is to surface a disagreement *before* it's baked into code, not after —
  then write the ADR capturing that discussion.

## Writing an ADR

1. Copy `docs/adr/0000-adr-template.md` to
   `docs/adr/NNNN-kebab-case-title.md`, where `NNNN` is the next sequential
   number (check `docs/adr/README.md`'s index for the last one used).
2. Fill it in. For a deep dive, `Considered Options` and `Pros and Cons of
   the Options` should reflect the actual alternatives generated during
   the brainstorm — not be padded out after the fact to look thorough. For
   a light dive, it's fine for these sections to be brief.
3. Set `Status: Proposed` while it's still being discussed. Once settled,
   change it to `Accepted` — that status change *is* the record of the
   decision being made, don't also write a separate "decision" note
   elsewhere.
4. Add a row to the index table in `docs/adr/README.md`.
5. Update the relevant `docs/*.md` subsystem doc to reflect the resulting
   current state, linking back to the ADR rather than re-explaining the
   reasoning.
6. If this ADR changes direction from an earlier one, set the earlier
   ADR's `Status` to `Superseded by ADR-XXXX` — don't edit its Context/
   Decision/Options sections, an accepted ADR is a historical record, not
   a living doc (see `docs/adr/README.md`'s immutability rule).

A design dive — light or deep — that never produces an ADR hasn't actually
finished. The brainstorm (for a deep dive) or the quick judgment call (for
a light one) is only useful if it leaves a trail the next reader can
follow.
