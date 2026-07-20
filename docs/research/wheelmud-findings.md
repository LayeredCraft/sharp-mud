# WheelMUD Research Findings

Source reviewed: `/Users/ncipollina/source/repos/davidrieman/WheelMUD/src` (external
reference repo, not part of this project). This doc captures what we found and
which patterns we're adopting into sharp-mud. See [README.md](../README.md) for
how this relates to the rest of `docs/`.

**Context for the decision that follows**: WheelMUD is built as a redistributable
*engine* — `WheelMUD.Core` has zero dependency on any specific game's rules, and
`WarriorRogueMage` is a complete separate ruleset (stats, races, skills, combat,
character creation) built entirely on Core's primitives to prove the engine
supports a real game without modifying it. Sharp-mud has adopted the same goal
(see `SPEC.md`) — design so that a different ruleset/game can be built on the
same engine without forking it — so several of these patterns are being adopted
directly rather than just "considered."

## 1. Top-level structure

13 assemblies. The load-bearing split: `WheelMUD.Core` (engine) is completely
separate from `WarriorRogueMage` (a sample ruleset), and `WheelMUD.Universe`
(default world content) is separate from both.

```
src/
├── Interfaces/            WheelMUD.Interfaces
├── Utilities/              WheelMUD.Utilities
├── Core/                    WheelMUD.Core — Thing, Behaviors, Events, CommandSystem, ManagerSystems, Session
├── Actions/                 WheelMUD.Actions — concrete GameAction verbs
├── ConnectionStates/        WheelMUD.ConnectionStates — login/char-creation/playing state machine
├── Effects/                  WheelMUD.Effects — buffs/debuffs
├── Server/                   WheelMUD.Server — telnet networking, MCCP/MXP/NAWS
│   └── Telnet/
├── Data/, Data.RavenDb/      persistence (RavenDB document store)
├── Universe/                 default world/area content bootstrap
├── WarriorRogueMage/         sample ruleset (stats/skills/races/combat) built entirely on Core
├── Main/, ServerHarness/     entry point / dev harness
└── Tests/
```

## 2. Core entity model — `Thing` + `Behavior` composition

`Core/Thing.cs` — `Thing` is **sealed**. Every game object (player, room, item,
mobile, exit, area) is the same class; differentiation is entirely by attached
`Behavior` instances, never subclassing:

```csharp
[JsonObject(IsReference = true)]
public sealed class Thing : IThing, IDisposable, IIdentifiable
{
    public Thing(params Behavior[] behaviors) {
        Eventing = new ThingEventing(this);
        Behaviors = new BehaviorManager(this);
        foreach (var behavior in behaviors) { behavior.SetParent(this); Behaviors.Add(behavior); }
    }

    public ThingEventing Eventing { get; }
    public string Id { get; set; }
    public Thing Parent { get; private set; }
    public List<Thing> Parents { get; }
    public ReadOnlyCollection<Thing> Children { get; }
    public BehaviorManager Behaviors { get; }
    public Dictionary<string, ContextCommand> Commands { get; }

    public T FindBehavior<T>() where T : Behavior => Behaviors.FindFirst<T>();
    public bool HasBehavior<T>() where T : Behavior => Behaviors.FindFirst<T>() != null;
    public bool Add(Thing thing) { /* goes through cancellable AddChildEvent */ }
    public bool Remove(Thing thing) { /* goes through cancellable RemoveChildEvent */ }
}
```

Class remark states the philosophy directly: *"a 'player' is a Thing that has a
PlayerBehavior (and likely a UserControlledBehavior, and so on)."*

`Core/Behaviors/Behavior.cs`:

```csharp
public abstract class Behavior : IPersistsWithPlayer
{
    public Thing Parent { get; private set; }
    public void SetParent(Thing newParent) {
        if (Parent != newParent) {
            if (Parent != null) OnRemoveBehavior();
            Parent = newParent;
            if (newParent != null) OnAddBehavior();
        }
    }
    protected virtual void OnAddBehavior() { }   // typically subscribes to Parent's ThingEventing here
    protected virtual void OnRemoveBehavior() { }
}
```

