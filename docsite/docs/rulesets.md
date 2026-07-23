# Rulesets

`SharpMud.Engine` knows nothing about stats, hit points, or combat — on
purpose. It only understands `Thing`/`Behavior` composition, rooms and
exits, the command pipeline, and the tick loop. Every "what kind of game is
this" decision lives above it, in a **ruleset**.

There are three tiers:

```
SharpMud.Engine            Thing/Behavior, events, commands, tick loop.
                            Zero game-rule knowledge.

SharpMud.Ruleset.Rpg        Reusable RPG scaffolding: a fightable-thing
                            behavior, combat resolution, encounter tracking,
                            attack/flee commands, dice rolling. Not a
                            runnable game on its own.

SharpMud.Ruleset.Basic      A small, concrete, runnable game built on
                            Ruleset.Rpg. Your own ruleset (or Classic, the
                            richer reference sample) is a sibling of this,
                            not a layer on top of it.
```

If you've used EF Core, this shape will look familiar:

| EF Core | sharp-mud | Role |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | `SharpMud.Engine` | Core abstractions, no opinions about your data/game |
| `Microsoft.EntityFrameworkCore.Relational` | `SharpMud.Ruleset.Rpg` | Reusable scaffolding for a *shape* of thing (relational databases / RPG-style games) — still not runnable alone |
| `Microsoft.EntityFrameworkCore.Sqlite` | `SharpMud.Ruleset.Basic` | A concrete, runnable leaf you can actually point at something |

Just like you can build a completely different EF Core provider on top of
`Relational` instead of using `Sqlite`, you can build a completely different
game on top of `Ruleset.Rpg` instead of using `Basic` — same combat
scaffolding, your own stats, your own world, your own commands.

## `SharpMud.Ruleset.Basic` — the fastest path to a running game

This is the "install and go" option — a plain numeric stat block, a small
default world with one fightable NPC, and working `kill`/`attack`/`flee`
combat, with no ruleset code of your own required.

```bash
dotnet add package SharpMud.Engine --prerelease
dotnet add package SharpMud.Hosting --prerelease
dotnet add package SharpMud.Persistence.Sqlite --prerelease
dotnet add package SharpMud.Ruleset.Basic --prerelease
dotnet add package SharpMud.Adapters.Cli --prerelease
```

```csharp
using Microsoft.Extensions.Hosting;
using SharpMud.Adapters.Cli;
using SharpMud.Hosting;
using SharpMud.Persistence.Sqlite;
using SharpMud.Ruleset.Basic;

var app = SharpMudApplication.CreateBuilder(args);

app.Services.AddSharpMudSqlitePersistence("./mygame.db");
app.Services.AddSharpMudBasicRuleset();
app.Services.AddSharpMudCliTransport();

var mud = app.Build();
await mud.RunAsync();
```

`dotnet run` gives you a fresh character, two rooms, one fightable NPC, and
persistence across restarts — no `IWorldBuilder`/`IPlayerFactory` to write
yourself.

`AddSharpMudBasicRuleset(...)` takes an optional callback to tune the
starting numbers for a fresh character:

```csharp
app.Services.AddSharpMudBasicRuleset(options =>
{
    options.StartingHitPoints = 30;
    options.StartingArmorClass = 12;
});
```

Swap `SharpMud.Persistence.Sqlite` for `SharpMud.Persistence.DynamoDb`
and/or `SharpMud.Adapters.Cli` for `SharpMud.Adapters.Telnet` as needed —
see [Getting Started](getting-started.md) for how those packages compose.

## `SharpMud.Ruleset.Rpg` — build your own ruleset

`Ruleset.Basic` is one concrete leaf built on `Ruleset.Rpg`'s scaffolding.
If Basic's numbers and world aren't what you want, reference `Ruleset.Rpg`
directly instead and get the same combat plumbing:

- **`CombatantBehavior`** — hit points, armor class, damage range, XP
  reward. Attach it to any `Thing` you want to be fightable (a player, an
  NPC, a hostile plant) — it doesn't require a full character sheet.
- **`ICombatResolver`** — resolves one round: roll to hit vs. armor class,
  roll damage, apply it.
