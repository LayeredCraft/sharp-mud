# Plans

An ADR (`docs/adr/`) records a **decision** — what was decided and why,
immutable once accepted. A plan records **how that decision actually gets
built** — the task breakdown, the files touched, the test/verification
strategy, and live progress tracking. Conflating the two makes the ADR
churn (defeating the point of it being a permanent record) and makes the
plan hard to find (buried inside a decision doc). Keep them separate.

See `.agents/skills/engineering-workflow/references/design-decisions.md`'s
**Writing a Plan** section for the full process. This file is the
mechanics: numbering, status, when a plan is warranted, and the index.

## Numbering

- Files are `NNNN-kebab-case-title.md`, matching the number of the ADR
  they implement (`docs/plans/0002-telnet-protocol-negotiation.md`
  implements `docs/adr/0002-telnet-protocol-negotiation.md`). One ADR gets
  at most one plan — if the plan's scope needs to fork into genuinely
  separate efforts, that's a sign the ADR itself should have been two
  decisions, not one plan splitting into two.
- `0000-plan-template.md` is the template, not a real plan.

## When a plan is warranted

Not every ADR needs one. A light design dive whose `Decision Outcome` is
already a one-file, obvious change doesn't need a separate tracking
document — the ADR is enough. Write a plan when the implementation is
multi-file, will span more than one sitting, or has enough sequencing
that a checklist is genuinely useful to come back to. This mirrors the
light/deep design-dive distinction: light decisions often skip a plan
entirely, deep ones almost always warrant one.

## Status lifecycle

`Not Started` → `In Progress` → `Done` (or `Blocked`, if genuinely stuck
on something outside this plan's control — note what in the plan itself).

Unlike an ADR, **a plan is a living document.** Check tasks off as they're
completed, adjust the task list if reality diverges from what was
originally scoped (a plan being wrong about *how* is not the same as the
ADR being wrong about *what/why* — updating the how doesn't require
superseding anything). Once `Done`, a plan settles into a historical
record of how the work actually happened — still useful for a future
reader asking "how was this built," but no longer actively edited.

A plan can be drafted alongside a still-`Proposed` ADR — working out *how*
something would be built often surfaces questions about *what* was
decided, so designing them together is normal. What matters is
**execution**: a plan stays `Status: Not Started` until its ADR flips to
`Accepted`. Checking off tasks (moving to `In Progress`) against a
still-`Proposed` ADR means code is being written for a decision that
isn't settled yet — don't do that.

## Index

| Plan | Implements | Status |
|---|---|---|
| [0001](0001-wheelmud-reconciliation-roadmap.md) | [ADR-0001](../adr/0001-wheelmud-reconciliation-roadmap.md) | In Progress |
| [0002](0002-telnet-protocol-negotiation.md) | [ADR-0002](../adr/0002-telnet-protocol-negotiation.md) | Done |
| [0004](0004-session-state-machine-and-reconnect.md) | [ADR-0004](../adr/0004-session-state-machine-and-reconnect.md) | Done |
| [0005](0005-security-role-model-and-moderation-commands.md) | [ADR-0005](../adr/0005-security-role-model-and-moderation-commands.md) | In Progress |
| [0006](0006-nuget-package-distribution.md) | [ADR-0006](../adr/0006-nuget-package-distribution.md) | Done |
| [0008](0008-ruleset-scaffolding-tier.md) | [ADR-0008](../adr/0008-ruleset-scaffolding-tier.md) | Done |
