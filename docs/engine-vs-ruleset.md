# Engine vs. Ruleset

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs, and
[research/wheelmud-findings.md](research/wheelmud-findings.md) for the prior
art (WheelMUD) this design is adapted from, with code citations.

This doc supersedes the concrete `Room`/`Player`/`Npc`/`Item`/`Exit` classes
described in [world-model.md](world-model.md), [character.md](character.md),
and [combat.md](combat.md) — those docs describe the *shape* of the game
concepts; this doc describes how they're actually represented now (`Thing` +
`Behavior`) and which project owns which behaviors.

## Why

Original goal (`SPEC.md`) was one game, one ruleset. That's changed: sharp-mud
should support a *different* game (different stats, different combat math,
different content) being built on the same engine without forking it. That
means `SharpMud.Engine` can never reference anything ruleset-specific (no
`ArmorClass`, no `Race`, no dice-roll formulas) — only generic primitives that
any ruleset can compose into whatever it needs.

## Project structure (revised)

```
src/
  SharpMud.Engine/            Thing, Behavior, event system, generic behaviors
                               (Room/Area/Exit/Lockable/Player-identity/Npc-marker/
                               Wearable/Container), command pipeline, session
                               abstraction, tick loop. Zero ruleset knowledge.
  SharpMud.Hosting/            Generic-host composition helpers (WorldContext,
                               IWorldBuilder, IPlayerFactory, SessionLoop/LoginFlow,
                               AddSharpMud* extensions). Ruleset-agnostic.
  SharpMud.Persistence/        EF Core repositories, provider-agnostic.
                               References Engine only.
  SharpMud.Adapters.Cli/       Local stdin/stdout ISession. References Hosting.
  SharpMud.Ruleset.Rpg/        Reusable RPG scaffolding tier (ADR-0008): CombatantBehavior,
                               ICombatResolver/CombatResolver, ICombatManager/CombatManager,
                               ICombatOutcomeHandler (a ruleset's XP-award/death-penalty/
                               respawn hook), AttackCommand/FleeCommand, IDiceRoller over
                               IRandomSource, AddSharpMudRpgRuleset(...). References
                               Engine/Hosting/Persistence. No ruleset-flavor knowledge
                               (no Race/CharacterClass/stat blocks) - not runnable on its
                               own, same as Microsoft.EntityFrameworkCore.Relational isn't
                               a database.
  SharpMud.Ruleset.Basic/      Minimal concrete leaf ruleset built on SharpMud.Ruleset.Rpg
                               (ADR-0008): a plain numeric BasicStatsBehavior (no Race/
                               CharacterClass), BasicWorldBuilder (a small default world
                               with a fightable NPC), BasicPlayerFactory,
                               BasicCombatOutcomeHandler, AddSharpMudBasicRuleset(...).
                               The actual "dotnet add package, few lines in Program.cs,
                               run a basic game" quick-start.
samples/
  SharpMud.Samples.Classic/    D&D-flavored sample ruleset: Race/CharacterClass, stats,
                               dice-roll character creation, hand-built hub world content,
                               plus the composition root (Program.cs) that references
                               everything. Built on SharpMud.Ruleset.Rpg for combat/
                               encounter scaffolding rather than owning it directly.
```

Dependency direction is stricter than before: a ruleset like the sample
depends on `Engine`/`Hosting` only (never the reverse). Nothing under `src/`
is allowed to know about a specific ruleset — this is what makes "swap the
ruleset" mean something. A different game would be its own sample/consumer
project (e.g. `SharpMud.Samples.SciFi`), referencing `SharpMud.Engine`/
`SharpMud.Hosting` the exact same way, with its own composition root wiring
it in instead of `SharpMud.Samples.Classic`. See
[ADR-0006](adr/0006-nuget-package-distribution.md)/
[PLAN-0006](plans/0006-nuget-package-distribution.md) for how this split is
now distributed as NuGet packages (`SharpMud.*`) plus a sample consumer,
rather than a single solution with one hardcoded ruleset.

## `Thing` + `Behavior`

```csharp
namespace SharpMud.Engine;

public readonly record struct ThingId(Guid Value)
{
    public static ThingId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
```

`ThingId` replaces `RoomId`/`PlayerId`/`NpcId`/`ItemId`/`AreaId` — a single
`Thing` can play more than one role at once (a player is a container of items,
a room is a container of players and exits), so per-role ID types stopped
making sense.

`AccountId` no longer exists — it was introduced when accounts/auth was
designed around external OAuth with a separate `Account` entity owning
multiple characters. That design was reversed to username/password with one
character per login (see [accounts-auth.md](accounts-auth.md)), so there's
no auth identity living outside the player `Thing` — `PlayerBehavior.Username`/
`PasswordHash` cover it directly, and `AccountId.cs` was deleted when that
was implemented.