Built-in behaviors: `RoomBehavior`, `AreaBehavior`, `ExitBehavior`,
`PlayerBehavior`, `MobileBehavior`, `LivingBehavior`, `MovableBehavior`,
`UserControlledBehavior`, `MultipleParentsBehavior`, `OpensClosesBehavior`,
`WanderingBehavior`. Ruleset-specific behaviors extend the same pattern outside
Core, e.g. `WarriorRogueMage/Behaviors/{SkillsBehavior,WieldableBehavior,MountBehavior}.cs`.

**Adopted for sharp-mud**, with two deliberate deviations — see Decisions below.

## 3. Command/action system

`Core/CommandSystem/GameAction.cs`:

```csharp
public abstract class GameAction
{
    protected enum CommonGuards { InitiatorMustBeAlive, InitiatorMustBeConscious, InitiatorMustBeStanding,
        InitiatorMustBeBalanced, InitiatorMustBeMobile, InitiatorMustBeAPlayer,
        RequiresAtLeastOneArgument, RequiresAtLeastTwoArguments }

    public abstract void Execute(ActionInput actionInput);
    public abstract string Guards(ActionInput actionInput);   // error message, or null if OK
}
```

`Guards()` runs before `Execute()` and separates "can I do this" from "do it" -
a `CommonGuards` enum covers the repeated preconditions (must have args, must
be alive/conscious, etc.) so individual actions don't hand-roll them.

Actions register via MEF export attributes, not a manual switch statement -
example, `Actions/Communicate/Say.cs`:

```csharp
[CoreExports.GameAction(0)]
[ActionPrimaryAlias("say", CommandCategory.Communicate)]
[ActionAlias("'", CommandCategory.Communicate)]
[ActionSecurity(SecurityRole.player | SecurityRole.mobile)]
public class Say : GameAction { ... }
```

`CommandManager.Instance.MasterCommandList` is populated at composition time by
scanning `[CoreExports.GameAction]`-exported types (see §8). `CommandCreator`
resolves aliases against it, falling back to per-Thing **context commands**
(`ContextCommand`, `Thing.Commands`) contributed dynamically by nearby objects
(e.g. an `ExitBehavior` registers "north" only while relevant).

`CommandProcessor` runs command execution on its own worker thread, pulling
queued `ActionInput` and invoking guard-check then execute with **exception
isolation per command** - one bad command can't crash the game loop.

**Adopted**: the `Guards()`-before-`Execute()` split, and per-command exception
isolation. **Not adopted**: MEF-based registration (see Decisions) or a
separate command-processing thread (unnecessary at our scale; async/await on
the existing loop is sufficient).

## 4. Networking / session model

`Core/Session/Session.cs` and `Core/Session/SessionState.cs`:

```csharp
public class Session : IController, ISubSystem
{
    public Thing Thing { get; set; }
    public SessionState State { get; private set; }
    public void SetState(SessionState newState) { State = newState; newState?.Begin(); }
    public void ProcessCommand(string input) => State.ProcessInput(input);
}

public abstract class SessionState
{
    protected Session Session { get; set; }
    public abstract void ProcessInput(string command);
    public abstract OutputBuilder BuildPrompt();
    public virtual void Begin() { Session.WritePrompt(); }
}
```

Concrete states (`WheelMUD.ConnectionStates`) implement login, character
creation (multi-step, its own sub-state machine), and the "playing" state that
hands input to the command pipeline.

Telnet transport (`Server/Telnet/`) implements the IAC state machine plus
MCCP (compression), MXP, and NAWS (window-size negotiation) - this is fiddly
protocol code worth reading directly rather than re-deriving from the RFC when
we build our own Telnet adapter.

**Adopted (planned, not yet built)**: the `SessionState` shape, for
`docs/accounts-auth.md`'s login flow (now username/password, not the OAuth
device-code flow originally planned when this note was written — see
accounts-auth.md's revision) and `docs/character.md`'s character creation.
**Reference, not adopted verbatim**: the Telnet protocol code, to consult
when `docs/networking.md` phase 2 is built.

