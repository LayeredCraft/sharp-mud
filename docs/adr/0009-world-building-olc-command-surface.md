# [ADR-0009] World-Building/OLC Command Surface

**Status:** Accepted

**Date:** 2026-07-23

**Decision Makers:** solo (design dive conducted with the user)

## Context

Per [ADR-0001](0001-wheelmud-reconciliation-roadmap.md), this is Slice 4
of the WheelMUD reconciliation roadmap, explicitly bundled with Slice 3
(now `SecurityRole`, [ADR-0005](0005-security-role-model-and-moderation-commands.md))
since it consumes the same mechanism. WheelMUD's own OLC surface is
minimal — just `Actions/OLC/Tunnel.cs`, an admin command that wires a
two-way exit between two *already-existing* rooms by id; WheelMUD has no
room-creation command at all (rooms are only ever built in code via
`Core/Creators/`).

`docs/world-model.md`'s "Content Authoring Evolution" section, though,
already names a further stage sharp-mud wants that WheelMUD never had:
in-game building commands (`@dig`, `@describe`) writing directly into the
live world tree, explicitly *not* requiring a data-file format first.
`docs/commands.md`'s V1 Verb List already flags builder/OLC verbs as
excluded pending "the deferred in-game building phase" — this ADR is that
phase.

Verified by direct research (see `docs/plans/0009-world-building-olc-command-surface.md`
for exact file/method citations): rooms today only ever get created once,
at boot, in a ruleset's `IWorldBuilder.Build()` (e.g.
`src/SharpMud.Ruleset.Basic/BasicWorldBuilder.cs`), which also contains the
only existing "wire a two-way exit" logic (`Connect(world, a, b,
direction)` — two `Thing`s, one `ExitBehavior` each, one per direction,
using the existing `Direction.Opposite()` extension). There is no
runtime/in-game equivalent, and no player-facing short id for a room today
— just an internal `ThingId` (`Guid`), never surfaced to a user.
`SecurityRole.MinorBuilder`/`FullBuilder` (added in ADR-0005, accumulation
already implemented) exist and are unused, doc-commented as reserved for
this slice.

## Decision Drivers

- ADR-0001 already scoped this as "small once #3 exists" — the mechanism
  (role-gated commands via `RegisterWithRole`, admin-command dependency
  shape via constructor-injected `IThingRepository`) is fully reusable
  from Slice 3, nothing new needed there.
- `world-model.md` already commits sharp-mud to going further than
  WheelMUD's Tunnel-only OLC (room creation, not just room linking) —
  reconciling only the narrower WheelMUD scope would leave `commands.md`'s
  own flagged gap half-closed.
- No new `Thing` subtype (`design-decisions.md` rule 2) — a "room" is
  still a `Thing` + `RoomBehavior`, built exactly like
  `BasicWorldBuilder.CreateRoom` does today, just at runtime instead of at
  boot.
- `CommandParser` splits on whitespace only (`src/SharpMud.Engine/Commands/CommandParser.cs`)
  — no quoted-string support exists anywhere in the command pipeline
  today. Any multi-word argument (a room name) has to be "rest of the
  line," which rules out a command needing two independent free-text
  fields (e.g. name *and* description) in one line.
- Rooms have no player-facing identifier other than `Thing.Name` — a
  targeting scheme has to be built on that (name-based lookup) or a wholly
  new id concept invented; introducing a new persisted id purely for this
  slice is unjustified scope given name-based lookup already works and
  nothing else in the codebase uses non-`Guid` ids.

## Considered Options

**Scope (what this slice builds):**

1. **Tunnel-only** — one command, linking two pre-existing rooms by name,
   matching WheelMUD's actual OLC surface exactly.
2. **Dig + tunnel** — add room *creation* (`dig`, wiring a new room to the
   current one) alongside a `tunnel`-only command for linking two
   pre-existing rooms, closing `world-model.md`'s stated gap in full.

**Room targeting:**

1. **By exact `Thing.Name`** — reuse the field players already see;
   ambiguous/duplicate names rejected with a clear error.
2. **New short numeric/slug room id** (WheelMUD/ROM-`vnum`-style) — a new
   stable identifier, its own admin command to list rooms with their ids,
   and a new persisted field.

## Decision Outcome

Chosen: **"2 — dig + tunnel"** for scope, **"1 — by exact `Thing.Name`"**
for targeting.

