# Persistence

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [engine-vs-ruleset.md](engine-vs-ruleset.md) for `Thing`/
`Behavior`, the entities being persisted.

**Implemented and verified**: `IThingRepository` (`SharpMud.Engine.Core`) and
its EF Core/SQLite implementation `ThingRepository` (`SharpMud.Persistence`)
exist and are wired into `Host` — see Verified below for what was actually
exercised end-to-end, not just unit-tested.

## Strategy

EF Core + SQLite, behind a single repository interface in `SharpMud.Engine`
(`IThingRepository`), implemented in `SharpMud.Persistence`. Game logic
depends only on the interface — never on EF Core, SQLite, or SQL directly
(see [architecture.md](architecture.md) for the enforced dependency
direction).

```csharp
public interface IThingRepository
{
    Task<Thing?> LoadTreeAsync(ThingId rootId, CancellationToken ct);
    Task SaveTreeAsync(Thing root, CancellationToken ct);
    Task<Thing?> FindPlayerByNameAsync(string name, CancellationToken ct);
}
```

One repository, not per-concept repositories (`IPlayerRepository`/
`IRoomRepository`, as earlier drafts of this doc had) — `Thing` is the one
entity type now (see engine-vs-ruleset.md), so a room, a player, and an item
are all just "a `Thing`, loaded/saved the same way." `LoadTreeAsync`/
`SaveTreeAsync` operate on a `Thing` and its full descendant subtree in one
call (a room plus its exits/items/NPCs; a player plus their inventory).

## Why not per-type repositories, and why TPH

Decision: EF Core TPH (table-per-hierarchy) for `Behavior`, not a JSON
column serializing the whole behavior list. TPH gets real relational columns
per behavior type (queryable, no deserialize-to-filter), at the cost of every
`Behavior` subclass needing an EF Core mapping. Since `Behavior` subclasses
span two assemblies that must not reference each other the wrong way
(`SharpMud.Ruleset.Classic` behaviors must not be known to `SharpMud.Engine`
or `SharpMud.Persistence`), mapping registration is itself split:

```csharp
public interface IBehaviorMappingContributor
{
    void ConfigureBehaviors(ModelBuilder modelBuilder);
}
```

`SharpMud.Persistence` maps Engine's own behavior types directly (it already
references `SharpMud.Engine`); `SharpMud.Ruleset.Classic` provides
`ClassicBehaviorMappingContributor`, which requires a new `Ruleset.Classic →
Persistence` project reference (approved as part of this design — Persistence
still never references back). `Host` registers both via DI
(`IEnumerable<IBehaviorMappingContributor>` constructor injection into
`GameDbContext`). Adding a second ruleset later means writing one new
contributor class, not touching `Persistence`.

Each entity/behavior type gets its own `IEntityTypeConfiguration<T>` class
(`SharpMud.Persistence/Configurations/`, `SharpMud.Ruleset.Classic/Configurations/`)
rather than one large inline `OnModelCreating` — `GameDbContext` just calls
`modelBuilder.ApplyConfigurationsFromAssembly(...)` over its own assembly,
and each contributor does the same over its own; adding a new behavior type
means adding one new configuration class, automatically picked up.

## EF Model Shape

- **`Things` table**: `Id` (`ThingId`, via an EF value converter to/from
  `Guid`), `Name`, `Description`, `ParentId` (nullable self-referencing FK).
  `Thing.Children`/`Parent`/`Parents`/`Behaviors`/`Events` are all `Ignore`d
  in the EF model — see Rehydration below for why they're reconstructed in
  code rather than mapped as EF navigations.
- **`Behaviors` table (TPH)**: one shared table, a `Behavior`-type
  discriminator column, plus every registered subclass's own scalar
  properties as (nullable) columns on that same table — standard TPH shape.
  Keyed by a surrogate `PersistenceKey` (`Guid`, added to the `Behavior` base
  class purely for this purpose — see Open Items) plus a shadow `ThingId` FK
  column. `Behavior.Parent` is `Ignore`d in the EF model for the same reason
  `Thing.Children` is (see Rehydration).