## 5. Event system — propagating `GameEvent`/`CancellableGameEvent`

`Core/Events/EventBase.cs`:

```csharp
public class GameEvent {
    public Thing ActiveThing { get; }
    public Thing RootLocation { get; }
    public SensoryMessage SensoryMessage { get; }
}

public class CancellableGameEvent : GameEvent {
    public bool IsCanceled { get; private set; }
    public void Cancel(string cancelMessage) { IsCanceled = true; /* writes to the initiator's session */ }
}
```

`ThingEventing` (`Core/Events/ThingEventing.cs`) gives every `Thing` paired
**Request** (cancellable, "may I?") and **Event** (non-cancellable, "this
happened") delegates per category (Combat, Movement, Communication, Misc).
`EventScope` (`Core/Events/EventScope.cs`) controls propagation direction:
`SelfOnly`, `SelfDown` (breadth-first through descendants via a `Queue<Thing>`
to avoid stack overflows), `ParentsDown` (walk up, then broadcast down from
each ancestor). A cancellable request stops propagating the instant
`IsCanceled` flips:

```csharp
private void OnRequest(Func<ThingEventing, CancellableGameEventHandler> handlerSelector,
    CancellableGameEvent e, bool cascadeEventToChildren)
{
    var requestTargetQueue = new Queue<Thing>();
    requestTargetQueue.Enqueue(owner);
    while (requestTargetQueue.Count > 0) {
        var target = requestTargetQueue.Dequeue();
        var handler = handlerSelector(target.Eventing);
        if (handler != null) { handler(target, e); if (e.IsCanceled) break; }
        if (cascadeEventToChildren) foreach (var child in target.Children) requestTargetQueue.Enqueue(child);
    }
}
```

Behaviors subscribe in `OnAddBehavior()`. `Thing.Add`/`Remove` drive
`AddChildEvent`/`RemoveChildEvent` through this exact pipeline, so any Behavior
in the room hierarchy can veto a move (a locked door canceling `EnterEvent`).

**Adopted, simplified**: a single generic `Publish<TEvent>`/cancel mechanism
instead of WheelMUD's ~8 duplicated category-specific delegate pairs - see
Decisions below for why.

## 6. World/room model — hierarchical containment

No separate Room/Area/Exit class hierarchy - it's `Thing` + Behavior:

- `RoomBehavior` - adds furnishings/description state to a Thing.
- `ExitBehavior` - the exit is itself a **child Thing** inside a room, holding
  `List<ExitDestinationInfo> Destinations`. Two-way exits use
  `MultipleParentsBehavior` to place the same exit Thing in both rooms at once.
- `MultipleParentsBehavior` (`Core/Behaviors/MultipleParentsBehavior.cs`)
  supplements the single `Thing.Parent` with `List<Thing> SecondaryParents`,
  so `Thing.Parents` can return more than one container.

```csharp
public class MultipleParentsBehavior : Behavior
{
    public List<Thing> SecondaryParents { get; } = new();
    public void AddParent(Thing newParent) {
        var thing = Parent;
        if (thing.Parent == null) { thing.RigParentUnsafe(newParent); return; }
        if (thing.Parent == newParent) return;
        if (!SecondaryParents.Contains(newParent)) SecondaryParents.Add(newParent);
    }
}
```

**Adopted in full**, including promoting Exits to child Things and
`MultipleParentsBehavior` for two-way exits - see Decisions.

## 7. Persistence — RavenDB document store, tree-shaped save/load

`Data/Repositories/DocumentRepository.cs`:

```csharp
public static class DocumentRepository<T> where T : IIdentifiable, new() {
    public static void SaveTree(T mainDocument, Func<T, IEnumerable<T>> childDocumentFinder) {
        using var session = Helpers.OpenDocumentSession();
        AddChildTreeToSession(session, mainDocument, childDocumentFinder); // children saved before parents
        session.SaveChanges();
    }
}
```

`Thing.Save()` calls `DocumentRepository<Thing>.SaveTree(this, t =>
t.GetPersistableChildren())`. Parent references are `[JsonIgnore]`d (avoids
reference cycles in the document); only child-Id lists are persisted, and
`Thing.OnDeserialized` calls `RepairParentTree()` on load.

**Not directly adopted** - our persistence approach is EF Core against a
relational/document provider (`docs/persistence.md`), which handles
relationships via its own change tracker rather than manual tree-walking. The
transferable lesson (children must exist before parent save; don't serialize
back-references) is noted but doesn't require new code - EF Core's navigation
properties handle this differently.

## 8. Plugin/extensibility — MEF (`System.ComponentModel.Composition`)

`Core/DefaultComposer.cs`:

```csharp
static DefaultComposer() {
    var assembly = Assembly.GetExecutingAssembly();
    var asmCatalog = new AssemblyCatalog(assembly);
    var dirCatalog = new DirectoryCatalog(Path.GetDirectoryName(assembly.Location)); // picks up any DLL dropped in bin
    Container = new CompositionContainer(new AggregateCatalog(asmCatalog, dirCatalog));
}
```

Priority-based override: `[CoreExports.GameAction(priority)]` implements
`IExportWithPriority`; the highest non-negative priority wins, tie-broken by
most-recently-modified assembly. Core exports priority 0, `WarriorRogueMage`
exports at priority 100 - a downstream game overriding one command exports at
200 and wins, without touching either assembly.

**Not adopted** - `System.ComponentModel.Composition` is legacy tech, largely
superseded in modern .NET. We get the important property (ruleset assemblies
plug into the engine without the engine referencing them) from a reflection
scan over loaded assemblies plus standard DI registration instead - see
Decisions.

## 9. Combat

`Core/GameEngine/ICombat.cs` is a thin interface; the entire ruleset lives in
`WarriorRogueMage/Combat/WRMCombat.cs`. Combat doesn't get a special dispatch
mechanism - it's a `GameAction` (attack command) that raises `AttackEvent`/
`DeathEvent` through the same `ThingEventing`/`EventScope` pipeline as
everything else.

**Adopted in principle**: combat-as-ruleset (not engine) is exactly the
direction sharp-mud's combat math needs to move for the engine/ruleset split
to mean anything - see Decisions and the Follow-up Work section.

## 10. Dependency injection / composition

No modern DI container - MEF for pluggable parts, plus manual `XManager.Instance`
static singletons for the "one true instance" managers (`ThingManager`,
`SessionManager`, `CommandManager`, etc).

**Not adopted** - sharp-mud already uses constructor-injected DI
(`Microsoft.Extensions.DependencyInjection`, `docs/architecture.md`), which is
strictly better for testability than static singletons; our test suite already
depends on it (NSubstitute mocking constructor-injected interfaces). No reason
to regress here.

---

## 11. Security roles / moderation — `ActionSecurityAttribute`, `Actions/Admin/`

Source dive conducted for [ADR-0005](../adr/0005-security-role-model-and-moderation-commands.md)
(Slice 3 of the reconciliation roadmap). `Core/Attributes/ActionSecurityAttribute.cs`
+ `Actions/Admin/` (16 files): a `[Flags] enum SecurityRole` (`mobile` /
`item` / `room` / `tutorialPlayer` / `player` / `helper` / `married` /
`minorBuilder` / `fullBuilder` / `minorAdmin` / `fullAdmin` / `all`, 12 real
values), an `[ActionSecurity(SecurityRole.x)]` class attribute on each
Action, reflected off at registration time into a `Command.SecurityRole`
field, gated at dispatch by a bitwise-AND check
(`command.SecurityRole & user.SecurityRoles`) in `CommandGuard.cs`.
`Actions/Admin/` (`Announce`, `Ban`, `Boot`, `Buff`, `Clone`, `Control`,
`Find`, `GoTo`, `Jail`, `Locate`, `Mute`, `Relinquish`, `RoleGrant`,
`RoleRevoke`, `Spawn`, `Unmute`) are ordinary Actions decorated with
high-tier roles — no separate mechanism from any other command.

---

## Decisions for sharp-mud

Two real forks, resolved as follows (see the "Engine vs. Ruleset" architecture
update for the full design):

1. **Exits become full Things, like WheelMUD.** An exit is a child `Thing`
   (typically inside the room it exits from) carrying an `ExitBehavior`
   (direction + destination) and, for locked/lockable exits, a
   `LockableBehavior`. This buys the same flexibility WheelMUD has - a
   trapped exit, a puzzle door, a one-way slide with its own logic - by
   composing more Behaviors onto the same Thing rather than growing a
   special-cased `Exit` data class. Two-way exits use the same
   `MultipleParentsBehavior` mechanism described in §6, since the exit Thing
   genuinely lives in both rooms' child lists at once.

2. **Ruleset plugins load via assembly-scan + DI, not MEF.** We get the
   "engine doesn't reference ruleset code" property from reflection over
   loaded assemblies (find types tagged `[GameAction]`/`[Behavior]` and
   register them with the DI container at startup) instead of MEF's
   `CompositionContainer`/`DirectoryCatalog`. This keeps the extensibility
   property WheelMUD is built for while staying on the DI story
   `docs/architecture.md` already committed to. True hot-swap-a-DLL-without-
   rebuilding distribution (MEF's `DirectoryCatalog`) is not implemented yet -
   `AssemblyLoadContext`-based dynamic loading can be added later if genuine
   third-party redistribution (not just a separate project reference) is
   needed.

Additionally, the event system is simplified to one generic
`Publish<TEvent>(TEvent evt, EventScope scope) where TEvent : GameEvent`
(with cancellation via an `IsCanceled` flag on a `CancellableGameEvent` base)
rather than WheelMUD's ~8 duplicated category-specific delegate pairs
(`CombatEvent`/`CombatRequest`/`MovementEvent`/... on `ThingEventing`) - same
propagation/cancellation behavior, less boilerplate, and new event categories
don't require adding new delegate pairs to a growing interface.

3. **`Server/Telnet/`'s IAC/Q-Method negotiation is adopted, its byte-parser
   class hierarchy is not** - see
   [ADR-0002](../adr/0002-telnet-protocol-negotiation.md) (status: Proposed)
   for the full record. The RFC-1143 four-state negotiation tracking is
   adopted near-verbatim; the 5-class persistent byte-parser state machine is
   replaced with a single-call parser, since sharp-mud's read loop is already
   one sequential `await` chain per connection. MCCP/MXP/TermType remain
   unreviewed beyond the survey in §Networking above.

4. **The `[Flags] SecurityRole` bitmask and its bitwise-AND gating check are
   adopted from §11's `ActionSecurityAttribute`/`Actions/Admin/` dive;
   attribute+reflection dispatch is not.** sharp-mud gates via a
   hand-rolled Decorator (`RoleGuardedCommand`/`MuteGuardedCommand`
   wrapping an inner `ICommand` at registration time) instead, since
   sharp-mud's `ICommandRegistry.Register(ICommand)` model has no
   DI-container registration step for an attribute-scanning approach (or
   `LayeredCraft.DecoWeaver`, which only intercepts `IServiceCollection`
   registrations) to hook into. Full rationale, the grant/revoke
   hierarchy-accumulation rule, and the command set actually built are in
   [ADR-0005](../adr/0005-security-role-model-and-moderation-commands.md)/
   [PLAN-0005](../plans/0005-security-role-model-and-moderation-commands.md).

Going forward, further reconciliation against WheelMUD (moderation/admin
tooling, session reconnect, world-building commands, and more) is tracked as
a sequenced roadmap in [ADR-0001](../adr/0001-wheelmud-reconciliation-roadmap.md)
rather than as ad hoc additions to this findings doc.
