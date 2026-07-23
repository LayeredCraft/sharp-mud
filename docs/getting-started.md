# Getting Started

See [README.md](README.md) for how this doc relates to the rest of `docs/`.
This is the consumer-facing quick-start [ADR-0008](adr/0008-ruleset-scaffolding-tier.md)
promises — "`dotnet add package`, a few lines in `Program.cs`, run a basic
game" — analogous to EF Core's `UseSqlite(...)`. If you're building a
*different* ruleset on top of the engine instead, see
[Building your own ruleset on `SharpMud.Ruleset.Rpg`](#building-your-own-ruleset-on-sharpmudrulesetrpg)
below.

## Run a basic game

Create a new console project and add:

```
dotnet add package SharpMud.Engine --prerelease
dotnet add package SharpMud.Hosting --prerelease
dotnet add package SharpMud.Persistence.Sqlite --prerelease
dotnet add package SharpMud.Ruleset.Basic --prerelease
dotnet add package SharpMud.Adapters.Cli --prerelease
```

`--prerelease` is required until a stable 1.0 release ships — sharp-mud is
pre-1.0 and only prerelease packages are published so far.

(swap `SharpMud.Persistence.Sqlite` for `SharpMud.Persistence.DynamoDb` and/or
`SharpMud.Adapters.Cli` for `SharpMud.Adapters.Telnet` as needed — see
[persistence.md](persistence.md)/[networking.md](networking.md)).

`Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpMud.Adapters.Cli;
using SharpMud.Hosting;
using SharpMud.Persistence.Sqlite;
using SharpMud.Ruleset.Basic;

var builder = SharpMudApplication.CreateBuilder(args);

builder.Services.AddSharpMudSqlitePersistence("game.db");
builder.Services.AddSharpMudBasicRuleset();
builder.Services.AddSharpMudCliTransport();

var mud = builder.Build();
await mud.RunAsync();
```

That's it — `dotnet run` gives you a fresh character, a small default world
(a couple of rooms, one fightable NPC), and working `look`/movement/
`kill`/`attack`/`flee` combat, with SQLite persistence across restarts.

`AddSharpMudBasicRuleset(...)` takes an optional callback to tune the
starting numbers for a fresh character (see `BasicRulesetOptions`):

```csharp
builder.Services.AddSharpMudBasicRuleset(options =>
{
    options.StartingHitPoints = 30;
    options.StartingArmorClass = 12;
});
```

## Building your own ruleset on `SharpMud.Ruleset.Rpg`

`SharpMud.Ruleset.Basic` is one concrete leaf built on
`SharpMud.Ruleset.Rpg`'s combat/encounter scaffolding — `SharpMud.Ruleset.Rpg`
itself is not runnable on its own (same as
`Microsoft.EntityFrameworkCore.Relational` isn't a database), but a
different ruleset can reference it directly instead of `Basic`, and get the
same `CombatantBehavior`/`ICombatResolver`/`ICombatManager`/`AttackCommand`/
`FleeCommand`/`IDiceRoller` scaffolding without Basic's specific numbers or
world content.

To build on it:

1. Reference `SharpMud.Engine`, `SharpMud.Hosting`, `SharpMud.Persistence`,
   and `SharpMud.Ruleset.Rpg`.
2. Define your own stats behavior (a `Behavior` subtype), `IWorldBuilder`,
   `IPlayerFactory`, and `Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<T>`
   + `IBehaviorMappingContributor` for that stats behavior — see
   `SharpMud.Ruleset.Basic`'s `BasicStatsBehavior`/`BasicWorldBuilder`/
   `BasicPlayerFactory`/`BasicBehaviorMappingContributor` for a minimal
   worked example, or `samples/SharpMud.Samples.Classic` for a richer one.
3. Implement `ICombatOutcomeHandler` — your ruleset's hook for awarding XP/
   rewards on a win (`OnVictoryAsync`) and applying a death penalty plus
   choosing the respawn destination on a loss (`OnDefeatAsync`). This is the
   seam that keeps `SharpMud.Ruleset.Rpg` itself free of any concrete
   ruleset's stats/leveling concept.
4. Call `AddSharpMudRpgRuleset<TCombatOutcomeHandler>(...)` from your
   `Program.cs`, alongside `AddSharpMudWorld<TWorldBuilder>(...)`/
   `AddSharpMudPlayerFactory<TPlayerFactory>(...)` and your own
   `IBehaviorMappingContributor` registration. If you have your own commands
   beyond `kill`/`attack`/`flee`, pass them as
   `AddSharpMudRpgRuleset<TCombatOutcomeHandler>((sp, registry) => ...)` — this
   package calls `SharpMud.Hosting`'s `AddSharpMudRuleset(...)` exactly once
   internally, so don't call it again yourself; a second call would silently
   drop whichever call came first via DI's last-registration-wins resolution
   for `ICommandRegistry`.

`samples/SharpMud.Samples.Classic`'s `Program.cs` is a complete worked
example of this shape (Classic has no commands of its own beyond
`kill`/`attack`/`flee`, so it calls `AddSharpMudRpgRuleset<ClassicCombatOutcomeHandler>()`
with no callback).
