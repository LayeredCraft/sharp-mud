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
- [x] **Slice 3 — Permission/security-role model + moderation commands.**
      ADR-0005 Accepted, PLAN-0005 Done. See
      [PLAN-0005](0005-security-role-model-and-moderation-commands.md).
- [x] **Slice 4 — World-building/OLC command surface.** ADR-0009
      Accepted, PLAN-0009 Done. See
      [PLAN-0009](0009-world-building-olc-command-surface.md).
- [ ] **Slice 5 — Help system.** Not yet designed.
- [ ] **Slice 6 — Player configuration commands.** Not yet designed.
- [ ] **Slice 7 — Commerce/shops.** Not yet designed.
- [ ] **Slice 8 — Plugin/extensibility for a second ruleset.** Placeholder
      only; no action until a real second ruleset is being built.
- [ ] **Slice 9 — Procedural frontier generation.** Not yet designed.
- [ ] **WheelMUD's FTP server: rejected, recorded in ADR-0001.** No
      further action — checking this off just means the decision itself
      (not implementation) is settled once ADR-0001 is `Accepted`.
- [ ] **Slice 10 (not yet numbered in ADR-0001) — NPC/item spawning,
      mob-respawn loops, loot tables.** Not yet designed. Surfaced during
      Slice 4's design dive (ADR-0009), deliberately kept out of it —
      maps to WheelMUD's `Clone`/`Spawn` admin actions, already flagged as
      deferred by PLAN-0005 pending "item/NPC creation tooling," plus a
      genuinely new (WheelMUD doesn't have one either) tick-driven respawn
      timer and loot-table shape. Needs its own research/design pass
      before it's added to ADR-0001's inventory table with a real number.
- [ ] **Slice 11 (not yet numbered in ADR-0001, not really a WheelMUD gap
      at all) — scoped/lazy world loading.** Not yet designed. Surfaced
      while discussing ADR-0009's `tunnel` room-lookup with the user, who
      then walked this back to the bigger underlying question: `ThingRepository`
      reconstructs the *entire* stored world into memory on every load —
      fine today, won't scale to real user counts or a large world. A
      persistence-layer/loading-strategy concern, separable from the
      `Thing`/`Behavior` domain model itself (confirmed: `GameDbContext`
      already `Ignore()`s every graph-reference property, so this isn't
      even leaning on EF Core's relationship features today — it's
      `ThingRepository`'s own hand-written full-reconstruct choice). Likely
      fix direction: lazy/regional loading exploiting MUD spatial locality
      (load a `Thing` when first referenced, cache while active), not a
      domain-model rewrite. See [persistence.md](../persistence.md) Open
      Items. Deliberately not designed now — no real world-size trigger yet;
      revisit once there's an actual deployment approaching scale, or
      Slice 9 makes the world big enough to matter.

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
