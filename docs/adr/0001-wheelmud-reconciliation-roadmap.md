# [ADR-0001] WheelMUD Reconciliation Roadmap

**Status:** Proposed

**Date:** 2026-07-16

**Decision Makers:** solo

## Context

sharp-mud was designed with WheelMUD (a mature open-source C# MUD codebase,
checked out locally at `/Users/ncipollina/source/repos/davidrieman/WheelMUD`)
as prior art. A real architecture-level reconciliation already happened —
`docs/research/wheelmud-findings.md` + `docs/engine-vs-ruleset.md` document
adopting `Thing`/`Behavior` composition, the event system (simplified), the
world/room model, command guards, and combat-as-ruleset, while explicitly
rejecting MEF, static singletons, and RavenDB-style persistence.

What's left is going through the areas that were **deferred or never
compared** against WheelMUD at all, systematically, one at a time — each
following the design-dive → ADR → implement → docs → tests cycle
formalized in `.agents/skills/engineering-workflow/references/design-decisions.md`.
This ADR records the roadmap: the full inventory of what's left, the
priority order, and the standard process each slice follows. It does not
design any slice past the first in detail — each future slice gets its own
research-and-plan pass (and, where warranted, its own ADR) when its turn
comes.

## Decision Drivers

- WheelMUD's `src/` is a real, substantial codebase (37,445 lines across
  ~490 files, 15 assemblies) — reconciling against it needs a sequence,
  not an unbounded "port everything" effort.
- Several sharp-mud subsystem docs (`SPEC.md`, `networking.md`,
  `commands.md`, `world-model.md`) already carry "Open Items"/deferred
  markers that turn out to map directly onto specific WheelMUD subsystems
  — the roadmap should close those gaps in a deliberate order, not
  randomly.
- Some things WheelMUD has were already explicitly rejected (MEF, RavenDB)
  or explicitly deferred pending real need (hot-swap ruleset loading) —
  the roadmap should say so plainly rather than re-litigating settled
  decisions.
- Not every WheelMUD subsystem is worth reconciling — some (the embedded
  FTP server) have no clear purpose for sharp-mud at all and deserve an
  explicit "no" recorded, not silent omission.

## Full Inventory: WheelMUD vs. sharp-mud

Already reconciled (not part of this roadmap): `Thing`/`Behavior`
composition, the event system, the world/room model, command guards,
combat-as-ruleset, basic Telnet transport — see
`docs/research/wheelmud-findings.md` and `docs/engine-vs-ruleset.md`.

Everything below is what's left, organized by WheelMUD subsystem:

| WheelMUD source (size) | What it does | sharp-mud today | Gap |
|---|---|---|---|
| `Server/` — Telnet protocol (2,679 lines) | IAC/Q-Method negotiation, MCCP/MXP/NAWS/TermType | Raw TCP, IAC bytes stripped and discarded, zero negotiation | **Slice 1** — see ADR-0002 |
| `ConnectionStates/` (1,117 lines, 17 files) | Explicit state machine: handshake → login/character-creation → playing; reconnect handling | `LoginFlow` is a procedural loop, not a formal state machine; no reconnect/resumption at all — a fresh Telnet connection always creates a new player | **Slice 2** |
| `Core/Attributes/ActionSecurityAttribute.cs` + `Actions/Admin/` (16 files) | `SecurityRole` flags enum (`player`/`minorBuilder`/`fullAdmin`/etc.) gating every command; `ban`/`boot`/`jail`/`mute`/`role-grant`/`control`/`goto`/`find`/`clone`/`spawn`/`buff`/`announce` | **Nothing.** No per-command authorization concept exists in `ICommand` at all; `SPEC.md` lists moderation/admin tooling as "not designed" | **Slice 3** |
| `Actions/OLC/Tunnel.cs` (1 file) + `Core/Creators/` | World-building is minimal even in WheelMUD — just an admin command to wire a two-way exit between existing rooms by ID. No room/area file format, no procedural gen anywhere. World content is hand-built in code (`DefaultWorldCreator`, ruleset-specific area creators), then persisted and extended only via admin commands | `HubWorldBuilder` is sharp-mud's direct equivalent of WheelMUD's `Creators` pattern — **already substantially reconciled in spirit.** What's missing: any in-game way to extend the world after boot (no `Tunnel`-equivalent command) | **Slice 4** — bundles with #3, `Tunnel` is itself security-gated in WheelMUD |
| `Core/ManagerSystems/HelpManager.cs` | Loads plaintext help files off disk by filename, aliasable | `HelpCommand` exists but `commands.md`'s Open Items flags help content structure as "undecided" | **Slice 5** |
| `Actions/Configure/` (10 files: AFK, alias, description, password, title, settings, ...) | Player-level preference commands | None of these exist | **Slice 6** — quality-of-life, not blocking |
| `Actions/Commercial/` + `CurrencyBehavior` | Shop buy/sell/list, currency | `SPEC.md` already defers "shopkeepers/quest givers until currency/trade/quest systems exist" — this is exactly that prerequisite | **Slice 7** |
| `Core/DefaultComposer.cs` (MEF) + priority-based override (Core=0, ruleset=100, game=200+) + `ServerHarness`'s hot-swap `update-actions` | How a ruleset (WarriorRogueMage) and world-content package (Universe) register into Core without Core knowing about them ahead of time | MEF already explicitly **rejected** (`wheelmud-findings.md`) in favor of assembly-scan + DI; hot-swap already deferred in `SPEC.md` pending a real second-ruleset need | **Slice 8 — placeholder only**, not actionable until a second ruleset is actually being built |
| *(no WheelMUD equivalent — WheelMUD has no procedural generation at all)* | — | `world-model.md`: frontier generation algorithm not chosen | **Slice 9** — genuinely from-scratch design dive, not a reconciliation task; lower priority per `SPEC.md` |
| `WheelMUD.Ftp` (2,623 lines, 48 files) | Embedded FTP server, unclear purpose relative to the MUD itself | N/A | **Rejected** — see Decision Outcome |
| `WheelMUD.Data.RavenDb` | RavenDB-specific persistence | Already superseded — EF Core decision recorded in `wheelmud-findings.md` | No action needed |
| Soft-code/scripting | Not really present in WheelMUD as a distinct system either | `SPEC.md`: "revisit only if data-driven behavior proves insufficient" | Lowest priority, unchanged |

