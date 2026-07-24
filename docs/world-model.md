# World Model

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [character.md](character.md) for the Player entity that
occupies rooms, and [persistence.md](persistence.md) for how rooms are stored.

**Superseded by [engine-vs-ruleset.md](engine-vs-ruleset.md)**: `Room`/`Exit`/
`Area` below are no longer dedicated classes — they're `Thing`s composed from
`RoomBehavior`/`ExitBehavior`/`AreaBehavior`. This doc still describes the
correct *shape* (exits, locking, hand-built hub vs. generated frontier); read
engine-vs-ruleset.md for how that shape is actually represented in code now.

## Authoring Strategy

Hybrid, per SPEC.md:

- **Core/hub area** (starting town, key NPCs, tutorial content): hand-authored.
- **Wilderness/dungeon frontier**: procedurally generated, but **generated
  once and persisted** — not regenerated per visit. Once created, a frontier
  room is saved and stays fixed, exactly like a hand-built room from the
  player's perspective. This preserves classic MUD navigation muscle memory
  (N N E E S W means the same thing every time).

The in-memory world model below makes no assumption about which authoring path
produced a given `Room` — that's what allows moving from hardcoded rooms to a
data-driven JSON format later without disturbing the rest of the engine.

## Data Model

```csharp
public sealed class Room
{
    public RoomId Id { get; init; }
    public AreaId AreaId { get; init; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsGenerated { get; init; } // true for procedural frontier rooms
    public List<Exit> Exits { get; set; } = [];
    public List<ItemId> ItemsOnGround { get; set; } = [];
    public List<NpcId> Npcs { get; set; } = [];
}

public sealed class Exit
{
    public Direction Direction { get; set; }
    public RoomId DestinationRoomId { get; set; }
    public bool IsOneWay { get; set; }        // no implicit return path assumed
    public ExitLockState? Lock { get; set; }  // null = not lockable
}

public sealed class ExitLockState
{
    public bool IsLocked { get; set; }
    public bool IsClosed { get; set; }
    public ItemId? RequiredKeyItemId { get; set; }
}

public enum Direction { North, South, East, West, NorthEast, NorthWest, SouthEast, SouthWest, Up, Down }
```

Bidirectional exits are a **construction-time convenience**, not a runtime
invariant: creating a standard exit creates the matching return `Exit` on the
destination room, but the model itself makes no assumption that a reverse exit
exists — this is what allows one-way exits and locked doors to be first-class
without special-casing.

## World Access

```csharp
public interface IWorld
{
    Room? GetRoom(RoomId id);
    Player? GetPlayer(PlayerId id);
    IEnumerable<Player> PlayersInRoom(RoomId id);
    void MovePlayer(Player player, Room from, Room to);
}
```

`IWorld.MovePlayer` is the single choke point for all player-room transitions
— see the movement walkthrough in [commands.md](commands.md) for the full
call sequence, and the frontier-generation walkthrough below for what happens
when a move target doesn't exist yet.

## Content Authoring Evolution (not all v1)

1. Hardcoded rooms in C# (bootstrap only, to get the loop working) — still
   how every world's starting content is authored today
   (`HubWorldBuilder`/`BasicWorldBuilder`).
2. Data-driven world files (JSON/YAML) loaded at startup — **not yet
   implemented**; still a natural next step if hardcoded content stops
   scaling, but turned out not to be a prerequisite for stage 3 below.
3. In-game building commands (`dig <direction> <name>`, `tunnel <direction>
   <existing room name>`, `describe <text>`) writing directly to the live
   `Thing` tree — **implemented**, without needing stage 2's file format
   first (a runtime command mutates the same in-memory/persisted model
   stage 1's rooms already use). Role-gated at
   `SecurityRole.MinorBuilder`; see
   [ADR-0009](adr/0009-world-building-olc-command-surface.md) and
   [commands.md](commands.md)'s V1 Verb List. Deliberately doesn't cover
   NPC/item spawning, mob-respawn loops, or loot tables — a separate,
   still-undesigned gap (see
   [PLAN-0001](plans/0001-wheelmud-reconciliation-roadmap.md)'s Slice 10).

## Sequence: Procedural Frontier Room Generated

1. `MoveCommand` (see [commands.md](commands.md)) resolves an exit whose
   `DestinationRoomId` points into ungenerated frontier space (represented as
   a coordinate not yet backed by a persisted `Room`).
2. World layer calls the (future, deferred per SPEC.md) frontier generator to
   produce a `Room` for that coordinate.
3. Generated `Room` is saved via `IRoomRepository.SaveAsync` (see
   [persistence.md](persistence.md)) with `IsGenerated = true` **before** the
   player is moved into it — generation happens once, then it's a normal
   persisted room forever after. This is the mechanism behind the
   generate-once-persist decision in SPEC.md.

## Frontier Generation Approach

Grid-based overland map: frontier rooms exist on an implicit (x, y) grid;
terrain assigned via noise (Perlin/simplex) for biome variety, room
descriptions templated per terrain type. Matches classic MUD "wilderness
outside town" convention better than a roguelike dungeon-graph generator
would. Still deferred per SPEC.md until after the foundation phase — this is
the confirmed approach for when that phase starts, not a v1 commitment.

## Open Items

- Area boundary rules: how/when a frontier coordinate gets assigned to an
  `AreaId` vs. spanning a borderless wilderness.
- Terrain-type template variety needed to avoid repetitive descriptions.
- Noise-function parameters (scale, octaves, seed source) not yet chosen.