- **`ICombatManager`** — tracks active encounters and advances them once per
  game tick; also drives the `kill`/`attack`/`flee` commands.
- **`IDiceRoller`** — "N dice of M sides plus a modifier," built on the
  engine's `IRandomSource`.
- **`ICombatOutcomeHandler`** — your extension point (see below).

`Ruleset.Rpg` deliberately has **no idea what your character sheet looks
like** and **no idea where a defeated character should respawn** — those
are genuinely your ruleset's decisions, not generic combat mechanics. Both
are expressed through one interface you implement:

```csharp
public interface ICombatOutcomeHandler
{
    // Called when `victor` defeats `defeated` - award XP/rewards here.
    Task OnVictoryAsync(Thing victor, Thing defeated, CancellationToken ct);

    // Called when `defeated` loses to `victor` - apply a death penalty and
    // return where `defeated` respawns.
    Task<Thing> OnDefeatAsync(Thing defeated, Thing victor, CancellationToken ct);
}
```

A minimal implementation, tracking XP on your own stats behavior and always
respawning at the world's starting room:

```csharp
public sealed class MyCombatOutcomeHandler(WorldContext worldContext) : ICombatOutcomeHandler
{
    public Task OnVictoryAsync(Thing victor, Thing defeated, CancellationToken ct)
    {
        var stats = victor.FindBehavior<MyStatsBehavior>();
        var reward = defeated.FindBehavior<CombatantBehavior>()?.ExperienceReward ?? 0;
        if (stats is not null)
            stats.Experience += reward;

        return Task.CompletedTask;
    }

    public Task<Thing> OnDefeatAsync(Thing defeated, Thing victor, CancellationToken ct) =>
        Task.FromResult(worldContext.StartingRoom);
}
```

You don't need to reset `CombatantBehavior.CurrentHitPoints` yourself —
`ICombatManager` already does that unconditionally before calling
`OnDefeatAsync`, regardless of what your handler does.

### Putting it together

1. Reference `SharpMud.Engine`, `SharpMud.Hosting`, `SharpMud.Persistence`,
   and `SharpMud.Ruleset.Rpg`.
2. Define your own stats behavior (a `Behavior` subtype), plus its EF Core
   `IEntityTypeConfiguration<T>` and an `IBehaviorMappingContributor` that
   registers it — see [Customizing sharp-mud](customizing.md) for the
   general pattern.
3. Implement `IWorldBuilder`/`IPlayerFactory` for your own world content and
   character creation (same as any ruleset — see
   [Getting Started](getting-started.md)).
4. Implement `ICombatOutcomeHandler` as shown above.
5. Wire it all up:

```csharp
app.Services.AddSharpMudSqlitePersistence("./mygame.db");
app.Services.AddSingleton<IBehaviorMappingContributor, MyBehaviorMappingContributor>();
app.Services.AddSharpMudWorld<MyWorldBuilder>();
app.Services.AddSharpMudPlayerFactory<MyPlayerFactory>();
app.Services.AddSharpMudRpgRuleset<MyCombatOutcomeHandler>();
app.Services.AddSharpMudCliTransport();
```

If you have commands of your own beyond `kill`/`attack`/`flee`, pass them
as a callback rather than calling `AddSharpMudRuleset(...)` a second time
yourself:

```csharp
app.Services.AddSharpMudRpgRuleset<MyCombatOutcomeHandler>((sp, registry) =>
{
    registry.Register(new MyCustomCommand());
});
```

`AddSharpMudRpgRuleset` calls the underlying command registration exactly
once internally. A second, independent call would silently replace the
first — the way this DI registration works, the *last* call wins, not both
— so `kill`/`attack`/`flee` or your own commands would quietly disappear
depending on call order. Routing everything through the one callback avoids
that entirely.

For a complete, richer worked example (its own D&D-style stats, races and
classes, a hand-built world) built this exact way, see
[`SharpMud.Samples.Classic`](https://github.com/LayeredCraft/sharp-mud/tree/main/samples/SharpMud.Samples.Classic)
in the sharp-mud repo.