- **Thing-reference properties on behaviors** (`ExitBehavior.Destination`,
  `LockableBehavior.RequiredKey`) map to a shadow nullable-`Guid` FK column
  each, resolved to a real `Thing` reference in a second rehydration pass
  (see below) rather than as an EF navigation — same reasoning again.
- **`PlayerBehavior.Aliases` and `EquippedBehavior.Equipped` are not persisted
  at all** (`Ignore`d) — narrower than originally planned (a JSON column per
  property). `Aliases` has no consuming command yet, so there's nothing to
  lose. `Equipped` is a real gap: carried items themselves persist fine
  (they're just `Thing.Children`), but which `EquipSlot` each is worn in does
  not — a reloaded/reconnecting player has their sword back in inventory, not
  on their back. Verified live (see Verified below). See Open Items.
- **Enum-like value types** (`Race`/`CharacterClass`, now
  `LayeredCraft.OptimizedEnums` instances, not plain enums — see
  `docs/character.md`) map via an EF value converter to/from their
  underlying `int` value.

## Rehydration: why loading can't just be "EF Core navigation fixup"

`Behavior.OnAddBehavior()`/`OnRemoveBehavior()` (see engine-vs-ruleset.md)
run real side effects when a `Behavior` is attached to a `Thing` — e.g.
`LockableBehavior` subscribes to move-request events on its parent. If EF
Core materialized `Thing.Children`/`Behavior.Parent` as ordinary navigation
properties, it would set them directly via reflection on load, **bypassing**
`Thing.Add`/`BehaviorManager.Add` entirely — a reloaded locked door would
silently stop working, with no compile error and no exception, just a door
that quietly stopped checking `IsLocked`.

So loading a tree is two passes, done in the repository (not EF Core's
automatic graph loading):

1. Query all `Things` and `Behaviors` rows under the requested root
   (recursively, by `ParentId`). Construct each `Thing` and each `Behavior`
   as bare objects with their own scalar properties populated by EF Core,
   but not yet attached to anything.
2. Walk the constructed set and call the real domain APIs: `thing.Behaviors
   .Add(behavior)` for each behavior (triggers `OnAddBehavior` normally —
   this is the actual mechanism, not a workaround), and a `Thing`-tree
   attach for parent/child structure (see below), plus resolving each
   shadow-FK `Guid` reference (`Destination`, `RequiredKey`) to the matching
   already-constructed `Thing` from this same load.

`Thing.Add` itself publishes a cancellable `AddChildEvent` — appropriate for
a *new* pickup/move during play, wrong for reconstructing state that already
existed (nothing should get a "you enter the room" broadcast or a chance to
veto a room's own saved contents on every server boot). `Thing` gains an
internal `AttachLoadedChild` method (direct parent/child wiring, no event
publish) for this purpose, visible to `SharpMud.Persistence` via
`InternalsVisibleTo` — not part of the public API, not for game logic.

`ExitBehavior.Destination`/`LockableBehavior.RequiredKey` change from
`required init` to plain mutable `set` to allow this second-pass resolution
(an EF Core materialization / late-binding tradeoff — see Open Items).

## SQLite Path

`SHARPMUD_DB_PATH` env var, following the same precedence pattern as
`HostOptions` (`SHARPMUD_MODE`/`SHARPMUD_TELNET_PORT`) — CLI arg, then env
var, then a default of `./sharpmud.db`. Deploying the Docker container with
this pointed at a mounted volume is required for the container's data to
actually survive a redeploy — not solved here, flagged in
[deployment.md](deployment.md).

## Write Frequency (revised scope)

`SPEC.md`/this doc's original draft said "persist immediately on every
state-changing action." **What's actually implemented**: whole-world
snapshot save on graceful shutdown, plus a per-player save on disconnect
(`SessionLoop`'s `finally` block — wrapped around the whole method, not just
the happy path, so a save-during-cancellation still runs; uses
`CancellationToken.None` for the save itself, since the loop's own token is
already cancelled by the time shutdown reaches it). This covers the common
case a containerized deployment cares about — a graceful redeploy/restart
doesn't lose anything — but an ungraceful crash between saves loses whatever
changed since the last save. Tightening this to persist-on-every-mutation
(every combat round, every wander tick, every `get`/`drop`) is real
remaining work, not done in this pass — see Open Items. This is a genuine,
deliberate scope reduction from the original plan, not an oversight.

**Graceful shutdown is triggered via `PosixSignalRegistration` for both
`SIGINT` and `SIGTERM`, not `Console.CancelKeyPress`.** `CancelKeyPress` only
ever catches `SIGINT` (Ctrl+C) — it never catches `SIGTERM`, which is exactly
what `docker stop`/Kubernetes send on a graceful shutdown, i.e. the actual
scenario this whole design exists for. This was caught during live testing,
not code review: `CancelKeyPress` was also observed not firing reliably
without a TTY attached (relevant since a container has none), independent of
the `SIGTERM` gap. See `src/SharpMud.Host/Program.cs`.

## Verified

Live end-to-end, not just unit tests: ran the Telnet server against a real
SQLite file, connected, created a character, equipped a weapon, moved to
another room, picked up an item; sent `SIGTERM` and confirmed the process
exited (not hung — see the `PosixSignalRegistration` note above, found by
this exact test); restarted the process against the same DB file; confirmed
the log reported "Loaded persisted world" (not a fresh rebuild); reconnected
with the same character name and confirmed: spawned back in the room they'd
moved to (not the hub start room — this exercises `SessionLoop` using
`player.Parent`, not a fixed starting room, for the reconnect description);
both items were still in inventory; the previously-equipped sword now showed
as carried rather than worn (the known `EquippedBehavior` gap, confirmed
in practice, not just documented); moving through an exit correctly
resolved to the right destination room (confirms `ExitBehavior.Destination`
shadow-FK resolution survives a real reload, not just the in-process test
suite).

Also: `ThingRepositoryTests` (`SharpMud.Persistence.Tests`) round-trips a
room+exit pair, a player with `StatsBehavior`/`CombatantBehavior`/a carried
item (exercises the cross-assembly Ruleset.Classic mapping contributor), a
locked exit with a required key (nullable `Thing` reference resolution),
`FindPlayerByNameAsync` (including that it correctly ignores non-player
`Thing`s with a matching name), and that a second `SaveTreeAsync` call for
the same root correctly reflects updated state (validates the delete-and-
reinsert approach doesn't throw "already tracked" on a second save of live
in-memory objects, and doesn't leave stale rows behind).

## Open Items

- Persist-on-every-mutation (combat rounds, wander ticks, individual item
  moves) instead of save-on-shutdown/-disconnect only — the bigger
  remaining gap, see Write Frequency above.
- `EquippedBehavior.Equipped` (which slot a carried item is worn in) isn't
  persisted — confirmed as a real, live gap during end-to-end testing, not
  just a theoretical one. `PlayerBehavior.Aliases` similarly isn't persisted
  but has no consuming command yet, so it's lower-priority.
- `Behavior.PersistenceKey` is a surrogate key added to the domain base
  class purely so EF Core TPH has something to key rows on — a small
  concession of persistence concerns leaking into `SharpMud.Engine`'s
  domain model. Alternative considered: a composite `(ThingId,
  discriminator)` key requiring no new domain property, rejected as more
  entangled with EF Core's discriminator machinery for a first pass.
- Procedural frontier generation (`world-model.md`) will need
  `SaveManyAsync`-style batched writes for a freshly generated chunk of
  rooms — not designed yet, frontier generation itself is still deferred.
- `MultipleParentsBehavior`/secondary parents are not persisted (only
  `Parent`/`Children`) — consistent with it having no real consumer yet
  (see engine-vs-ruleset.md).
- Schema/migration strategy: still drop-and-recreate
  (`EnsureDeleted`+`EnsureCreated`) during early dev, per the original
  decision — revisit once there's real player data worth preserving across
  schema changes. Boot only calls `EnsureCreatedAsync` (never
  `EnsureDeletedAsync`), so a genuinely changed C# model against an existing
  `.db` file needs the file deleted by hand during dev, not an automatic wipe.
- `ThingRepository` reconstructs the entire stored world into memory on every
  `LoadTreeAsync`/`FindPlayerByNameAsync` call — fine at hand-built-hub scale,
  not yet a scoped/paginated load suitable for a large world.
- Concurrent `SaveTreeAsync` calls (e.g. two players disconnecting at once)
  rely on SQLite's own single-writer file locking; not independently
  stress-tested.