## Considered Options

1. Port WheelMUD subsystems in the order they appear in its own solution
   (Core, then Actions, then everything else).
2. Prioritize by line count / raw size (largest subsystems first).
3. Prioritize by dependency order and how directly each gap blocks an
   already-stated sharp-mud goal (`SPEC.md`/subsystem-doc Open Items),
   doing the most directly portable and most-referenced-elsewhere work
   first.

## Decision Outcome

Chosen option: **"3 — prioritize by dependency order and existing stated
need,"** because sharp-mud already has explicit Open Items pointing at
several of these gaps, and tackling the session/transport layer first
(slices 1–2) unblocks things layered on top of it (combat's linkdead grace
period, reconnect) before spending effort on lower-urgency systems
(commerce, procedural generation) that nothing else is currently blocked
on.

### Roadmap (prioritized)

1. **Telnet protocol negotiation** (IAC/Q-Method core + NAWS) — most
   directly portable from real WheelMUD code, bounded, unblocks
   terminal-size-aware features. **Detailed in ADR-0002.**
2. **Session/connection state machine + reconnect** — natural next step
   since it's the same session layer slice 1 touches; unblocks combat's
   linkdead-grace-period and `networking.md`'s open reconnect item.
3. **Permission/security-role model + moderation commands** — a real,
   previously-unscoped gap needed before any public multiplayer
   deployment. No direct 1:1 WheelMUD translation given sharp-mud's
   Thing/Behavior model.
4. **World-building/OLC command surface** — small once #3 exists.
5. **Help system** — small, bounded, resolves an existing `commands.md`
   open item.
6. **Player configuration commands** — quality-of-life, not blocking.
7. **Commerce/shops** — unlocks shopkeeper NPCs, matching an
   already-planned `SPEC.md` phase.
8. **Plugin/extensibility for a second ruleset** — placeholder only.
9. **Procedural frontier generation** — genuinely from-scratch, bigger,
   deliberately lower priority per `SPEC.md`.
10. **WheelMUD's FTP server: explicitly rejected.** No clear purpose for
    sharp-mud relative to the MUD itself; recorded here so it's a decision,
    not an oversight.

Each future slice (2 onward) gets its own research-and-plan pass (read the
real WheelMUD source for that area, run the design dive, write its own
ADR and plan) when we get to it — this ADR fixes the sequence and the
*why*, not the detailed *how* for anything past slice 1. Every slice
follows the standard design-dive → ADR → plan → implement → docs → tests
cycle from `design-decisions.md` — not repeated here to avoid two copies
of the same process drifting apart.

Live progress against this roadmap is tracked in
[PLAN-0001](../plans/0001-wheelmud-reconciliation-roadmap.md), not in this
ADR — this document doesn't get edited as slices complete.

### Positive Consequences

- Every future slice has a clear place in a sequence with a stated reason,
  instead of an unbounded "rewrite WheelMUD" mandate.
- Gaps that were previously invisible (no per-command authorization at
  all) are now explicit, tracked roadmap items instead of silent absence.
- WheelMUD subsystems with no real value to sharp-mud (FTP server) get a
  recorded "no," closing the door on accidentally reconciling them later
  out of completionism.

### Negative Consequences

- The roadmap's ordering (drivers: "what's most portable" + "what's
  already referenced elsewhere") is a judgment call, not a formula — it
  will need revisiting if priorities change (e.g. if public deployment
  moves up, slice 3 should move up with it).
- Slices 2+ are intentionally under-designed here; each still needs its
  own research pass before implementation, so this ADR alone doesn't make
  those slices "ready to build."

## Links

- `docs/research/wheelmud-findings.md` — prior architecture-level
  reconciliation this roadmap builds on.
- `docs/engine-vs-ruleset.md` — the `Thing`/`Behavior` model referenced
  throughout the inventory above.
- [ADR-0002](0002-telnet-protocol-negotiation.md) — Telnet Protocol
  Negotiation (Slice 1, detailed decision).
- [PLAN-0001](../plans/0001-wheelmud-reconciliation-roadmap.md) — live
  progress tracking for this roadmap.
- `/Users/ncipollina/source/repos/davidrieman/WheelMUD` — the real WheelMUD
  source checkout this inventory was built from.
