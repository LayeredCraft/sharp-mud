# [PLAN-0006] NuGet Package Distribution + Sample-Based Ruleset Extraction

**Implements:** [ADR-0006](../adr/0006-nuget-package-distribution.md)

**Status:** Not Started

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

- [ ] `git mv src/SharpMud.Ruleset.Classic samples/SharpMud.Samples.Classic`
      (preserve history — this becomes the single consolidated project)
- [ ] **Split `src/SharpMud.Host`'s files by whether they're
      ruleset-specific or genuinely generic — don't move all of it to
      `samples/` as one block** (caught in PR review: an earlier version of
      this task sent everything to `samples/`, which would strand
      `SharpMud.Hosting` without any actual session/login handling and
      leave every consumer copying sample code to accept logins at all):
      - `git mv src/SharpMud.Host/Program.cs samples/SharpMud.Samples.Classic/Program.cs`
        — genuinely sample-specific (it's the composition root being
        rewritten against `SharpMud.Hosting`'s builder anyway).
      - `git mv src/SharpMud.Host/SessionLoop.cs src/SharpMud.Host/LoginFlow.cs
        src/SharpMud.Host/PasswordHashing.cs src/SharpMud.Host/HostOptions.cs
        src/SharpMud.Hosting/` — all four only import `SharpMud.Engine.*`
        (verified: zero references to `Ruleset.Classic` in any of them),
        and `SessionLoop` specifically is documented as *"shared by every
        transport"* (`SPEC.md`, `docs/networking.md`) — this is exactly the
        generic plumbing `SharpMud.Hosting` exists to package, not sample
        content. See the `SharpMud.Hosting` task section below for how
        these integrate with the builder.
      - Delete the now-empty `src/SharpMud.Host` project and remove it from
        `SharpMud.slnx`. The old `Ruleset.Classic` → `Host` project
        *reference* disappears entirely — there's only one project now
        referencing `SharpMud.Hosting`, not two referencing each other.
- [ ] Move `HubWorldBuilder` (and any other hand-built hub content) into
      the consolidated project — it's sample content per ADR-0006, not
      engine
- [ ] **Move each test project to follow its production code, not as one
      block** — mirrors the split above, not the "everything to `samples/`"
      version this task previously described:
      - `git mv tests/SharpMud.Ruleset.Classic.Tests
        tests/SharpMud.Samples.Classic.Tests` (`CombatManagerTests`,
        `CombatResolverTests`) — follows `Ruleset.Classic`'s move into the
        sample.
      - `git mv tests/SharpMud.Host.Tests/SessionLoopTests.cs
        tests/SharpMud.Host.Tests/LoginFlowTests.cs
        tests/SharpMud.Host.Tests/HostOptionsTests.cs
        tests/SharpMud.Host.Tests/PasswordHashingTests.cs
        tests/SharpMud.Hosting.Tests/` (new project) — follows
        `SessionLoop`/`LoginFlow`/`HostOptions`/`PasswordHashing` into
        `SharpMud.Hosting`, then delete the now-empty
        `tests/SharpMud.Host.Tests`.
      This is regression coverage either way, not new testing — every
      existing test keeps passing under its new home, none get deleted or
      downgraded to "sample, so untested" (the error an earlier version of
      this plan made, caught in PR review).
- [ ] Rewrite `samples/SharpMud.Samples.Classic/Program.cs` against
      `SharpMud.Hosting`'s builder — this is the concrete proof that the
      ~130 lines of generic plumbing identified in ADR-0006's Context
      actually collapse to a few lines, in a single project alongside the
      ruleset code it registers; if it doesn't, that's a signal the
      `Hosting` design needs revisiting before merging, not something to
      paper over
- [ ] Update `docs/engine-vs-ruleset.md`'s project-structure listing and
      `docs/deployment.md`'s Dockerfile references to the new paths

### `SharpMud.Hosting` (new project)

- [ ] `src/SharpMud.Hosting/SharpMud.Hosting.csproj`
- [ ] `SharpMudApplicationBuilder : IHostApplicationBuilder` — wraps
      `Host.CreateApplicationBuilder(args)`, delegates
      `Services`/`Configuration`/`Environment`/`Logging`/`Metrics`/
      `Properties`/`ConfigureContainer` straight through; static
      `SharpMudApplication.CreateBuilder(args)` factory
- [ ] `SharpMudApplication : IHost` — wraps the built `IHost`; `RunAsync`
      delegates to it directly (no custom middleware/invocation pipeline —
      see ADR-0006's comparison to `minimal-lambda` for why that's
      deliberately not needed here)
- [ ] `SharpMudOptions` — `IOptions<T>`-shaped (`TransportMode`
      enum: `Cli`/`Telnet`, `TelnetPort`, `DbPath`) per
      `coding-standards.md`'s `IOptions<T>` convention; kept as a distinct
      concern from `HostOptions.Parse`'s env-var/secrets path even though
      both now live in this same project — `SharpMudOptions` is code-
      configured wiring, `HostOptions` stays the env-var/CLI-arg parsing
      path per `security.md`'s reasoning for keeping that one manual, not
      a second `IOptions<T>` binding for the same thing.
- [ ] `SessionLoop.cs`/`LoginFlow.cs`/`PasswordHashing.cs`/`HostOptions.cs`
      — moved in from `src/SharpMud.Host` per the Repository reorganization
      task above, namespace updated to `SharpMud.Hosting`, otherwise
      unchanged (all three already only depend on `SharpMud.Engine.*`).
      This is what actually makes a consumer's login/session handling work
      out of the package instead of requiring copied sample code — the
      concrete gap caught in PR review.
- [ ] `GameLoop` registered as a `BackgroundService` (or a thin
      `BackgroundService` wrapper around it, if `GameLoop` itself shouldn't
      take a direct `Microsoft.Extensions.Hosting` dependency — decide
      during implementation which project should own that coupling)
- [ ] Telnet listener registered as a `BackgroundService`, gated on
      `SharpMudOptions.Transport`, driving `SessionLoop.RunAsync` per
      accepted connection (through `LoginFlow` first, matching today's
      `HostRunner.HandleConnectionAsync` shape); CLI path wired the
      equivalent way for `TransportMode.Cli`
- [ ] `AddSharpMudRuleset(Action<ICommandRegistry> register)` (or
      equivalent) extension point for the consumer's ruleset registration
      callback, and a world-builder registration point (name/shape TBD
      during implementation — needs to express "load persisted tree, else
      call the consumer's builder, then save" without hardcoding
      `HubWorldBuilder`)
- [ ] **Verify**: does the generic host's default `ConsoleLifetime` handle
      `SIGTERM` correctly on Unix out of the box? If yes, delete the
      hand-rolled `PosixSignalRegistration` code instead of porting it
      into `Hosting` — don't re-solve an already-fixed problem blind to
      whether it's already fixed. If no, port the existing fix forward.
- [ ] XML doc comments on every public member per `documentation.md`

### `SharpMud.Persistence` split

- [ ] Remove `Microsoft.EntityFrameworkCore.Sqlite`/
      `SQLitePCLRaw.lib.e_sqlite3` `PackageReference`s from
      `SharpMud.Persistence.csproj` — core stays provider-agnostic
      (`Microsoft.EntityFrameworkCore`/`.Relational` only)
- [ ] New `src/SharpMud.Persistence.Sqlite/` — the removed package refs,
      plus `AddSharpMudSqlitePersistence(string dbPath)` extension
      wrapping today's `UseSqlite(...)` call site (moved out of
      `Program.cs`)
