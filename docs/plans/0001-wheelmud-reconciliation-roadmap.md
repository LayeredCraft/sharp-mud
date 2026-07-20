# [PLAN-0001] WheelMUD Reconciliation Roadmap

**Implements:** [ADR-0001](../adr/0001-wheelmud-reconciliation-roadmap.md)

**Status:** In Progress

**Last updated:** 2026-07-17

## Goal

Work through every remaining WheelMUD-vs-sharp-mud gap identified in
ADR-0001, in priority order, each via its own research → design dive →
ADR → plan → implement cycle. "Done" for *this* plan means every slice
below has its own `Accepted` ADR and a `Done` plan (or an explicit
decision that no further work is needed).

## Scope

This plan tracks the roadmap **sequence**, not the detailed execution of
each slice — each slice gets its own plan once its own ADR exists. This
doc's job is to be the one place that shows, at a glance, where the whole
reconciliation effort stands.

## Tasks

- [x] **Slice 1 — Telnet protocol negotiation.** ADR-0002 Accepted,
      PLAN-0002 Done. See [PLAN-0002](0002-telnet-protocol-negotiation.md).
- [x] **Slice 2 — Session/connection state machine + reconnect.** ADR-0004
      Accepted, PLAN-0004 Done. See
      [PLAN-0004](0004-session-state-machine-and-reconnect.md).
- [ ] **Slice 3 — Permission/security-role model + moderation commands.**
      ADR-0005 Accepted, PLAN-0005 Not Started. See
      [PLAN-0005](0005-security-role-model-and-moderation-commands.md).
- [ ] **Slice 4 — World-building/OLC command surface.** Not yet designed;
      bundles with Slice 3.
- [ ] **Slice 5 — Help system.** Not yet designed.
- [ ] **Slice 6 — Player configuration commands.** Not yet designed.
- [ ] **Slice 7 — Commerce/shops.** Not yet designed.
- [ ] **Slice 8 — Plugin/extensibility for a second ruleset.** Placeholder
      only; no action until a real second ruleset is being built.
- [ ] **Slice 9 — Procedural frontier generation.** Not yet designed.
- [ ] **WheelMUD's FTP server: rejected, recorded in ADR-0001.** No
      further action — checking this off just means the decision itself
      (not implementation) is settled once ADR-0001 is `Accepted`.

## Critical files

This plan doesn't itself touch code — it tracks other plans/ADRs. No
files beyond `docs/adr/*.md`/`docs/plans/*.md` as each slice progresses.

## Test plan

N/A at this level — each slice's own plan carries its test plan.

## Verification

This plan is "on track" if every slice above either has a linked
`Accepted` ADR + `Done` plan, or an explicit recorded decision that it's
out of scope (like the FTP server). Review this doc whenever picking up
the next slice, to confirm the priority order still makes sense before
starting fresh research on it.
