# Customizing sharp-mud

!!! note "This page is a work in progress"
    It covers the extension points that exist today. More detail (data
    persistence deep-dive, transport customization, world generation) is
    coming as those areas stabilize.

sharp-mud has no subclassing-based extensibility — there's no `Player`
class to override, no `Room` class to inherit from. Everything is a `Thing`
(a bare node in the world tree — an id, a name, a location) with zero or
more `Behavior`s attached to it. "What kind of thing is this" is answered
by which behaviors it carries, not by which class it is:

```csharp
var caveRat = new Thing { Id = ThingId.New(), Name = "cave rat" };
caveRat.Behaviors.Add(new NpcBehavior());
caveRat.Behaviors.Add(new CombatantBehavior { MaxHitPoints = 6, ArmorClass = 8, /* ... */ });
caveRat.Behaviors.Add(new WanderingBehavior());
```

That one `Thing` is simultaneously an NPC, something you can fight, and
something that wanders around — because it carries those three behaviors,
not because it's some `NpcCombatantWanderer` subclass. Adding a new kind of
game object almost always means writing a new `Behavior`, not a new class
in an inheritance tree.

## Where does X belong?

The three-tier package split (see [Rulesets](rulesets.md)) exists to answer
this question. As a rule of thumb:

| If it's... | It belongs in... |
|---|---|
| True for every conceivable MUD, regardless of genre (containment, movement, exits, the tick loop) | `SharpMud.Engine` — you don't touch this |
| A shape shared by RPG-style games specifically (hit points, to-hit rolls, an encounter tracker) but not tied to any one game's numbers | `SharpMud.Ruleset.Rpg`, or your own equivalent package if you're not building an RPG |
| Specific to *your* game (your stat block, your world content, your commands, your dice formulas) | Your own project |

Genuinely volatile, game-balance-sensitive content — exact damage formulas,
race/class stat tables, hand-built world content — is deliberately kept out
of any shared package, even `Ruleset.Rpg`. That kind of content changes far
more often than infrastructure code, and turning it into a versioned public
API would make ordinary game-balance tuning a breaking change. Keep it in
your own project.

## Extension points

These are the seams `SharpMud.Hosting` (and `SharpMud.Ruleset.Rpg`, if
you're using it) expose for you to plug into:

| Interface | Registered via | Purpose |
|---|---|---|
| `IWorldBuilder` | `AddSharpMudWorld<T>()` | Build a brand-new world when nothing is persisted yet, and locate the starting room after a reload |
| `IPlayerFactory` | `AddSharpMudPlayerFactory<T>()` | Create a fresh player `Thing` at character creation |
| `IBehaviorMappingContributor` | `AddSingleton<IBehaviorMappingContributor, T>()` | Register EF Core mapping for your own `Behavior` subtypes |
| `ICombatOutcomeHandler` (if using `Ruleset.Rpg`) | `AddSharpMudRpgRuleset<T>()` | Award XP/rewards on a win, apply a death penalty and pick a respawn destination on a loss |
| `ICommand` | Registered inside your `AddSharpMudRuleset(...)`/`AddSharpMudRpgRuleset(...)` callback | Add a new player-facing verb |

### Adding your own `Behavior`

A new `Behavior` subtype needs two things beyond the class itself if it's
going to be saved/loaded:

1. An `IEntityTypeConfiguration<T>` describing its EF Core mapping (most
   behaviors with only plain scalar properties need an empty
   `Configure(...)` — the default mapping is already correct).
2. An `IBehaviorMappingContributor` that applies it — typically just:

```csharp
public sealed class MyBehaviorMappingContributor : IBehaviorMappingContributor
{
    public void ConfigureBehaviors(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyBehaviorMappingContributor).Assembly);
}
```

Register it once: `services.AddSingleton<IBehaviorMappingContributor, MyBehaviorMappingContributor>();`.
Behaviors persist via a single shared table with a type discriminator, not
one table per behavior type — skipping this step doesn't fail at compile
time, it fails the first time you try to save or load a `Thing` carrying
that behavior.

### Adding your own command

Implement `ICommand` (a verb, optional aliases, and an `ExecuteAsync`), then
register it inside whichever ruleset-registration callback you're already
using — see [Rulesets](rulesets.md#putting-it-together) for why that has to
be one callback, not several independent calls.

`SharpMud.Engine` also ships two ready-made, opt-in command sets built on
this same registration mechanism — moderation (`boot`/`mute`/`ban`/...) and
world-building (`dig`/`tunnel`/`describe`) — see
[Moderation & World Building](moderation-and-world-building.md).

## What's next

Planned additions to this page: a deeper look at the `Thing`/`Behavior`
event system (how behaviors observe and veto changes elsewhere in the world
tree), guidance on choosing a persistence provider, and a walkthrough of
building a transport adapter beyond CLI/Telnet.
