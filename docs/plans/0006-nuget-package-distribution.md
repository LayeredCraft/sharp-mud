# [PLAN-0006] NuGet Package Distribution + Sample-Based Ruleset Extraction

**Implements:** [ADR-0006](../adr/0006-nuget-package-distribution.md)

**Status:** Done

**Last updated:** 2026-07-20

## Goal

An external consumer can `dotnet add package SharpMud` (or pick individual
`SharpMud.*` packages), write their own `Ruleset` project (or fold it into
one project with their `Program.cs`), and get a running MUD with a
`SharpMudApplication.CreateBuilder()`/`.Build()`/`.RunAsync()` composition
root a few lines long — without cloning this repo. This repo's own
`SharpMud.Ruleset.Classic` + reference host consolidate into one
`samples/SharpMud.Samples.Classic` project and become the proof that the
packages are sufficient on their own, in the same one-project shape a real
consumer would use.

## Scope

Per ADR-0006's Decision Outcome. In scope: the full package set (`Engine`,
`Hosting`, `Persistence`/`.Sqlite`/`.DynamoDb`, `Adapters.Telnet`,
`Adapters.Cli`, meta-package `SharpMud`), the `samples/` reorganization,
`Directory.Build.props` + `publish-preview.yaml`/`publish-release.yaml` CI,
root `LICENSE` (MIT), the `coding-standards.md` correction, and a minimal
GitHub Pages docs-site skeleton (mechanism + one real Getting Started page).