- [ ] **Verify before building `SharpMud.Persistence.DynamoDb`**: does
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
- [ ] New `src/SharpMud.Persistence.DynamoDb/` — references
      `EntityFrameworkCore.DynamoDb 10.0.0` (the current stable release,
      confirmed against NuGet directly — see ADR-0006's package table),
      targeting this project's `net10.0` TFM specifically since the
      provider is an EF Core 10 build, not EF Core 11, equivalent
      `AddSharpMudDynamoDbPersistence(...)` extension
- [ ] Update `samples/SharpMud.Samples.Classic` to consume
      `SharpMud.Persistence.Sqlite`'s extension instead of the inline
      `UseSqlite(...)` call

### Packaging metadata + CI

- [ ] Root `Directory.Build.props` — `PackageLicenseExpression` (MIT),
      `RepositoryUrl`, `Authors`, `PackageProjectUrl`, `PackageIcon`,
      `PackageReadmeFile`, `GenerateDocumentationFile`, `DebugType=embedded`,
      `Microsoft.SourceLink.GitHub` — matching `optimized-enums`'/
      `structured-logging`'s shape exactly; no `VersionPrefix` (release-drafter
      resolves the version, per ADR-0006)
- [ ] Root `icon.png`
- [ ] Root `LICENSE` (MIT)
- [ ] `NOTICE.md` (or a README section) crediting WheelMUD as design
      inspiration, per ADR-0006's License and naming section — not a legal
      requirement, matches this project's existing citation discipline
