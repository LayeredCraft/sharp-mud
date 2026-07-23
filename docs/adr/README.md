# Architecture Decision Records

This directory is the permanent, numbered record of every design decision
made in this repo — see `.agents/skills/engineering-workflow/references/design-decisions.md`
for the full process of *when* and *how* to write one. This file is just
the mechanics: numbering, status, and the index.

## Numbering and immutability

- Files are `NNNN-kebab-case-title.md`, zero-padded to 4 digits,
  sequential (`0001-...`, `0002-...`, ...). `0000-adr-template.md` is the
  template, not a real decision — the first real ADR is `0001`.
- An ADR is **immutable once its status leaves `Proposed`.** Don't edit an
  `Accepted` ADR's Decision/Context/Considered Options to reflect a change
  of mind later — write a new ADR that changes `Status` to `Superseded by
  ADR-XXXX` on the old one and references it from the new one's Context or
  Links section. The old ADR stays exactly as it was when accepted; that's
  the point of it being a record, not a living doc.
- Copy `0000-adr-template.md` for every new ADR rather than writing one
  from a blank file, so the section shape stays consistent across every
  decision in this repo.

## Status lifecycle

`Proposed` → `Accepted` → (`Deprecated` | `Superseded by ADR-XXXX`)

- **Proposed**: still being discussed — the deep-dive brainstorm phase
  described in `design-decisions.md` produces a `Proposed` ADR before
  anything gets built against it.
- **Accepted**: the decision this repo is actually operating under. Code
  should match an `Accepted` ADR; if it doesn't, that's drift — fix the
  code or supersede the ADR, the same "code and docs disagree" rule that
  governs the rest of this repo's documentation.
- **Deprecated**: no longer the guidance, but nothing formally replaced it
  (rare — most decisions that stop applying get superseded by whatever
  replaced them instead).
- **Superseded by ADR-XXXX**: a later ADR explicitly replaced this one.
  Follow the chain forward to the current answer rather than trusting a
  superseded ADR's Decision section.

## How this relates to subsystem docs and research notes

- **Subsystem docs (`docs/*.md`)** describe *current state* — what a
  subsystem does today. When a subsystem doc describes a decision, it
  should link to the ADR that made it rather than re-deriving the
  rationale inline. Existing subsystem docs (`engine-vs-ruleset.md`,
  `accounts-auth.md`, etc.) predate this ADR system and still carry
  decisions inline — that's tracked debt, not a pattern to copy forward;
  backfill a link opportunistically when you're already touching that
  section, don't do it as a dedicated sweep.
- **`docs/research/*.md`** captures external research (e.g.
  `wheelmud-findings.md`) — what was looked at and why. Its `## Decisions`
  section should point at the ADR(s) the research fed into, rather than
  being the only record of what was decided.
- **`SPEC.md`** stays the vision/intent document; an ADR that changes
  product-level direction gets referenced from `SPEC.md`, not duplicated
  into it.

## Index

| ADR | Title | Status |
|---|---|---|
| [0001](0001-wheelmud-reconciliation-roadmap.md) | WheelMUD Reconciliation Roadmap | Accepted |
| [0002](0002-telnet-protocol-negotiation.md) | Telnet Protocol Negotiation (IAC/Q-Method core + NAWS) | Accepted |
| [0003](0003-allow-appsettingsjson-for-non-secret-config.md) | Allow `appsettings.json` for Non-Secret Configuration | Accepted |
| [0004](0004-session-state-machine-and-reconnect.md) | Session State Machine + Linkdead Reconnect | Accepted |
| [0005](0005-security-role-model-and-moderation-commands.md) | Security Role Model + Moderation Commands | Accepted |
| [0006](0006-nuget-package-distribution.md) | NuGet Package Distribution + Sample-Based Ruleset Extraction | Accepted |
| [0007](0007-narrow-meta-package-scope.md) | Narrow the `SharpMud` Meta-Package to Engine + Hosting + Persistence | Accepted |
| [0008](0008-ruleset-scaffolding-tier.md) | A Reusable RPG Scaffolding Tier Between `Engine` and Concrete Rulesets | Accepted |