```csharp
public sealed class Thing
{
    public required ThingId Id { get; init; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public Thing? Parent { get; private set; }
    public IReadOnlyList<Thing> Parents { get; }       // Parent + secondary parents (MultipleParentsBehavior)
    public IReadOnlyList<Thing> Children { get; }

    public BehaviorManager Behaviors { get; }
    public ThingEvents Events { get; }

    public Thing(params Behavior[] behaviors);

    public T? FindBehavior<T>() where T : Behavior;
    public bool HasBehavior<T>() where T : Behavior;

    public bool Add(Thing thing);      // publishes a cancellable AddChildEvent first
    public bool Remove(Thing thing);   // publishes a cancellable RemoveChildEvent first
}
```

`Name`/`Description` live directly on `Thing` (not a behavior) since almost
every object needs them for `look`/`examine` — this is the one concession to
"universal" state, matching how WheelMUD's `IIdentifiable` works.

```csharp
public abstract class Behavior
{
    public Thing? Parent { get; private set; }
    internal void SetParent(Thing? newParent); // calls OnAdd/OnRemoveBehavior

    protected virtual void OnAddBehavior() { }     // subscribe to Parent.Events here
    protected virtual void OnRemoveBehavior() { }  // unsubscribe here
}

public sealed class BehaviorManager
{
    public void Add(Behavior behavior);
    public void Remove(Behavior behavior);
    public T? FindFirst<T>() where T : Behavior;
    public IEnumerable<T> FindAll<T>() where T : Behavior;
}
```

## Event system (simplified from WheelMUD)

```csharp
public enum EventScope { SelfOnly, SelfDown, ParentsDown }

public abstract class GameEvent
{
    public required Thing ActiveThing { get; init; }
}

public abstract class CancellableGameEvent : GameEvent
{
    public bool IsCanceled { get; private set; }
    public string? CancelReason { get; private set; }
    public void Cancel(string reason) { IsCanceled = true; CancelReason = reason; }
}

public sealed class ThingEvents
{
    public void SubscribeRequest(Action<Thing, CancellableGameEvent> handler);
    public void UnsubscribeRequest(Action<Thing, CancellableGameEvent> handler);
    public void SubscribeEvent(Action<Thing, GameEvent> handler);
    public void UnsubscribeEvent(Action<Thing, GameEvent> handler);

    public void PublishRequest(CancellableGameEvent evt, EventScope scope);
    public void PublishEvent(GameEvent evt, EventScope scope);
}
```

One generic pub/sub instead of WheelMUD's ~8 duplicated category-specific
delegate pairs (`CombatEvent`/`CombatRequest`/`MovementEvent`/...). Handlers
pattern-match on the concrete event type themselves (`if (evt is
EnterExitEvent enter) { ... }`) — new event categories don't require touching
`ThingEvents`. `PublishRequest` stops propagating the instant a handler calls
`Cancel`; `PublishRequest`/`PublishEvent` both walk `Children`
breadth-first for `SelfDown`, or `Parents` then `SelfDown` from each for
`ParentsDown`, matching WheelMUD's traversal exactly (see
[research/wheelmud-findings.md](research/wheelmud-findings.md) §5).

`Thing.Add`/`Remove` publish a cancellable `AddChildEvent`/`RemoveChildEvent`
before mutating — any `Behavior` anywhere in the hierarchy can veto (a locked
exit canceling a move, a full container canceling an item pickup).

## Engine-level behaviors (`SharpMud.Engine`, ruleset-agnostic)

- `RoomBehavior` — marks a `Thing` as a room. Minimal; exits are discovered
  via `FindAll<ExitBehavior>()` over `Children`, not a dedicated list.
- `AreaBehavior` — marks a `Thing` as an area; rooms are its `Children`
  (containment, not a separate `AreaId` foreign key).
- `ExitBehavior` — `Direction Direction`, `Thing Destination`. One exit
  `Thing` per direction of travel (see Decisions in the findings doc for why
  this is simpler than WheelMUD's single bidirectional exit +
  `MultipleParentsBehavior`, which is still available as a generic engine
  primitive for cases that do need it).
- `LockableBehavior` — `bool IsLocked`, `bool IsClosed`, `Thing? RequiredKey`.
  Optionally attached to an exit `Thing`; subscribes to move requests and
  cancels them when locked.
- `PlayerBehavior` — identity: `Username`/`PasswordHash` (see
  [accounts-auth.md](accounts-auth.md)), `List<string> Aliases`. No stats —
  those are ruleset-owned (see below).
- `NpcBehavior` — marker only (`bool IsNpc`-equivalent via presence). No
  combat stats.
- `WearableBehavior` — `EquipSlot Slot`. Presence means "this can be worn."
- `EquippedBehavior` — attached to a player-like `Thing`; tracks
  `Dictionary<EquipSlot, Thing?> Equipped`. Carried-but-not-worn items are
  just `Children` of the actor not present in this dictionary's values — no
  separate `Inventory` list needed, since `Thing.Children` already is the
  generic container.
- `WanderingBehavior` — `int WanderChancePercent`. Presence + a registered
  `WanderManager` (`ITickable`, like `CombatManager`) means "this NPC has a
  chance each tick of moving to a random adjacent room." Deliberately
  engine-level, not ruleset-level, since it depends only on `NpcBehavior`/
  `ExitBehavior` — no combat or stat system involved. First real validation
  that the split holds for a whole feature, not just data classes:
  `SharpMud.Samples.Classic.Tests` never had to change for this to work.