- [ ] Set `IsPackable=true` (or leave default) on every `src/` project
      above; confirm `samples/` projects are `IsPackable=false` explicitly
      so a solution-wide `dotnet pack` never emits sample packages
- [ ] New `src/SharpMud/SharpMud.csproj` — the meta-package: no code,
      **`ProjectReference`s (not `PackageReference`s) to every other
      `SharpMud.*` project** — verified experimentally: `dotnet pack`
      automatically translates a `ProjectReference` to a packable project
      into a `<dependency>` entry in the resulting `.nuspec`, at that
      project's own version, with no requirement that the referenced
      package already exist on any feed. A literal `PackageReference`
      instead would fail restore on the very first `dotnet pack
      SharpMud.slnx` — none of the sibling packages exist on any feed
      until *after* that first pack/publish — caught in PR review.
- [ ] `.github/workflows/publish-preview.yaml` — push to `main` →
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
- [ ] `.github/workflows/publish-release.yaml` — same shape, triggered on
      `release: published`
- [ ] Multi-target `net10.0;net11.0` on every packaged `src/` project;
      **verify** the codebase actually compiles clean against `net10.0` —
      if any `net11.0`-only API is in use, resolve via `#if` gating or
      confirm it's acceptable to require `net11.0` after all (a real
      finding, not assumed clean)

### Documentation site (GitHub Pages)

- [ ] New top-level `docsite/` directory (exact name TBD) — the Zensical
      site source, kept separate from `docs/`'s existing ADR/plan/subsystem
      content per ADR-0006's Documentation site section