Explicitly deferred (per ADR-0006): Postgres/SqlServer/other relational
provider packages (consumer can already BYO-provider against core
`Persistence`); an `internal`-by-default public-API-surface audit (Open
Item, tracked separately, not blocking first publish); multi-repo/
independent-per-package versioning (lockstep is the decision, not a gap);
full docs-site content beyond the Getting Started skeleton (configuration
reference, deeper guides — real writing work tracked as follow-up, not
bundled into this plan's "done").

## Tasks

### Repository reorganization

- [x] `git mv src/SharpMud.Ruleset.Classic samples/SharpMud.Samples.Classic`
      (preserve history — this becomes the single consolidated project)
- [x] **Update the merged project's `.csproj`, not just its file
      contents** — caught in PR review: `SharpMud.Ruleset.Classic.csproj`
      today is a plain class library (no `OutputType`, no `Serilog`/config/
      adapter references) — none of what `Program.cs` needs once it moves
      in. Add `<OutputType>Exe</OutputType>`, `ProjectReference`s to
      `SharpMud.Hosting` and whichever transport package(s) the sample
      wants, and whatever logging/config `PackageReference`s `Program.cs`
      still needs directly (verify at implementation time which of these
      `SharpMud.Hosting` already pulls in transitively vs. which the
      sample still owns directly — e.g. Serilog is a consumer's own
      logging-provider choice, not something `Hosting` should hardcode).
      Skipping this leaves a project with `Program.cs` in it that doesn't
      build.
- [x] **Split `src/SharpMud.Host`'s files by whether they're
      ruleset-specific or genuinely generic — don't move all of it to
      `samples/` as one block** (caught in PR review: an earlier version of
      this task sent everything to `samples/`, which would strand
      `SharpMud.Hosting` without any actual session/login handling and
      leave every consumer copying sample code to accept logins at all):
      - `git mv src/SharpMud.Host/Program.cs samples/SharpMud.Samples.Classic/Program.cs`
        — genuinely sample-specific (it's the composition root being
        rewritten against `SharpMud.Hosting`'s builder anyway).
      - `git mv src/SharpMud.Host/SessionLoop.cs src/SharpMud.Host/PasswordHashing.cs
        src/SharpMud.Hosting/` — `SessionLoop` only imports
        `SharpMud.Engine.*` (verified: zero references to `Ruleset.Classic`)
        and is documented as *"shared by every transport"* (`SPEC.md`,
        `docs/networking.md`); `PasswordHashing` depends on nothing
        ruleset/sample-specific either, but **does** need
        `Microsoft.Extensions.Identity.Core` added to
        `SharpMud.Hosting.csproj` (caught in PR review: it uses
        `Microsoft.AspNetCore.Identity`'s `PasswordHasher<TUser>`, which
        that package supplies — an earlier version of this plan/the ADR's
        package table said `Hosting` needed nothing beyond
        `Microsoft.Extensions.Hosting`, which was wrong).
      - `git mv src/SharpMud.Host/HostOptions.cs src/SharpMud.Hosting/` —
        **trim it first, don't move it unchanged** (caught in PR review):
        today's `HostOptions` bundles `DbPath` with `UseTelnet`/
        `TelnetPort`, which conflicts with the "no `TransportMode` in
        `Hosting`" decision below. Reduce it to `DbPath`
        (`SHARPMUD_DB_PATH`) only before moving; `UseTelnet`/`TelnetPort`
        don't move with it — see the transport-wiring tasks below for
        where the port setting actually goes.
      - `git mv src/SharpMud.Host/LoginFlow.cs src/SharpMud.Host/PlayerLogin.cs
        src/SharpMud.Hosting/` — **not "unchanged" like the two above**
        (caught in PR review, twice now): both currently call
        `HubWorldBuilder.CreatePlayer(...)` directly, and `HubWorldBuilder.cs`
        itself `using`s `SharpMud.Ruleset.Classic` — genuinely ruleset-coupled,
        not just apparently so from `LoginFlow`'s/`PlayerLogin`'s own `using`
        statements. **Do not add a `createPlayer` parameter to either
        method** — both already sit at `coding-standards.md`'s 4-parameter
        limit, and a 5th parameter (raw `Func<>` or otherwise) blows past
        it, caught in a second round of PR review. Instead:
        1. Add a new `IPlayerFactory` interface to `SharpMud.Hosting`:
           `Thing CreatePlayer(World world, string username, string
           passwordHash, Thing startingRoom)`.
        2. Convert `LoginFlow`/`PlayerLogin` from `public static class` to
           ordinary constructor-injected service classes, taking
           `IThingRepository`/`IPlayerFactory` via the constructor (per
           `coding-standards.md`'s DI convention) instead of as method
           parameters — their `RunAsync`/`ResolveOrCreateAsync` methods
           drop back to 3 parameters plus a trailing `CancellationToken`,
           under the limit again.
        3. Register both in `SharpMud.Hosting`'s own DI setup; the sample
           registers `services.AddSingleton<IPlayerFactory,
           ClassicPlayerFactory>()`, a thin wrapper around
           `HubWorldBuilder.CreatePlayer`.
        Do this decoupling *before* moving the files, not after, so the
        move itself doesn't temporarily break the build.
      - `TelnetHostContext.cs` does **not** move anywhere — it gets
        deleted, not relocated (caught in PR review: an earlier version of
        this plan deleted `src/SharpMud.Host` without accounting for this
        file at all). It exists today only to satisfy the 4-parameter rule
        for `HostRunner.RunTelnetAsync` by bundling `World`/`Parser`/
        `Registry`/`Repository`/`StartingRoom`/`Port`/`Logger` into one
        parameter object; once `HostRunner`'s logic becomes a
        DI-constructed `BackgroundService` (next bullet), all of those
        except `Port` and `StartingRoom` are ordinary constructor-injected
        DI dependencies and don't need a bundling record at all. `Port`
        becomes `AddSharpMudTelnetTransport(int port)`'s own parameter.
        `StartingRoom` has no obvious DI-singleton home yet — tied to the
        still-open world-builder registration point, see Open Questions.
      - `git mv src/SharpMud.Host/HostRunner.cs
        src/SharpMud.Adapters.Telnet/` and fold its logic into a new
        `AddSharpMudTelnetTransport(int port)` DI extension there instead of
        keeping it as a static `HostRunner` class — it directly constructs
        `TelnetListener` (`using SharpMud.Adapters.Telnet;`), so it can
        never live in `SharpMud.Hosting` without forcing every `Hosting`
        consumer to take a `Adapters.Telnet` reference. Add a
        `ProjectReference` from `SharpMud.Adapters.Telnet` to
        `SharpMud.Hosting` (new — the dependency direction flips relative
        to today, since `Hosting` must not reference `Adapters.Telnet`).
      - Add an equivalent `AddSharpMudCliTransport()` extension to
        `SharpMud.Adapters.Cli`, covering what today's `Program.cs` CLI
        branch does inline (`ConsoleSession` + `PlayerLogin.ResolveOrCreateAsync`
        + `SessionLoop.RunAsync`), same `ProjectReference`-to-`Hosting` shape.
      - Delete the now-empty `src/SharpMud.Host` project and remove it from
        `SharpMud.slnx`. The old `Ruleset.Classic` → `Host` project
        *reference* disappears entirely — there's only one project now
        (the sample), referencing `SharpMud.Hosting` and whichever
        transport package(s) it wants, not one project referencing another
        app project.
- [x] Move `HubWorldBuilder` (and any other hand-built hub content) into
      the consolidated project — it's sample content per ADR-0006, not
      engine
- [x] `git mv src/SharpMud.Host/appsettings.json
      samples/SharpMud.Samples.Classic/appsettings.json`, and add the same
      `<Content Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />`
      item (per ADR-0003) to the new sample's `.csproj` — caught in PR
      review: `Program.cs` loads it with `optional: false`, so without
      this the sample fails at startup, not just at some later config
      lookup.
- [x] **Move each test project to follow its production code, not as one
      block** — mirrors the split above, not the "everything to `samples/`"
      version this task previously described:
      - `git mv tests/SharpMud.Ruleset.Classic.Tests
        tests/SharpMud.Samples.Classic.Tests` (`CombatManagerTests`,
        `CombatResolverTests`) — follows `Ruleset.Classic`'s move into the
        sample, unmodified.
      - `git mv tests/SharpMud.Host.Tests/SessionLoopTests.cs
        tests/SharpMud.Host.Tests/PasswordHashingTests.cs
        tests/SharpMud.Hosting.Tests/` (new project) — follows
        `SessionLoop`/`PasswordHashing` into `SharpMud.Hosting`, unmodified.
      - `git mv tests/SharpMud.Host.Tests/HostOptionsTests.cs
        tests/SharpMud.Hosting.Tests/` — **needs real edits, not a straight
        move**: `HostOptionsTests` today covers `UseTelnet`/`TelnetPort`
        parsing, which no longer exists on the trimmed `HostOptions` (per
        the split above) — drop those assertions, keep/expand `DbPath`
        parsing coverage. Any `UseTelnet`/`TelnetPort` coverage that's
        still worth having moves to wherever that config actually lands
        (`SharpMud.Adapters.Telnet.Tests`, once that config exists there).
      - `git mv tests/SharpMud.Host.Tests/LoginFlowTests.cs
        tests/SharpMud.Hosting.Tests/` — **needs real updates, not a
        straight move**: `LoginFlowTests` currently calls
        `LoginFlow.RunAsync` directly against the static class; once
        `LoginFlow` becomes a constructor-injected service taking
        `IThingRepository`/`IPlayerFactory` (per the split above), every
        existing test needs to construct a `LoginFlow` instance with a
        fake/stub `IPlayerFactory` (a real one only where a test actually
        asserts on the created player's shape) instead of calling a static
        method. Existing assertions carry forward, the call sites don't.
      - `PlayerLogin`/`HostRunner` have no existing tests to move (neither
        has a test file today) — add coverage for the new
        `IPlayerFactory`-injected `PlayerLogin.ResolveOrCreateAsync` as new
        `SharpMud.Hosting.Tests` coverage, not a carried-forward regression
        test.
      - Delete the now-empty `tests/SharpMud.Host.Tests`.
      - **New test projects for the transport extensions** — neither
        exists today (only `tests/SharpMud.Adapters.Telnet.Tests` does,
        and it doesn't yet cover the new extension): add coverage for
        `AddSharpMudTelnetTransport(...)` in
        `tests/SharpMud.Adapters.Telnet.Tests`, and create a new
        `tests/SharpMud.Adapters.Cli.Tests` project (doesn't exist today)
        for `AddSharpMudCliTransport()`. New coverage, not regression —
        this behavior doesn't exist yet either.
      Everything above is regression coverage in spirit for the *moved*
      files — no existing assertion gets dropped or downgraded to "sample,
      so untested" (the error an earlier version of this plan made, caught
      in PR review) — but `HostOptionsTests`/`LoginFlowTests` specifically
      need real edits, not just a `git mv`, because the signatures/types
      they test are changing.
- [x] Rewrite `samples/SharpMud.Samples.Classic/Program.cs` against
      `SharpMud.Hosting`'s builder — this is the concrete proof that the
      ~130 lines of generic plumbing identified in ADR-0006's Context
      actually collapse to a few lines, in a single project alongside the
      ruleset code it registers; if it doesn't, that's a signal the
      `Hosting` design needs revisiting before merging, not something to
      paper over. **Must preserve `SHARPMUD_MODE`/`SHARPMUD_TELNET_PORT`/
      `--telnet` parsing and the transport decision that used to live in
      `HostOptions`** (caught in PR review): since that decision moved to
      the sample's own composition-root logic (per the `HostOptions`
      split), the sample has to actually parse those variables itself,
      same arg-wins-over-env precedence as today, and call
      `AddSharpMudTelnetTransport`/`AddSharpMudCliTransport` accordingly —
      `Dockerfile` sets `SHARPMUD_MODE=telnet` by default and expects it to
      work; needs a real test proving that default still starts the Telnet
      transport post-refactor, not just an assumption.
- [x] **Update the actual `Dockerfile`, not just docs referencing it** —
      flagged independently in two rounds of PR review (self-review and
      Codex): it currently `COPY`s/restores/publishes
      `src/SharpMud.Host/SharpMud.Host.csproj` and
      `src/SharpMud.Ruleset.Classic/SharpMud.Ruleset.Classic.csproj`
      directly, and its `ENTRYPOINT` runs `SharpMud.Host.dll` — every one
      of those breaks once `src/SharpMud.Host` is deleted and the
      runnable app moves to `samples/SharpMud.Samples.Classic`. This is a
      real build/deploy break, not just a docs-drift issue: the checklist
      items above can all be satisfied while the container image still
      fails to build or immediately crashes on the wrong entrypoint.
      Concretely: update the `COPY`/`dotnet restore`/`dotnet publish`
      lines to target `samples/SharpMud.Samples.Classic/SharpMud.Samples.Classic.csproj`
      (and drop the now-nonexistent separate `Ruleset.Classic` `COPY`
      line, since it's part of the same project now), and change
      `ENTRYPOINT` to `["dotnet", "SharpMud.Samples.Classic.dll"]`. The
      `SHARPMUD_MODE`/`SHARPMUD_TELNET_PORT`/`SHARPMUD_DB_PATH` `ENV`
      lines stay as-is — they're still consumed by the sample's
      `Program.cs`, which per the `HostOptions` split above is exactly
      where transport-mode selection now lives.
- [x] Update `docs/engine-vs-ruleset.md`'s project-structure listing and
      `docs/deployment.md`'s Dockerfile references to the new paths

### `SharpMud.Hosting` (new project)

- [x] `src/SharpMud.Hosting/SharpMud.Hosting.csproj` — `PackageReference`s
      to `Microsoft.Extensions.Hosting` **and**
      `Microsoft.Extensions.Identity.Core` (needed by `PasswordHashing.cs`
      — caught in PR review, an earlier version of this plan/ADR-0006's
      package table said `Hosting` needed nothing beyond
      `Microsoft.Extensions.Hosting`)
- [x] `SharpMudApplicationBuilder : IHostApplicationBuilder` — wraps
      `Host.CreateApplicationBuilder(args)`, delegates
      `Services`/`Configuration`/`Environment`/`Logging`/`Metrics`/
      `Properties`/`ConfigureContainer` straight through; static
      `SharpMudApplication.CreateBuilder(args)` factory
- [x] `SharpMudApplication : IHost` — wraps the built `IHost`; `RunAsync`
      delegates to it directly (no custom middleware/invocation pipeline —
      see ADR-0006's comparison to `minimal-lambda` for why that's
      deliberately not needed here)
- [x] **No `SharpMudOptions` type** (removed per PR review — an earlier
      draft had it duplicating `DbPath` with the trimmed `HostOptions`
      below, two sources of truth for the same setting with no stated
      precedence). `HostOptions.Parse`'s env-var/CLI-arg path is the single
      source of truth for `DbPath`, per `security.md`'s existing reasoning
      for keeping deployment config manual rather than `IOptions<T>`-bound.
      Introduce a real `IOptions<T>`-shaped options type later only if a
      genuine code-configured setting actually needs one.
- [x] `HostOptions.cs` — moved in from `src/SharpMud.Host` **trimmed to
      `DbPath` only** (per the Repository reorganization task above, not a
      straight move) — `UseTelnet`/`TelnetPort` don't come with it.
- [x] `PasswordHashing.cs` — moved in from `src/SharpMud.Host` per the
      Repository reorganization task above, namespace updated to
      `SharpMud.Hosting`, otherwise unchanged.
- [x] `SessionLoop.cs` — moved in from `src/SharpMud.Host`, **converted
      from `public static class` to a constructor-injected service class**
      taking `World`/`ICommandParser`/`ICommandRegistry`/`IThingRepository`
      via the constructor instead of as method parameters — **not** a
      straight move (caught in PR review, a second time in this plan):
      today's `RunAsync` takes six non-`CancellationToken` parameters,
      already past `coding-standards.md`'s 4-parameter limit, and this plan
      already fixed the identical issue for `LoginFlow`/`PlayerLogin` while
      missing this sibling. `RunAsync(ISession session, Thing player,
      CancellationToken ct)` is what's left on the method itself once the
      rest move to the constructor.
- [x] New `IPlayerFactory` interface: `Thing CreatePlayer(World world,
      string username, string passwordHash, Thing startingRoom)`.
- [x] `LoginFlow.cs`/`PlayerLogin.cs` — moved in from `src/SharpMud.Host`,
      **converted from `public static class` to constructor-injected
      service classes taking `IThingRepository`/`IPlayerFactory`** (per the
      Repository reorganization task above) — not a straight move and
      **not** a raw `Func<>` parameter either (a first-draft fix that
      would have blown past `coding-standards.md`'s 4-parameter limit,
      caught in a second round of PR review). This is what actually makes
      a consumer's login/session handling work out of the package instead
      of requiring copied sample code — the concrete gap caught in PR
      review.
- [x] Register `LoginFlow`/`PlayerLogin` in `SharpMud.Hosting`'s DI setup
      (whatever shape that takes — `AddSharpMud(...)`-style extension or
      direct `Services.AddScoped<LoginFlow>()`/etc., implementation's
      call).
- [x] `GameLoop` registered as a `BackgroundService` (or a thin
      `BackgroundService` wrapper around it, if `GameLoop` itself shouldn't
      take a direct `Microsoft.Extensions.Hosting` dependency — decide
      during implementation which project should own that coupling)
- [x] **Explicit owner for the shutdown-time whole-world save** — caught in
      PR review: today's `Program.cs` does `await
      repository.SaveTreeAsync(hubArea, CancellationToken.None)` after the
      session loop/listener wind down but before the final `gameLoopTask`
      await, specifically to capture NPC state (wander position, live
      combat HP) that isn't tied to any player session and so isn't
      already covered by each session's own on-disconnect save
      (`docs/persistence.md`). The `SharpMudApplicationBuilder`/
      `BackgroundService` shape this ADR moves to doesn't have an
      equivalent by default — nothing currently listed in this plan
      performs that save once `IHost` owns the shutdown sequence. Assign
      this to a hosted service's `StopAsync` override (or an
      `IHostApplicationLifetime.ApplicationStopping` callback) inside
      `SharpMud.Hosting`, not left as only a Verification-section check —
      needs the world root/hub `Thing` reference, which ties to the
      still-open world-builder registration point (see Open Questions).
- [x] **No transport wiring lives here** — `Hosting` must not reference
      `SharpMud.Adapters.Telnet`/`SharpMud.Adapters.Cli` (caught in PR
      review: an earlier version of this task had `Hosting` itself
      instantiate `TelnetListener` and branch on a `TransportMode` enum,
      which would force every `Hosting` consumer to take both adapter
      packages regardless of need). What `Hosting` *does* expose is
      whatever shared surface the adapter extensions below actually need
      to call (the injected `SessionLoop`/`LoginFlow` services, `GameLoop`
      registration, etc.) — see the new `SharpMud.Adapters.Telnet`/
      `SharpMud.Adapters.Cli` tasks below for where the transport-specific
      `BackgroundService`s actually get registered.
- [x] `AddSharpMudRuleset(Action<ICommandRegistry> register)` (or
      equivalent) extension point for the consumer's ruleset registration
      callback — **must call `BuiltinCommands.RegisterAll(registry)` itself
      before invoking the consumer's callback** (caught in PR review:
      today's `Program.cs` registers built-ins before `ClassicCommands`;
      an earlier version of this ADR's example only showed the ruleset
      callback running, which would leave every core verb — `look`/`move`/
      `quit`/inventory — unregistered and returning `Huh?` for any
      consumer following the documented pattern). Also a world-builder
      registration point (name/shape TBD during implementation — needs to
      express "load persisted tree, else call the consumer's builder, then
      save" without hardcoding `HubWorldBuilder`).
- [x] `GameLoop`'s `BackgroundService` constructor-injects
      `IEnumerable<ITickable>` and registers whatever's resolved at
      startup, rather than a second dedicated registration callback
      alongside `AddSharpMudRuleset` — caught in PR review: today's
      `Program.cs` constructs `CombatManager` (implements `ITickable`) and
      registers it with `GameLoop` directly, and nothing in the
      package-entry-point sketch reproduced that; without it, `kill` would
      register as a command but never actually advance combat.
      `WanderManager`/`LinkdeadSweeper` (engine-level, not ruleset-specific)
      get registered the same way, by `Hosting` itself via its own DI
      registration — a consumer only needs to
      `services.AddSingleton<ITickable, MyCombatManager>()` for their own
      ruleset-specific tickables, same DI pattern already established for
      `IPlayerFactory`.
- [x] **Verify**: does the generic host's default `ConsoleLifetime` handle
      `SIGTERM` correctly on Unix out of the box? If yes, delete the
      hand-rolled `PosixSignalRegistration` code instead of porting it
      into `Hosting` — don't re-solve an already-fixed problem blind to
      whether it's already fixed. If no, port the existing fix forward.
- [x] XML doc comments on every public member per `documentation.md`

### `SharpMud.Adapters.Telnet` transport wiring (new tasks on an existing project)

- [x] Add a `ProjectReference` from `SharpMud.Adapters.Telnet` to
      `SharpMud.Hosting` — the dependency direction flips relative to
      today (`Hosting` must never reference `Adapters.Telnet`, per the
      `SharpMud.Hosting` task section above).
- [x] Fold `HostRunner.cs`'s logic (moved in from `src/SharpMud.Host` per
      the Repository reorganization task) into an
      `AddSharpMudTelnetTransport(int port)` DI extension — a
      `BackgroundService` constructor-injected with `World`/
      `ICommandParser`/`ICommandRegistry`/`IThingRepository`/
      `ILogger<TelnetSession>` (ordinary DI dependencies, replacing
      `TelnetHostContext`'s manually-assembled bundle — see the Repository
      reorganization task above for why that record doesn't move) and
      `port` from the extension's own parameter. Accepts connections
      (`TelnetListener.AcceptSessionsAsync`, unchanged) and, per
      connection, runs `LoginFlow.RunAsync` then `SessionLoop.RunAsync`
      via the injected `LoginFlow`/`SharpMud.Hosting` services, same
      exception-isolation shape `HostRunner.HandleConnectionAsync` already
      has today. Where `StartingRoom` comes from is tied to the
      still-open world-builder registration point — see Open Questions.
- [x] XML doc comments per `documentation.md`.

### `SharpMud.Adapters.Cli` transport wiring (new tasks on an existing project)

- [x] Add a `ProjectReference` from `SharpMud.Adapters.Cli` to
      `SharpMud.Hosting`, same reasoning as Telnet above.
- [x] `AddSharpMudCliTransport()` DI extension covering what today's
      `Program.cs` CLI branch does inline: construct a `ConsoleSession`,
      resolve/create the player via the injected `PlayerLogin` service
      (constructor-injected `IThingRepository`/`IPlayerFactory`, not a
      `createPlayer` parameter — per the `LoginFlow`/`PlayerLogin` fix
      above), run `SessionLoop.RunAsync`.
- [x] XML doc comments per `documentation.md`.

### `SharpMud.Persistence` split

- [x] Remove `Microsoft.EntityFrameworkCore.Sqlite`/
      `SQLitePCLRaw.lib.e_sqlite3` `PackageReference`s from
      `SharpMud.Persistence.csproj` — core stays provider-agnostic
      (`Microsoft.EntityFrameworkCore`/`.Relational` only)
- [x] New `src/SharpMud.Persistence.Sqlite/` — the removed package refs,
      plus `AddSharpMudSqlitePersistence(string dbPath)` extension
      wrapping today's `UseSqlite(...)` call site (moved out of
      `Program.cs`)
- [x] **Verify before building `SharpMud.Persistence.DynamoDb`**: does
      `EntityFrameworkCore.DynamoDb` actually accept the shared
      `Configurations/` classes as-is? `ThingConfiguration`/
      `BehaviorConfiguration`'s `ToTable(...)` and single-property
      `HasKey(...)` calls are confirmed supported per the provider's own
      table-and-key-mapping docs, but `BehaviorConfiguration`'s
      `HasDiscriminator<string>("BehaviorType")` (TPH inheritance mapping)
      is *not* yet confirmed either way — check the provider's modeling
      docs for inheritance/discriminator support specifically. If
      unsupported, `SharpMud.Persistence`'s "one shared config tree, thin
      provider packages on top" premise (ADR-0006) doesn't hold as-is —
      the fallback is provider-specific configuration (a Dynamo-specific
      partial config or a different inheritance strategy for that
      provider only), not silently shipping a `.DynamoDb` package that
      fails at model-validation time. This is a real go/no-go check, not a
      formality — do it before writing `SharpMud.Persistence.DynamoDb`'s
      other tasks below.
- [x] New `src/SharpMud.Persistence.DynamoDb/` — references
      `EntityFrameworkCore.DynamoDb 10.0.0` (the current stable release,
      confirmed against NuGet directly — see ADR-0006's package table),
      targeting this project's `net10.0` TFM specifically since the
      provider is an EF Core 10 build, not EF Core 11, equivalent
      `AddSharpMudDynamoDbPersistence(...)` extension
- [x] Update `samples/SharpMud.Samples.Classic` to consume
      `SharpMud.Persistence.Sqlite`'s extension instead of the inline
      `UseSqlite(...)` call

### Packaging metadata + CI

- [x] Root `Directory.Build.props` — `PackageLicenseExpression` (MIT),
      `RepositoryUrl`, `Authors`, `PackageProjectUrl`, `PackageIcon`,
      `PackageReadmeFile`, `GenerateDocumentationFile`, `DebugType=embedded`,
      `Microsoft.SourceLink.GitHub` — matching `optimized-enums`'/
      `structured-logging`'s shape exactly; no `VersionPrefix` (release-drafter
      resolves the version, per ADR-0006)
- [x] Root `icon.png`
- [x] Root `LICENSE` (MIT)
- [x] `NOTICE.md` (or a README section) crediting WheelMUD as design
      inspiration, per ADR-0006's License and naming section — not a legal
      requirement, matches this project's existing citation discipline
- [x] Set `IsPackable=true` (or leave default) on every `src/` project
      above; confirm `samples/` projects are `IsPackable=false` explicitly
      so a solution-wide `dotnet pack` never emits sample packages
- [x] New `src/SharpMud/SharpMud.csproj` — the meta-package: no code,
      **`ProjectReference`s (not `PackageReference`s) to every other
      `SharpMud.*` project** — verified experimentally: `dotnet pack`
      automatically translates a `ProjectReference` to a packable project
      into a `<dependency>` entry in the resulting `.nuspec`, at that
      project's own version, with no requirement that the referenced
      package already exist on any feed. A literal `PackageReference`
      instead would fail restore on the very first `dotnet pack
      SharpMud.slnx` — none of the sibling packages exist on any feed
      until *after* that first pack/publish — caught in PR review.
- [x] `.github/workflows/publish-preview.yaml` — push to `main` →
      `devops-templates`' `publish-preview.yml@v10.1` +
      `LayeredCraft/devops-templates/.github/actions/nuget-push@v10.1` (the
      full `{owner}/{repo}/{path}@{ref}` reference — a bare
      `actions/nuget-push@v10.1` resolves to a different, nonexistent
      `actions/nuget-push` repository and fails to load), matching
      `structured-logging`'s repo-level workflow file exactly, pointed at
      `SharpMud.slnx`, with
      `prereleaseIdentifier: alpha` explicitly set (default is `preview`) —
      packages publish as `X.Y.Z-alpha.<run_number>` while this package set
      is in its alpha stage; revisit this input once it graduates out of
      alpha
- [x] `.github/workflows/publish-release.yaml` — same shape, triggered on
      `release: published`
- [x] Multi-target `net10.0;net11.0` on every packaged `src/` project;
      **verify** the codebase actually compiles clean against `net10.0` —
      if any `net11.0`-only API is in use, resolve via `#if` gating or
      confirm it's acceptable to require `net11.0` after all (a real
      finding, not assumed clean)

### Documentation site (GitHub Pages)

- [x] New top-level `docsite/` directory (exact name TBD) — the Zensical
      site source, kept separate from `docs/`'s existing ADR/plan/subsystem
      content per ADR-0006's Documentation site section
- [x] `pyproject.toml`/`uv.lock` (Zensical + `mdformat` toolchain, matching
      `dynamodb-efcore-provider`'s dependency set), `zensical.toml` with a
      minimal curated `nav` (Home, Getting Started to start — expand as
      real content lands)
- [x] `.github/workflows/docs.yaml` — PR builds (no deploy) + push-to-`main`
      build/deploy via `actions/upload-pages-artifact`/`actions/deploy-pages`,
      matching `dynamodb-efcore-provider`'s workflow shape exactly
      (`uv sync --locked --all-extras --dev` → `uv run zensical build`)
- [ ] Enable GitHub Pages (Pages source: GitHub Actions) in repo settings
- [x] Minimal skeleton content: a home page and one real "Getting Started"
      walkthrough using the actual packages this plan produces (install
      `SharpMud`, write a one-project `Ruleset` + `Program.cs`, run it) —
      enough to prove the pipeline end-to-end. A full content build-out
      (per-package configuration reference, a data-modeling-equivalent
      guide, etc.) is out of scope for this plan — see Open Questions.

### Docs / standards corrections

- [x] `coding-standards.md`'s DI/composition section: replace the "no
      `AddSharpMudX()` extension-method sprawl... without discussing it
      first" line — it described the codebase's state at the time, not a
      standing prohibition. Correct it to document `SharpMud.Hosting`'s
      builder/extension-method pattern as the sanctioned shape for a
      package's own composition-root entry point, while keeping the
      existing "`Program.cs` wires everything inline, no `AddSharpMudX()`
      sprawl" guidance for *application-level* (non-package) DI wiring —
      these are different concerns and the corrected text needs to say so,
      not just delete the old line. **Done in the design PR** (#8), not
      implementation — a design decision's own record-keeping, not code.
- [x] `docs/adr/README.md` — index row for ADR-0006. **Done in the design
      PR** (#8).
- [x] `docs/plans/README.md` — index row for this plan. **Done in the
      design PR** (#8).
- [x] `docs/engine-vs-ruleset.md` — Open Items: forward-reference to
      ADR-0006 ("Host's role as the only ruleset-aware project is now
      fulfilled by each consumer's own project, not this repo's `Host` —
      see ADR-0006"), without rewriting the doc's current-state prose to
      describe unimplemented behavior as current (per `design-decisions.md`).
      **Done in the design PR** (#8).
- [x] `README.md` (root) — update to describe the package-based consumption
      story once implemented, not before
- [x] **Update every subsystem doc whose current-state prose cites the old
      `src/SharpMud.Host` paths/types, not just `engine-vs-ruleset.md`/
      `deployment.md`** — caught in PR review, a real gap: this task
      previously listed only two files, but a direct search turns up
      concrete path/type references that go stale in at least:
      - `docs/accounts-auth.md` — cites `src/SharpMud.Host/PasswordHashing.cs`,
        `src/SharpMud.Host/LoginFlow.cs`, `HostRunner.HandleConnectionAsync`,
        `SharpMud.Host.Tests`, and describes `LoginFlow` as "only used by
        `HostRunner`'s Telnet path" (no longer accurate once `LoginFlow` is
        transport-agnostic and both transports use it)
      - `docs/networking.md` — cites `Host`'s per-connection loop as
        `SessionLoop.RunAsync`, `HostRunner.RunTelnetAsync`
      - `docs/persistence.md` — cites `HostOptions`
        (`SHARPMUD_MODE`/`SHARPMUD_TELNET_PORT`), `src/SharpMud.Host/Program.cs`
      - `docs/architecture.md` — project-structure listing includes
        `SharpMud.Host/` as "composition root"
      - `docs/README.md` — `deployment.md`'s summary row cites `HostOptions`
      - `SPEC.md` — cites "a shared `SessionLoop` used by every transport"
        in a way that should still read true but is worth a pass to confirm
      Per `documentation.md`, these get updated in the same PR as the
      behavior change (implementation), not this design PR — but listing
      them here now is what keeps the implementation PR from missing one,
      the same reason `engine-vs-ruleset.md`/`deployment.md` were already
      called out individually above.

## Critical files

New:
- `docs/adr/0006-nuget-package-distribution.md`
- `docs/plans/0006-nuget-package-distribution.md`
- `src/SharpMud.Hosting/*` — new `SharpMudApplicationBuilder`/
  `SharpMudApplication`/`IPlayerFactory` (no `SharpMudOptions` type — see
  above), plus `PasswordHashing.cs` (unchanged), `SessionLoop.cs` (static
  class converted to constructor-injected service, per above),
  `HostOptions.cs` (trimmed to `DbPath`), and `LoginFlow.cs`/`PlayerLogin.cs`
  (converted to constructor-injected services taking
  `IThingRepository`/`IPlayerFactory`) moved in from `src/SharpMud.Host`
- `src/SharpMud.Persistence.Sqlite/*`
- `src/SharpMud.Persistence.DynamoDb/*`
- `src/SharpMud/SharpMud.csproj` (meta-package)
- `Directory.Build.props`, `LICENSE`, `icon.png`, `NOTICE.md`
- `.github/workflows/publish-preview.yaml`, `.github/workflows/publish-release.yaml`
- `.github/workflows/docs.yaml`, `docsite/*` (`pyproject.toml`, `uv.lock`,
  `zensical.toml`, skeleton content)
- `samples/SharpMud.Samples.Classic/*` (moved + consolidated from
  `src/SharpMud.Ruleset.Classic` and only `Program.cs` from
  `src/SharpMud.Host` — not the rest, see Repository reorganization)
- `tests/SharpMud.Samples.Classic.Tests/*` (moved from
  `tests/SharpMud.Ruleset.Classic.Tests`)
- `tests/SharpMud.Hosting.Tests/*` (new project — `SessionLoopTests.cs`/
  `PasswordHashingTests.cs` moved unchanged, `HostOptionsTests.cs`/
  `LoginFlowTests.cs` moved with real edits for the trimmed `HostOptions`/
  the `LoginFlow`→service-class conversion, plus new coverage for
  `PlayerLogin`, all moved in from `tests/SharpMud.Host.Tests`)
- `tests/SharpMud.Adapters.Cli.Tests/*` (new project — doesn't exist
  today, covers the new `AddSharpMudCliTransport()` extension)

Modified:
- `src/SharpMud.Persistence/SharpMud.Persistence.csproj` (drop SQLite refs)
- `src/SharpMud.Adapters.Telnet/SharpMud.Adapters.Telnet.csproj` (new
  `ProjectReference` to `SharpMud.Hosting`, new `AddSharpMudTelnetTransport`
  extension, absorbs `HostRunner.cs`)
- `src/SharpMud.Adapters.Cli/SharpMud.Adapters.Cli.csproj` (new
  `ProjectReference` to `SharpMud.Hosting`, new `AddSharpMudCliTransport`
  extension)
- `tests/SharpMud.Adapters.Telnet.Tests/*` (new coverage for
  `AddSharpMudTelnetTransport(...)`)
- `Dockerfile` (COPY/restore/publish/`ENTRYPOINT` retargeted from
  `src/SharpMud.Host` to `samples/SharpMud.Samples.Classic` — flagged in
  two rounds of PR review, a real build/deploy break if missed)
- `SharpMud.slnx`
- `.agents/skills/engineering-workflow/references/coding-standards.md`
- `docs/adr/README.md`, `docs/plans/README.md`, `docs/engine-vs-ruleset.md`,
  `docs/deployment.md`, `docs/accounts-auth.md`, `docs/networking.md`,
  `docs/persistence.md`, `docs/architecture.md`, `docs/README.md`,
  `SPEC.md`, `README.md`

## Test plan

- Unit tests for `SharpMud.Hosting`'s new surface
  (`SharpMudApplicationBuilder`/`SharpMudApplication` wiring,
  `BuiltinCommands`/`IEnumerable<ITickable>` auto-registration) — new
  coverage, matching `testing.md`'s conventions.
- Existing `SharpMud.Persistence.Tests` split/adjusted to match the
  core/`Sqlite`/`DynamoDb` project split — regression coverage, not new
  behavior.
- `tests/SharpMud.Hosting.Tests` (moved from `SharpMud.Host.Tests` per the
  Repository reorganization task) — mixed, not uniformly "carried forward
  unmodified" (an earlier version of this summary said that for
  everything, out of sync with the detailed task list — fixed here to
  match): `SessionLoopTests`/`PasswordHashingTests` carry forward
  unmodified, regression coverage; `HostOptionsTests` needs its
  `UseTelnet`/`TelnetPort` assertions dropped (trimmed `HostOptions`, see
  above); `LoginFlowTests` needs real edits for the `LoginFlow`
  static-class-to-service conversion. New coverage for `PlayerLogin` and
  both transport extensions (`AddSharpMudTelnetTransport`/
  `AddSharpMudCliTransport`, including the new
  `tests/SharpMud.Adapters.Cli.Tests` project) isn't regression coverage
  at all — none of that behavior exists today.
- `tests/SharpMud.Samples.Classic.Tests` (`CombatManagerTests`,
  `CombatResolverTests`, moved from `SharpMud.Ruleset.Classic.Tests`) —
  regression coverage, carried forward unmodified. This section previously
  (incorrectly) described the consolidated sample as "not unit-tested...
  matching `SharpMud.Host`'s current untested status" — `SharpMud.Host` is
  not untested today, that was a factual error, not a real gap being
  introduced, and it's now further corrected: the moved tests split across
  two projects (`Hosting.Tests`/`Samples.Classic.Tests`) following their
  production code, not one combined project.
- On top of that existing coverage, a successful build + a real manual run
  of `samples/SharpMud.Samples.Classic` is the actual verification that the
  new `SharpMud.Hosting`-based `Program.cs` itself works end-to-end
  (below) — unit tests alone don't prove that; the two are complementary,
  not substitutes for each other.

## Verification

- `dotnet pack SharpMud.slnx` produces the full expected package set
  locally, with no sample projects packed.
- A real manual smoke test: run `samples/SharpMud.Samples.Classic`
  against locally-`dotnet pack`ed + locally-fed packages (not the in-repo
  `ProjectReference`s) — i.e. prove a consumer-shaped scenario actually
  works, not just that the monorepo still builds. Both
  `AddSharpMudCliTransport()` and `AddSharpMudTelnetTransport(...)` need a
  real pass, matching this repo's established pattern for anything
  session/networking-facing.
- `publish-preview.yaml` produces a real preview package on a push to
  `main` in a throwaway branch/PR before merging this to `main` for real.
- Confirm `SIGINT`/`SIGTERM` shutdown still saves the world correctly
  under the new `BackgroundService`-based `GameLoop`/Telnet listener —
  this is the exact bug class `docs/persistence.md` already found and
  fixed once; don't regress it silently under the new hosting model.

## Open questions / blockers

All resolved during implementation:

- ~~Whether `EntityFrameworkCore.DynamoDb` supports EF Core's TPH
  `HasDiscriminator<string>(...)`~~ — **resolved: yes.** Confirmed directly
  against the provider's own modeling docs
  (`docs/modeling/single-table-design.md` in
  `LayeredCraft/dynamodb-efcore-provider`), not just inferred — TPH
  discriminators are supported, so the shared `Configurations/` tree works
  unmodified against DynamoDB.
- ~~Exact shape of the world-builder registration point~~ — **resolved:
  implemented as `IWorldBuilder`/`IPlayerFactory` (`SharpMud.Hosting`),
  populating `WorldContext` via `WorldLoaderHostedService`.** See
  `docs/engine-vs-ruleset.md`.
- ~~Whether `GameLoop` should take a direct `Microsoft.Extensions.Hosting`
  dependency~~ — **resolved: stays hosting-agnostic.**
  `GameLoopHostedService` (`SharpMud.Hosting`) wraps it as a thin
  `BackgroundService`; `SharpMud.Engine` never references
  `Microsoft.Extensions.Hosting`.
- ~~Exact name for the docs-site source directory~~ — **resolved:
  `docsite/`,** as used throughout this plan.
- Full docs-site content (per-package configuration reference, a
  data-modeling-equivalent guide, samples walkthroughs beyond the one
  Getting Started page) is real, separate writing work — still not
  scheduled against this plan, needs its own follow-up plan once the
  package mechanics this plan covers have had time to prove out in
  practice.
