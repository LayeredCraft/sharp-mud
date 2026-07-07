# Architecture

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs.

## Project / Module Structure

.NET 11 (preview — accept the preview-channel risk; fall back to .NET 10 LTS if
tooling/library support proves too immature). Nullable reference types enabled,
implicit usings on, file-scoped namespaces throughout.

```
sharp-mud.sln
  src/
    SharpMud.Engine/           # pure domain + game loop. Zero deps on Adapters,
                                # Persistence, or EF Core. This is the core.
    SharpMud.Persistence/      # EF Core DbContext + repository implementations.
                                # References Engine (for domain types / repo interfaces).
    SharpMud.Adapters.Cli/     # local stdin/stdout ISession implementation.
    SharpMud.Adapters.Telnet/  # (later) telnet ISession implementation.
    SharpMud.Adapters.Ssh/     # (later)
    SharpMud.Adapters.WebSocket/ # (later)
    SharpMud.Host/             # composition root: DI wiring (Microsoft.Extensions.
                                # DependencyInjection), config, process entry point.
  tests/
    SharpMud.Engine.Tests/     # xUnit v3 + AutoFixture + NSubstitute + AwesomeAssertions
    SharpMud.Persistence.Tests/
```

**Dependency direction (strict, enforced by project references):**
`Adapters.* → Engine`, `Persistence → Engine`, `Host → everything`. Engine never
references Adapters or Persistence. This is what makes transports (see
[networking.md](networking.md)) and storage backends (see
[persistence.md](persistence.md)) swappable without touching game logic — it's
the physical enforcement of the SPEC.md session-abstraction and
repository-interface decisions.

## The Global Tick Loop

```csharp
public interface ITickable
{
    void OnTick(TickContext ctx);
}

public interface IGameLoop
{
    Task RunAsync(CancellationToken ct); // drives the global tick, ~1-2s interval
}
```

Single server-wide heartbeat (per SPEC.md) that calls `OnTick` on every
registered `ITickable` — combat rounds in progress, NPC AI, regen. Player
commands (movement, look, chat) are NOT gated by the tick; they execute
immediately via the command pipeline (see [commands.md](commands.md)). Only
round-based systems hook into `ITickable` — see [combat.md](combat.md) for the
primary consumer.

## Dependency Injection

`Microsoft.Extensions.DependencyInjection`, standard container. `Host` is the
only project that composes the graph: registers the chosen `IPlayerRepository`/
`IRoomRepository` implementations (see [persistence.md](persistence.md)),
the chosen `ISession`-producing adapter (see [networking.md](networking.md)),
and engine services (`IGameLoop`, `ICommandRegistry`, `ICombatResolver`, etc).

## Testing & Observability

- **Unit tests** (xUnit v3 + AutoFixture + NSubstitute + AwesomeAssertions,
  per the `dotnet-unit-testing-patterns` skill conventions) required for:
  `ICommandParser`, each `ICommand` implementation, `ICombatResolver`,
  `IWorld.MovePlayer`, stat-derivation formulas. These are pure/deterministic
  enough to test without a live session or tick loop — `ISession` and
  repositories are mocked via NSubstitute.
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