Scope: WheelMUD's Tunnel-only OLC solves a narrower problem than the one
sharp-mud has already committed to solving (`world-model.md`'s stage-3
authoring evolution). Reconciling only the WheelMUD-faithful subset would
require redesigning this again immediately to add room creation — better
to build both now, in the same slice, while the security-role mechanism
and admin-command dependency shape are already fresh from Slice 3.

Targeting: researched ROM/Diku's OLC convention directly as prior art
(numeric `vnum` addressing, block-allocated per area, exits set
per-direction with no automatic two-way mirroring — a builder sets both
sides manually). That scheme solves a problem sharp-mud doesn't have
(multiple builders editing separate area files, needing collision-free
ids across files) — sharp-mud has one live, in-memory world tree, so a
new id concept would be solving for a constraint that isn't real here.
Name-based lookup costs nothing new; a `vnum`-equivalent would require
designing and persisting an id field, plus a command to discover it,
for a benefit that doesn't apply to this codebase's actual shape.

**Mechanism, in full:**

- Three new commands, all gated `SecurityRole.MinorBuilder`, registered
  via `RegisterWithRole` (mirrors Slice 3's `AdminCommands` shape exactly)
  in a new `BuilderCommands.RegisterAll(registry, repository)`:
  - **`dig <direction> <new room name>`** — creates a new `Thing` +
    `RoomBehavior` (empty `Description`, filled in afterward via
    `describe`), attached as a child of `ctx.CurrentRoom`'s parent (the
    same area/container `ctx.CurrentRoom` itself lives under — a dug room
    is a sibling of the room it's dug from, not a child of it; exits are
    children of rooms, rooms are children of an area), then wires a
    bidirectional exit between `ctx.CurrentRoom` and the new room in the
    given direction (and its `Direction.Opposite()` back).
  - **`tunnel <direction> <existing room name>`** — looks up an existing
    room by exact `Name` among `world.AllWithBehavior<RoomBehavior>()`;
    rejects with a clear message if zero or more than one match. Wires
    the same bidirectional exit as `dig`, between `ctx.CurrentRoom` and
    the found room.
  - **`describe <text>`** — sets `ctx.CurrentRoom.Description` to the
    rest-of-line text. Not itself in WheelMUD's Tunnel-only OLC, but a
    direct, unavoidable consequence of choosing option 2 above (`dig`
    creates a room with no description; without a way to set one
    afterward, every dug room is permanently blank) and matches
    `world-model.md`'s own `@describe` naming.
  - All three take `IThingRepository` via constructor injection (same
    shape as Slice 3's six repository-needing admin commands), and after
    mutating the tree, walk `ctx.CurrentRoom` up via `.Parent` to the
    root `Thing` (no `Parent`) and call `repository.SaveTreeAsync(root,
    ct)` — not `SaveTreeAsync(ctx.CurrentRoom, ct)`, because `dig`'s new
    room and its own reverse exit live outside `ctx.CurrentRoom`'s own
    subtree (siblings, not descendants); only a save rooted above both
    rooms captures everything that changed in one call.
- The exit-wiring logic (`Connect`-equivalent: two `Thing`s, one
  `ExitBehavior` each, `Direction`/`Direction.Opposite()`) is extracted
  from `BasicWorldBuilder` into a small shared helper in
  `SharpMud.Engine` (all the types involved — `Thing`, `Direction`,
  `ExitBehavior`, `IWorld` — already live in `Engine`), and
  `BasicWorldBuilder.Connect` is updated to call it instead of keeping its
  own copy — a direct, justified dedup enabled by writing the same logic
  a second time in this slice, not a separate unrelated refactor.
- No delete/undo command (`undig`, room removal) — not in WheelMUD's
  Tunnel either, and removal safety (what happens to players/items/exits
  currently inside a removed room) is a genuinely different problem,
  deliberately deferred rather than folded in here.

### Positive Consequences

- Closes `commands.md`'s explicitly-flagged builder/OLC gap in full, not
  just the narrower WheelMUD-equivalent subset.
- `SecurityRole.MinorBuilder`/`FullBuilder` get their first real consumer,
  as ADR-0005 anticipated.
- Reuses Slice 3's registration/dependency/save patterns exactly — no new
  architectural shape introduced.
- The `Connect`-logic dedup between `BasicWorldBuilder` and the new
  commands means there's exactly one place that knows how to wire a
  two-way exit, not two copies that can drift.

### Negative Consequences

- No room-removal/undo path — a `dig` mistake is permanent (fixable only
  by leaving the room in place, unconnected, or a manual DB edit); accepted
  as a smaller, separable problem, not a reason to hold up this slice.
- Name-based room lookup means two rooms sharing a name are ambiguous to
  `tunnel` (rejected with an error, not silently picking one) — acceptable
  given no naming-uniqueness constraint exists elsewhere in the world
  model today, and enforcing one now would be a separate, broader change.
- `describe` only touches the room the builder is currently standing in —
  there's no `describe <room name>` for a room elsewhere; consistent with
  WheelMUD's Tunnel needing no such remote-targeting either, but a real
  limitation if a builder wants to batch-describe several dug rooms
  before walking between them.
- **Explicitly does not include NPC/item spawning** — surfaced during this
  ADR's own scoping discussion: sharp-mud today has no mob-respawn loop or
  loot-table mechanism at all (`docs/combat.md` — NPC death permanently
  removes it from the world, no timer brings it back; loot drops are
  "not implemented, blocked on the item system"). This maps to WheelMUD's
  `Clone`/`Spawn` admin actions, already explicitly deferred by
  [PLAN-0005](../plans/0005-security-role-model-and-moderation-commands.md)
  pending "item/NPC creation tooling" — a genuinely separate decision
  (spawn timing, loot table shape, whether `Clone`/`Spawn` are even the
  right shape for a tick-driven respawn vs. a one-shot admin placement)
  deliberately left for its own future slice rather than folded in here,
  per the user's explicit call during this ADR's design dive.

## Pros and Cons of the Options

### Scope option 1: Tunnel-only

- Good, because it's the smallest possible slice, most faithful to
  WheelMUD's actual OLC surface.
- Bad, because it doesn't close `world-model.md`'s already-stated gap
  (room creation) — a second design pass would be needed almost
  immediately after.

### Scope option 2: Dig + tunnel (chosen)

- Good, because it closes the real, already-documented gap in one pass
  while the Slice 3 mechanism is fresh.
- Bad, because it's more surface than WheelMUD's own OLC ever had — not a
  1:1 reconciliation, a deliberate extension past it.

### Targeting option 1: By exact `Thing.Name` (chosen)

- Good, because it needs no new concept, field, or command — the name
  players already see is the identifier.
- Bad, because duplicate room names are ambiguous (rejected, not silently
  resolved).

### Targeting option 2: New short id (vnum-style)

- Good, because it's what WheelMUD/ROM-family MUDs actually do, and scales
  better if the world ever has many same-named rooms.
- Bad, because it solves a multi-builder/multi-area-file collision problem
  sharp-mud doesn't have (one live world tree, not separate area files),
  and requires a new persisted field plus a discovery command with no
  other consumer today.

## Links

- [ADR-0001](0001-wheelmud-reconciliation-roadmap.md) — WheelMUD
  Reconciliation Roadmap (this is Slice 4, bundled with Slice 3).
- [ADR-0005](0005-security-role-model-and-moderation-commands.md) — the
  `RegisterWithRole`/`SecurityRole` mechanism this slice consumes
  unchanged; `MinorBuilder`/`FullBuilder` were added there specifically
  for this slice.
- [PLAN-0009](../plans/0009-world-building-olc-command-surface.md) —
  execution plan for this decision.
- `docs/world-model.md` — "Content Authoring Evolution" stage this ADR
  implements; also where frontier/procedural generation (out of scope,
  Slice 9) is separately tracked.
- `docs/commands.md` — the V1 Verb List's builder/OLC exclusion this ADR
  resolves.
- `src/SharpMud.Ruleset.Basic/BasicWorldBuilder.cs` — existing
  boot-time room/exit creation pattern this slice's runtime commands
  mirror (and whose `Connect` logic moves into `Engine`, shared).
- WheelMUD `Actions/OLC/Tunnel.cs` — source consulted; adopted the
  two-way-exit-by-direction shape, explicitly extended past it (room
  creation) per the Decision Outcome above.
- ROM/Diku-family `redit`/vnum OLC convention — researched as prior art
  for room targeting, explicitly not adopted (see Decision Outcome).