## Ruleset-level behaviors (`SharpMud.Samples.Classic`)

- `StatsBehavior` — the D&D-style attributes (`Strength`...`Charisma`),
  `Race`, `CharacterClass`, `Level`, `Experience`, `MaxHitPoints`/
  `CurrentHitPoints`/etc. Everything [character.md](character.md) describes.
- `CombatantBehavior` — `ArmorClass`, `DamageMin`/`DamageMax`. What
  [combat.md](combat.md)'s `ICombatant` used to be an interface `Player`/`Npc`
  implemented is now a behavior any `Thing` can carry (a hostile plant, a
  turret — anything the ruleset wants to fight).
- Combat resolution (`ICombatResolver`/`CombatResolver`), `ICombatManager`/
  `CombatManager`, and the `kill`/`attack`/`flee` commands all move here
  unchanged in logic — only their dependency on `Player`/`Npc` becomes a
  dependency on `Thing` + `FindBehavior<CombatantBehavior>()`.

## Command pipeline changes

`CommandContext` changes from `(Player Actor, Room CurrentRoom, ...)` to
`(Thing Actor, Thing CurrentRoom, ...)`. Commands that need ruleset data
(`AttackCommand` needing `CombatantBehavior`) do `ctx.Actor.FindBehavior<...>()`
and fail gracefully if absent — this is the actual mechanism that keeps
`AttackCommand` in the sample ruleset rather than `Engine`: it's the first
command to depend on a ruleset-specific behavior type.

Adopted from WheelMUD (see findings doc §3): a lightweight `CommandGuards`
static helper covers repeated preconditions (`RequiresAtLeastOneArgument`,
etc.) so commands don't hand-roll `if (ctx.Args.Count == 0) { ...; return; }`
every time. Not a full `Guards()`-before-`Execute()` split on `ICommand`
itself — that's more ceremony than our command count currently justifies;
revisit if guard logic keeps growing.

## Sequence: Player types "north" (revised)

1. `ICommandParser.Parse("north")` → resolves to a `MoveCommand` bound to
   `Direction.North`, same as before.
2. `MoveCommand` finds the matching `ExitBehavior` via
   `ctx.CurrentRoom.Behaviors.FindAll<ExitBehavior>()`, filtered by direction.
3. Not found → `"You can't go that way."` (unchanged message, same guard
   shape).
4. Found → publishes a cancellable move-request event
   (`EventScope.SelfOnly` on the exit `Thing`) before doing anything. If a
   `LockableBehavior` on the exit cancels it (locked), the command sends the
   cancel reason and stops — the command itself no longer knows or cares
   *why* a move might be blocked.
5. Not canceled → `ctx.CurrentRoom.Remove(actor)` /
   `exit.Destination.Add(actor)` (both themselves publish cancellable
   Add/RemoveChildEvents — a second veto point any future behavior could hook,
   e.g. a room that's full).
6. Room occupants notified, destination description sent — same as before.

## Open Items

- `MultipleParentsBehavior` is implemented as a generic engine primitive but
  not currently used by anything (exits are one-Thing-per-direction instead —
  see Decisions in the findings doc). First real consumer will validate the
  design; revisit if none materializes.
- Whether `AreaBehavior` containment (rooms as `Children` of an area `Thing`)
  replaces or coexists with the procedural frontier's coordinate-based
  addressing (`world-model.md`) isn't resolved yet — frontier generation is
  still a deferred phase.
- Data-driven content loading (`world-model.md` phase 2) will need to
  serialize `Thing`+`Behavior` graphs; the JSON shape isn't designed yet.
- No `AssemblyLoadContext`-based dynamic ruleset loading — see `SPEC.md`
  Deferred/Open Items.
- How an actual external consumer gets `SharpMud.Engine` at all is resolved
  by [ADR-0006](adr/0006-nuget-package-distribution.md)/
  [PLAN-0006](plans/0006-nuget-package-distribution.md): `SharpMud.*` NuGet
  packages plus `SharpMud.Samples.Classic` as a reference consumer under
  `samples/`. Implemented — this doc's project-structure listing above
  reflects the current layout.
- `SharpMud.Samples.Classic` owning combat/stats scaffolding directly (not
  just its own D&D-specific content) was addressed by
  [ADR-0008](adr/0008-ruleset-scaffolding-tier.md)/
  [PLAN-0008](plans/0008-ruleset-scaffolding-tier.md): a new
  `SharpMud.Ruleset.Rpg` package for the reusable scaffolding, a new minimal
  `SharpMud.Ruleset.Basic` package as a concrete quick-start leaf sibling to
  Classic — both built directly on `SharpMud.Ruleset.Rpg`, neither depending
  on the other — with Classic staying a still-unpackaged, richer sample.
  Implemented — this doc's project-structure listing above reflects the
  current layout.
