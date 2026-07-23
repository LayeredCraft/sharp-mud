# Architecture

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs.

## Project / Module Structure

.NET 11 (preview — accept the preview-channel risk; fall back to .NET 10 LTS if
tooling/library support proves too immature). Nullable reference types enabled,
implicit usings on, file-scoped namespaces throughout.

```
SharpMud.slnx
  src/
    SharpMud.Engine/              # Thing/Behavior, event system, generic behaviors,
                                   # command pipeline, session abstraction, tick loop.
                                   # Zero deps on Adapters, Persistence, or any ruleset.
    SharpMud.Hosting/              # generic-host composition helpers: WorldContext,
                                   # IWorldBuilder, IPlayerFactory, SessionLoop/LoginFlow,
                                   # AddSharpMud* extension methods. Ruleset-agnostic -
                                   # samples/rulesets plug in via those extension points.
    SharpMud.Persistence/         # EF Core DbContext + repository interfaces, provider-agnostic.
                                   # References Engine (for domain types / repo interfaces).
    SharpMud.Persistence.Sqlite/  # SQLite provider package: UseSqlite + IStorageInitializer.
    SharpMud.Persistence.DynamoDb/ # DynamoDB provider package: UseDynamo (net10.0 only).
    SharpMud.Adapters.Cli/        # local stdin/stdout ISession implementation.
    SharpMud.Adapters.Telnet/     # raw TCP ISession + listener - see networking.md.
    SharpMud.Adapters.Ssh/        # (later)
    SharpMud.Adapters.WebSocket/  # (later)
    SharpMud.Ruleset.Rpg/         # reusable RPG scaffolding tier (ADR-0008): CombatantBehavior,
                                   # combat resolver/manager, ICombatOutcomeHandler, attack/flee
                                   # commands, dice-roller. References Engine + Hosting + Persistence.
                                   # No ruleset-flavor knowledge; not runnable on its own.
    SharpMud.Ruleset.Basic/       # minimal concrete leaf ruleset (ADR-0008) built on
                                   # SharpMud.Ruleset.Rpg - plain stat block, small default
                                   # world with a fightable NPC, player factory. The actual
                                   # "dotnet add package, few lines, run a basic game" leaf.
    SharpMud/                     # meta-package: Engine + Hosting + Persistence only -
                                   # provider/transport packages always explicit (ADR-0007).
                                   # Ruleset.Rpg/Ruleset.Basic deliberately excluded - see
                                   # ADR-0008 Open Items.
  samples/
    SharpMud.Samples.Classic/     # D&D-flavored sample ruleset + composition root
                                   # (Program.cs). References everything, including
                                   # SharpMud.Ruleset.Rpg for combat/encounter scaffolding;
                                   # owns only its own Race/CharacterClass/stats/hand-built
                                   # hub world content, not combat plumbing.
  tests/
    SharpMud.Engine.Tests/        # xUnit v3 + AutoFixture + NSubstitute + AwesomeAssertions
    SharpMud.Hosting.Tests/
    SharpMud.Persistence.Tests/
    SharpMud.Adapters.Cli.Tests/
    SharpMud.Adapters.Telnet.Tests/
    SharpMud.Ruleset.Rpg.Tests/
    SharpMud.Ruleset.Basic.Tests/
    SharpMud.Samples.Classic.Tests/
```

**Dependency direction (strict, enforced by project references):**
`Adapters.* → Hosting + Engine` (both are direct references, not purely
transitive through Hosting), `Persistence.* → Persistence + Engine` (same —
direct references to both, not just Persistence), `Ruleset.Rpg → Engine +
Hosting + Persistence`, `Ruleset.Basic → Ruleset.Rpg` (plus `Engine`/
`Hosting`/`Persistence` directly), `Samples.Classic → everything, including
Ruleset.Rpg`. Engine never references Adapters, Persistence,
or any ruleset — see [engine-vs-ruleset.md](engine-vs-ruleset.md) for the full
rationale (this is the actual mechanism behind the "engine, not just a game"
goal in `SPEC.md`, not just the transport/persistence swappability described
below). This is also what makes transports (see [networking.md](networking.md))
and storage backends (see [persistence.md](persistence.md)) swappable without
touching game logic.

