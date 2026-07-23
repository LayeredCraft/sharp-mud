# [PLAN-0009] World-Building/OLC Command Surface

**Implements:** [ADR-0009](../adr/0009-world-building-olc-command-surface.md)

**Status:** Done

**Last updated:** 2026-07-23

## Goal

A `MinorBuilder`-gated `dig <direction> <name>` / `tunnel <direction>
<existing room name>` / `describe <text>` command set works end-to-end
over real Telnet: `dig` creates a new room and a two-way exit from the
builder's current room; `tunnel` wires the same kind of two-way exit
between the current room and an already-existing room found by name;
`describe` sets the current room's description. All three persist
immediately and survive a restart.

## Scope

Per ADR-0009's Decision Outcome. In scope: the three commands above, the
shared `Connect`-equivalent exit-wiring helper extracted from
`BasicWorldBuilder` into `SharpMud.Engine`, room lookup by exact `Name`.

Explicitly deferred (per ADR-0009): room/exit deletion (`undig`), a
`describe <room name>` remote-targeting variant, any new room-id concept,
data-file-driven world content (Slice 9 territory / `world-model.md`
stage 2).

## Tasks

### Shared exit-wiring helper

- [x] New `src/SharpMud.Engine/Behaviors/RoomConnector.cs` — a static
      `Connect(IWorld world, Thing a, Thing b, Direction direction)` moved
      from `BasicWorldBuilder.Connect`: creates two `Thing`s (one
      `ExitBehavior` each, using `direction` and `direction.Opposite()`),
      adds each as a child of the room it exits from, registers both in
      `world`.
- [x] `src/SharpMud.Ruleset.Basic/BasicWorldBuilder.cs`: removed its
      private `Connect` method, calls the new shared helper instead — no
      behavior change.
- [x] `samples/SharpMud.Samples.Classic/HubWorldBuilder.cs`: same dedup —
      it has its own identical private `Connect`, missed in the original
      ADR-0009 text (which only named `BasicWorldBuilder`); this is the
      world the Classic/Telnet sample actually boots, so it's the one that
      matters for this plan's manual verification. Removed its `Connect`,
      calls `RoomConnector.Connect` instead — no behavior change.

### New commands (`src/SharpMud.Engine/Commands/Builtin/Builder/`)

All three take `IThingRepository` via constructor injection (same shape as
Slice 3's admin commands). All three, after mutating the tree, walk
`ctx.CurrentRoom` up via `.Parent` to the root `Thing` (`Parent == null`)
and call `repository.SaveTreeAsync(root, ct)` — **not**
`SaveTreeAsync(ctx.CurrentRoom, ct)`, since `dig`'s new room (and its
reverse exit) live outside `ctx.CurrentRoom`'s own subtree as siblings,
not descendants; only a save rooted above both rooms captures everything
that changed.

- [x] `DigCommand` (`MinorBuilder`) — usage `dig <direction> <new room
      name>`. Parses `Args[0]` as a `Direction`; rest of `Args` joined as
      the new room's name. Creates the room + `RoomBehavior()`
      (`Description` left `""`), attached as a child of
      `ctx.CurrentRoom.Parent`, registered in `ctx.World`. Calls
      `RoomConnector.Connect` between `ctx.CurrentRoom` and the new room.
- [x] `TunnelCommand` (`MinorBuilder`) — usage `tunnel <direction>
      <existing room name>`. Looks up the destination room via
      `BuilderCommandHelpers.FindRoomsByName` (exact `Name` match,
      `OrdinalIgnoreCase` — matches the existing convention used
      throughout `Commands/Builtin/Admin/*` and `GiveCommand`/
      `ObjectMatcher`, verified directly rather than assumed). Rejects
      zero matches, more than one match, and self-tunnel. Saves both the
      current room's tree root and the destination's (they may differ if
      there's more than one area).
- [x] `DescribeCommand` (`MinorBuilder`) — usage `describe <text>`. Sets
      `ctx.CurrentRoom.Description` to the rest-of-line text (empty
      rejected via `CommandGuards.RequireArgsAsync`).
- [x] `BuilderCommandHelpers`
      (`src/SharpMud.Engine/Commands/Builtin/Builder/BuilderCommandHelpers.cs`)
      — `TryParseDirection`, `FindRoomsByName`, `FindRoot`, shared by
      `DigCommand`/`TunnelCommand`/`DescribeCommand`. Mirrors
      `AdminCommandHelpers`'s shape (`internal static class`, no
      `IThingRepository` dependency — each command holds its own
      repository via its own constructor and never threads it into the
      helper).
