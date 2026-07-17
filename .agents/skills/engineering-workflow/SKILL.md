---
name: engineering-workflow
description: >
  One-stop engineering workflow and standards reference for the sharp-mud
  repo. Make sure to use this skill for ANY non-trivial work on this
  codebase, even if the user doesn't name the skill or say "engineering
  workflow" explicitly: designing new engine/ruleset features (including
  open-ended architecture/design dives with unclear scope, not just quick
  decisions), writing or reviewing C# code, deciding where a decision or
  doc belongs, touching persistence/EF Core, handling auth/secrets/input
  from untrusted sources (Telnet), writing tests, or writing any
  documentation (docs/, SPEC.md, README, CONTRIBUTING, commit messages).
  Always consult this skill before proposing an architecture change, before
  writing new C# files, and before adding a new coding pattern not already
  established in the repo — even a request that just looks like "add a
  quick feature" or "fix this bug" should route through it first. Also
  covers reviewing a PR/diff in this repo and responding to feedback left
  on one (`tasks/` procedures, not just reference lookup) — see How to use
  this skill below. Trigger on: "how should I structure this", "coding
  standards", "is this secure", "how do we handle X in the database",
  "where does this belong", "contributing", "code of conduct", "review
  this PR", "review my changes", "address that feedback", "handle the
  review comments", or any request to add a feature to SharpMud.Engine /
  SharpMud.Ruleset.Classic / SharpMud.Persistence / SharpMud.Host /
  SharpMud.Adapters.*.
---

# sharp-mud Engineering Workflow

This skill is the source of truth for **how** work gets done in this repo —
process and standards. It is not a restatement of current architecture;
`docs/` and `SPEC.md` hold that, and this skill tells you when and how to
update them. If code and this skill disagree, the code wins and this skill
is stale — fix the skill in the same PR you notice the drift.

Repo shape: `src/SharpMud.Engine` (ruleset-agnostic engine), `src/SharpMud.Ruleset.Classic`
(D&D-flavored ruleset built on Engine, zero back-references), `src/SharpMud.Persistence`
(EF Core), `src/SharpMud.Host` (composition root, DI, Program.cs), `src/SharpMud.Adapters.Telnet`
and `src/SharpMud.Adapters.Cli` (transport). Tests mirror 1:1 under `tests/`.

## How to use this skill

Two kinds of file live here, and they don't overlap:

- **`references/`** — topic knowledge. What's true about this repo's
  standards for a given topic (coding style, testing, persistence,
  security, docs, decision-making, conduct). No procedures, no step
  ordering — just the rules and the reasoning, one file per topic, so you
  only load what the current work actually needs.
- **`tasks/`** — procedures for a specific *kind of request* ("review this
  PR"). A task file doesn't restate reference content; it says which
  references to load for that request and what order to do things in. Add
  a new task file when a recurring request needs its own procedure, not
  when it just needs a reference lookup — most requests still route
  through the reference table below, not a task.

Read the file(s) that match the work in front of you *before* writing
code, docs, review output, or design output — don't guess from a section
title alone, the detail (specific patterns, rejected alternatives, naming)
lives in the file itself.

### Tasks

| When you're asked to... | Run |
|---|---|
| Review a PR/diff in this repo | `tasks/pr-review.md` |
| Address/respond to feedback already posted on a PR | `tasks/respond-to-pr-feedback.md` |

### References

| When you're about to... | Read |
|---|---|
| Decide where an architecture/feature decision belongs, run a design dive (light or deep) before writing code, write/reference an ADR (`docs/adr/`), or write/track a plan (`docs/plans/`) | `references/design-decisions.md` |
| Write or review any C# (naming, nullable, async, DI, error handling, file layout) | `references/coding-standards.md` |
| Add or change tests | `references/testing.md` |
| Touch EF Core, add a `Behavior`, or change anything under `SharpMud.Persistence` | `references/persistence.md` |
| Handle auth, secrets, config, or anything from the Telnet (untrusted) input surface | `references/security.md` |
| Write or update `docs/*.md`, `SPEC.md`, code comments, or commit messages | `references/documentation.md` |
| Review someone's work, or handle a disagreement about direction | `references/code-of-conduct.md` |
| Onboard a change end-to-end, or you're not sure what's expected of a PR | `references/contributing.md` |
| Post a comment or verdict on a PR review | `references/review-emoji-legend.md` |

Most non-trivial tasks touch more than one of these — e.g. adding a new
`Behavior` typically means reading `design-decisions.md` (where does this
decision live, is it a light or deep dive), `coding-standards.md` (naming,
DI), `persistence.md` (config class, no new table), and `documentation.md`
(update the subsystem doc in the same PR). Read all of the ones that apply,
not just the first match.