## The Global Tick Loop

```csharp
public interface ITickable
{
    Task OnTickAsync(TickContext ctx, CancellationToken ct); // async, not void - see docs/combat.md
}

public interface IGameLoop
{
    Task RunAsync(CancellationToken ct); // drives the global tick, ~1-2s interval
}
```

Single server-wide heartbeat (per SPEC.md) that calls `OnTickAsync` on every
registered `ITickable` — combat rounds in progress, NPC AI, regen. Player
commands (movement, look, chat) are NOT gated by the tick; they execute
immediately via the command pipeline (see [commands.md](commands.md)).
`GameLoopHostedService` (in `SharpMud.Hosting`) runs `IGameLoop.RunAsync` as a
`BackgroundService` alongside the session read loop, since `CombatManager`
(see [combat.md](combat.md)) is the first real `ITickable` consumer.

## Dependency Injection

`Microsoft.Extensions.DependencyInjection`, standard container, wired via the
.NET generic host (`Microsoft.Extensions.Hosting`). `SharpMud.Hosting`
provides the `AddSharpMud*` extension methods every project composes with;
the sample composition root (`samples/SharpMud.Samples.Classic/Program.cs`)
is the only place that assembles the full graph — the chosen persistence
provider (see [persistence.md](persistence.md)), the chosen transport
(see [networking.md](networking.md)), and engine services (`IGameLoop`,
`ICommandRegistry`, `ICombatResolver`, etc).

## Testing & Observability

- **Unit tests** (xUnit v3 + AutoFixture + NSubstitute + AwesomeAssertions,
  per the `dotnet-unit-testing-patterns` skill conventions) required for:
  `ICommandParser`, each `ICommand` implementation, `ICombatResolver` (in
  `SharpMud.Ruleset.Rpg.Tests` as of ADR-0008), `Thing`/`BehaviorManager`/`ThingEvents`
  propagation, stat-derivation formulas. These are pure/deterministic enough
  to test without a live session or tick loop — `ISession` and repositories
  are mocked via NSubstitute.
- **Test runner**: Microsoft.Testing.Platform (MTP), not VSTest. Test projects
  reference the `xunit.v3.mtp-v2` package (not the plain `xunit.v3`
  meta-package, which resolves an MTP-v1-compatible core and throws a
  `TypeLoadException` on `IDataConsumer` against a directly-referenced/newer
  `Microsoft.Testing.Platform`), plus
  `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`
  and `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>`
  in each test `.csproj`. `global.json` sets `"test": {"runner":
  "Microsoft.Testing.Platform"}` so plain `dotnet test` uses MTP.
- **Package versions**: managed centrally via `Directory.Packages.props`
  (Central Package Management) at the repo root — every project's
  `PackageReference` omits a `Version` attribute; the version lives in one
  place. `Microsoft.Testing.Platform` is pinned to the version
  `xunit.v3.core.mtp-v2` actually depends on (currently 2.0.2) rather than
  whatever NuGet reports as newest — bumping it independently of the xunit.v3
  package family risks reintroducing the `TypeLoadException` above.
- Each project has a `TestKit/` folder (`BaseFixtureFactory` +
  `<ProjectName>AutoDataAttribute`) providing the AutoFixture/NSubstitute
  pipeline used by `[Theory, EngineAutoData]`-style tests — see
  `tests/SharpMud.Engine.Tests/TestKit/` for the reference implementation.
- **Structured logging** via `Microsoft.Extensions.Logging` from day one — no
  raw `Console.WriteLine` for diagnostics (player-facing output goes through
  `ISession`, which is separate from logging). Minimum structured events:
  command resolution failures, combat round outcomes, tick timing/overrun
  warnings, player connect/disconnect.

## Open Items

- .NET 11 preview risk: if tooling/package ecosystem (esp. any EF Core
  provider) lags preview support, fall back to .NET 10 LTS.
- Tick interval: made configurable at the `Host` level rather than hardcoded
  (decision made) — actual default value still to be tuned once combat is
  being built, see [combat.md](combat.md).