- [x] Register all three via `RegisterWithRole` in
      `BuilderCommands.RegisterAll(registry, repository)`
      (`src/SharpMud.Engine/Commands/Builtin/Builder/BuilderCommands.cs`
      — mirrors `AdminCommands`'s shape exactly). Called from the same
      `registerConsumerCommands` callback in
      `samples/SharpMud.Samples.Classic/Program.cs` that already calls
      `AdminCommands.RegisterAll`, resolving `IThingRepository` from the
      same `IServiceProvider`.

### Docs

- [x] `docs/commands.md`: describe `dig`/`tunnel`/`describe` as current
      state in the V1 Verb List, replacing the "builder/OLC verbs
      excluded" note; link ADR-0009.
- [x] `docs/world-model.md`: updated "Content Authoring Evolution" —
      stage 3 marked implemented, stage 2 explicitly noted as not a
      prerequisite for it after all.
- [x] `SPEC.md`: updated its own (near-duplicate) "Content authoring
      evolution" list and phase-9 status the same way; added a new
      Deferred/Open Item for the NPC/item-spawning gap surfaced during
      this ADR's scoping discussion.
- [x] `docs/adr/README.md` / `docs/plans/README.md`: index rows added for
      ADR-0009 (`Accepted`)/PLAN-0009 (`In Progress`).
- [x] `docs/plans/0001-wheelmud-reconciliation-roadmap.md`: check off
      Slice 4.

## Critical files

New:
- `src/SharpMud.Engine/Behaviors/RoomConnector.cs`
- `src/SharpMud.Engine/Commands/Builtin/Builder/DigCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Builder/TunnelCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Builder/DescribeCommand.cs`
- `src/SharpMud.Engine/Commands/Builtin/Builder/BuilderCommands.cs`
- `src/SharpMud.Engine/Commands/Builtin/Builder/BuilderCommandHelpers.cs`
- `tests/SharpMud.Engine.Tests/Behaviors/RoomConnectorTests.cs`
- `tests/SharpMud.Engine.Tests/Commands/Builtin/Builder/DigCommandTests.cs`
- `tests/SharpMud.Engine.Tests/Commands/Builtin/Builder/TunnelCommandTests.cs`
- `tests/SharpMud.Engine.Tests/Commands/Builtin/Builder/DescribeCommandTests.cs`
- `tests/SharpMud.Engine.Tests/Commands/Builtin/Builder/BuilderCommandsTests.cs`

Modified:
- `src/SharpMud.Ruleset.Basic/BasicWorldBuilder.cs` (`Connect` extracted)
- `samples/SharpMud.Samples.Classic/HubWorldBuilder.cs` (same extraction —
  missed in the original ADR-0009 text)
- `samples/SharpMud.Samples.Classic/Program.cs`
- `docs/commands.md`, `docs/world-model.md`, `SPEC.md`,
  `docs/adr/README.md`, `docs/plans/README.md`,
  `docs/plans/0001-wheelmud-reconciliation-roadmap.md`

## Test plan

- Unit: shared `Connect` helper — given two rooms and a direction,
  produces one `ExitBehavior` on each side with correct `Direction`/
  `Direction.Opposite()` and `Destination`, both registered in `world`,
  both attached as children of the correct room.
- Unit: `DigCommand` — happy path (valid direction, non-empty name)
  creates a new `Thing` with `RoomBehavior`, wires the two-way exit,
  attaches the new room as a sibling (child of `ctx.CurrentRoom.Parent`,
  not of `ctx.CurrentRoom` itself), calls `SaveTreeAsync` with the tree
  root (not `ctx.CurrentRoom`). Invalid direction and empty name each
  rejected with a clear message, no mutation.
- Unit: `TunnelCommand` — happy path connects `ctx.CurrentRoom` to a
  found room; zero matches and multiple matches each rejected with a
  clear, distinct message; self-tunnel (found room equals
  `ctx.CurrentRoom`) rejected.
- Unit: `DescribeCommand` — sets `ctx.CurrentRoom.Description`; empty
  text rejected, `Description` unchanged.
- Unit: `BuilderCommands.RegisterAll` — all three register via
  `RegisterWithRole(_, SecurityRole.MinorBuilder)`, not `RegisterOpen`.
- Regression: `BasicWorldBuilder`'s existing world-boot tests (if any)
  still pass unchanged after `Connect` moves to the shared helper —
  confirms the extraction is behavior-preserving.

## Verification

**Done (2026-07-23)** — real manual check over Telnet against
`samples/SharpMud.Samples.Classic` (this repo's established pattern for
world/persistence-facing changes), scratch SQLite DB, `SHARPMUD_INITIAL_ADMIN`
bootstrap + a live `rolegrant <username> minorbuilder` to reach
`MinorBuilder`.

**Bug found and fixed during this pass, not caught by unit tests**:
`dig`/`tunnel` didn't check whether the origin room already had an exit in
the requested direction. `Town Square` already has all four cardinal exits
from `HubWorldBuilder`; `dig north Storage Shed` silently added a *second*
`north` exit-`Thing` rather than rejecting. `MoveCommand` resolves exits via
`.FirstOrDefault()`, so the new exit was shadowed by the pre-existing one —
the dug room became unreachable via `north`, and the room's exit listing
started showing `north` twice. Fixed by adding
`BuilderCommandHelpers.HasExit(Thing, Direction)`, checked by both
`DigCommand` (origin only) and `TunnelCommand` (origin *and* the
destination's opposite direction, since that side can be occupied too),
with regression tests added for all three cases
(`DigCommandTests.ExecuteAsync_RejectsDirectionAlreadyOccupied_WithoutMutating`,
`TunnelCommandTests.ExecuteAsync_RejectsWhenOriginDirectionAlreadyOccupied`,
`TunnelCommandTests.ExecuteAsync_RejectsWhenDestinationOppositeDirectionAlreadyOccupied`).
Re-verified live after the fix.

1. `dig up Storage Shed` from Town Square (its four cardinal directions
   were already occupied by the hub's own rooms — confirms the fix
   actually engages, not just an untested code path): room created,
   reachable via `up`, `down` returns to Town Square, `Town Square`'s exit
   list shows `up` exactly once.
2. `describe` the new room while standing in it; `look` immediately shows
   the new description — and, after the fix, this correctly targets the
   room the builder actually walked into (session 1's pre-fix run had
   this land on the wrong room entirely, a direct symptom of the shadowed
   -exit bug above).
3. `tunnel down Old Well` from Town Square to an already-existing hub
   room found by name: succeeds, `Old Well`'s exit list shows `up` (its
   only pre-existing exit was `east`, back to Town Square — no
   collision), reachable both directions.
4. Rejections confirmed live: `tunnel west Old Well` from Storage Shed
   (`Old Well` already has an `east` exit, the opposite of `west`) →
   `"Old Well already has an exit east."`; `tunnel south Storage Shed`
   from Town Square (`south` already goes to `Southern Gate`) →
   `"There's already an exit south from here."`; a nonexistent room name
   → `"No room named X was found."`.
5. Restart the server against the same DB; confirmed via direct SQLite
   inspection (`Things`/`Behaviors` tables) rather than a second Telnet
   session, since a raw-socket scripted client kept racing the session's
   own buffered output — every new room, its description, and both
   directions of every new exit (correct `ExitDestinationId` shadow-FK on
   both sides, no duplicates) were present exactly as created, alongside
   every pre-existing `HubWorldBuilder` room/exit, unchanged.
6. Confirmed a pre-existing, unrelated crash (documented already in
   PLAN-0005's own Verification section — the game loop's `WanderManager`
   tick broadcasting to a dead socket) is not something this slice
   introduced or worsened; hit it once by disconnecting a raw script
   mid-session, not through any dig/tunnel/describe path.

Not separately re-verified live (already covered by unit tests, and the
underlying mechanism is Slice 3's, not new here): a `Player` with no
builder role being rejected by all three commands —
`BuilderCommandsTests` confirms all three register via `RegisterWithRole`,
and `RoleGuardedCommand`'s gating behavior itself already has its own
Slice 3 test coverage.

## Open questions / blockers

- Exact rejection message wording is a placeholder, not a considered
  final string (same open item Slice 3 flagged for its own commands).
- **Not this slice, tracked for later**: NPC/item spawning, mob-respawn
  loops, and loot tables — a real, currently-undesigned gap surfaced
  while scoping ADR-0009, deliberately kept out of it (see ADR-0009's
  Negative Consequences). Not yet a numbered slice in
  [PLAN-0001](0001-wheelmud-reconciliation-roadmap.md) — needs its own
  research/design pass (WheelMUD's `Clone`/`Spawn` actions, plus
  something WheelMUD itself doesn't have: an actual tick-driven respawn
  timer and loot-table shape) before it earns a slice number.
- **Not this slice, deliberately deferred by choice, not oversight**:
  expanding `BasicWorldBuilder`'s two-room starter world. Discussed during
  this ADR's dive — kept as-is since it's explicitly documented as a
  "quick-start default," not real game content, and this slice's `dig`
  now gives an in-game path to grow it without enlarging the fixed
  sample.