- [ ] `pyproject.toml`/`uv.lock` (Zensical + `mdformat` toolchain, matching
      `dynamodb-efcore-provider`'s dependency set), `zensical.toml` with a
      minimal curated `nav` (Home, Getting Started to start — expand as
      real content lands)
- [ ] `.github/workflows/docs.yaml` — PR builds (no deploy) + push-to-`main`
      build/deploy via `actions/upload-pages-artifact`/`actions/deploy-pages`,
      matching `dynamodb-efcore-provider`'s workflow shape exactly
      (`uv sync --locked --all-extras --dev` → `uv run zensical build`)
- [ ] Enable GitHub Pages (Pages source: GitHub Actions) in repo settings
- [ ] Minimal skeleton content: a home page and one real "Getting Started"
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
- [ ] `README.md` (root) — update to describe the package-based consumption
      story once implemented, not before

## Critical files

New:
- `docs/adr/0006-nuget-package-distribution.md`
- `docs/plans/0006-nuget-package-distribution.md`
- `src/SharpMud.Hosting/*` — new `SharpMudApplicationBuilder`/
  `SharpMudApplication`/`SharpMudOptions`, plus `SessionLoop.cs`/
  `LoginFlow.cs`/`PasswordHashing.cs`/`HostOptions.cs` moved in from
  `src/SharpMud.Host`
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
- `tests/SharpMud.Hosting.Tests/*` (new project, plus `SessionLoopTests.cs`/
  `LoginFlowTests.cs`/`HostOptionsTests.cs`/`PasswordHashingTests.cs`
  moved in from `tests/SharpMud.Host.Tests`)

Modified:
- `src/SharpMud.Persistence/SharpMud.Persistence.csproj` (drop SQLite refs)
- `SharpMud.slnx`
- `.agents/skills/engineering-workflow/references/coding-standards.md`
- `docs/adr/README.md`, `docs/plans/README.md`, `docs/engine-vs-ruleset.md`, `docs/deployment.md`, `README.md`

## Test plan

- Unit tests for `SharpMud.Hosting`'s new surface
  (`SharpMudApplicationBuilder`/`SharpMudApplication` wiring,
  `SharpMudOptions` binding) — new coverage, matching `testing.md`'s
  conventions.
- Existing `SharpMud.Persistence.Tests` split/adjusted to match the
  core/`Sqlite`/`DynamoDb` project split — regression coverage, not new
  behavior.
- `tests/SharpMud.Hosting.Tests` (`SessionLoopTests`, `LoginFlowTests`,
  `HostOptionsTests`, `PasswordHashingTests`, moved from
  `SharpMud.Host.Tests` per the Repository reorganization task) —
  regression coverage, carried forward unmodified.
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
  works, not just that the monorepo still builds. Both `TransportMode.Cli`
  and `TransportMode.Telnet` need a real pass, matching this repo's
  established pattern for anything session/networking-facing.
- `publish-preview.yaml` produces a real preview package on a push to
  `main` in a throwaway branch/PR before merging this to `main` for real.
- Confirm `SIGINT`/`SIGTERM` shutdown still saves the world correctly
  under the new `BackgroundService`-based `GameLoop`/Telnet listener —
  this is the exact bug class `docs/persistence.md` already found and
  fixed once; don't regress it silently under the new hosting model.

## Open questions / blockers

- Whether `EntityFrameworkCore.DynamoDb` supports EF Core's TPH
  `HasDiscriminator<string>(...)` (used by `BehaviorConfiguration` for the
  `Behavior` subtype hierarchy) is unconfirmed — flagged in PR review.
  `ToTable(...)` and single-property `HasKey(...)` are confirmed supported
  per the provider's own docs, but the discriminator/inheritance question
  is open. Resolve via the explicit verification task in the
  `SharpMud.Persistence` split section above before assuming the shared
  `Configurations/` tree works unmodified against DynamoDB.
- Exact shape of the world-builder registration point on
  `SharpMudOptions`/the builder (how a consumer plugs in their own
  `HubWorldBuilder`-equivalent) isn't fully designed — implementation will
  need to work this out concretely, it's sketched but not nailed down in
  ADR-0006.
- Whether `GameLoop` itself should take a direct `Microsoft.Extensions.Hosting`
  dependency (become a `BackgroundService` itself) or stay hosting-agnostic
  with a thin wrapper in `SharpMud.Hosting` — implementation-time call, not
  pre-decided.
- Exact name for the docs-site source directory (`docsite/` used as a
  placeholder throughout this plan) isn't locked in.
- Full docs-site content (per-package configuration reference, a
  data-modeling-equivalent guide, samples walkthroughs beyond the one
  Getting Started page) is real, separate writing work — not scheduled
  against this plan, needs its own follow-up plan once the package
  mechanics this plan covers are actually done and stable enough to
  document accurately.
