# [ADR-0006] NuGet Package Distribution + Sample-Based Ruleset Extraction

**Status:** Accepted

**Date:** 2026-07-20

**Decision Makers:** Nick Cipollina

## Context

`SPEC.md`'s engine-vs-ruleset pivot (see
[engine-vs-ruleset.md](../engine-vs-ruleset.md)) already committed sharp-mud
to being "a redistributable engine other games can be built on," modeled on
WheelMUD. That doc already states the target shape in words — *"Host is the
only project allowed to know about a specific ruleset... a different game
would be `SharpMud.Ruleset.SciFi`... with its own `Host`-equivalent wiring
it in instead of `Ruleset.Classic`"* — but never answers the mechanical
question: how does an actual third-party consumer, in their own repo, get
`SharpMud.Engine` at all? Today the only way is cloning this repo and
editing `src/` in place. This ADR answers that.

Two things changed since `engine-vs-ruleset.md` was written that make this
answerable now instead of speculative:

1. `Program.cs` (`src/SharpMud.Host`) was read end-to-end as part of this
   design: of its ~140 lines, only ~4 are actually
   `SharpMud.Ruleset.Classic`-specific (`ClassicBehaviorMappingContributor`,
   `ClassicCommands.RegisterAll(...)`, the `Ruleset.Classic` using
   statements, and `HubWorldBuilder`, which is sample hub content, not
   engine). Everything else — DI/config/logging setup, DbContext bootstrap,
   world load-or-build-and-save, `GameLoop` assembly, `SIGINT`/`SIGTERM`
   handling, the Telnet-vs-CLI branch, shutdown save — is generic engine
   plumbing every consumer would otherwise have to hand-roll from scratch.
   Shipping only raw types (`Thing`/`Behavior`/`ICommand`) and calling the
   distribution problem solved would mean every consumer re-derives that
   ~130 lines themselves.
2. The org already has a proven CI/packaging pattern for exactly this shape
   (`LayeredCraft.OptimizedEnums`, `LayeredCraft.StructuredLogging`):
   `Directory.Build.props` for shared package metadata,
   `devops-templates`' reusable `publish-preview.yml`/`publish-release.yml`
   workflows (preview builds on push to `main`, real releases on a
   published GitHub Release), version resolved automatically by
   `release-drafter` from conventional-commit PR titles — no manual version
   bumping. sharp-mud already installed the `release-drafter`/
   `dependabot`/PR-title-check half of this in a prior PR; this ADR is the
   rest.

## Decision Drivers

- Minimize consumer friction: pull a package (or a few), write your own
  `Ruleset` code, and a couple of lines of `Program.cs` — not "clone this
  repo and edit two projects in place."
- Preserve flexibility: a consumer who only wants the Telnet adapter, or
  wants to swap SQLite for DynamoDB, shouldn't be forced to take everything.
- Don't reopen the MEF/dynamic-loading question `engine-vs-ruleset.md`
  already closed (Open Items: *"No `AssemblyLoadContext`-based dynamic
  ruleset loading"*) — a consumer's `Ruleset` is still a compile-time
  project reference in *their* repo, never something we reflect over or
  dynamically load in ours.
- Don't front-load operational cost we can't justify — but unlike the
  precedent below, sharp-mud's target consumers are genuinely external and
  unknown, so the cost of packaging is unavoidable, not speculative.

## Considered Options

1. **Monorepo + project references only, defer NuGet** — the pattern used
   in a related project (`trivia-platform`'s ADR-0017, "Shared Trivia Engine
   in a Platform Monorepo," an external repo not part of this one — see
   discussion below), where a shared engine is consumed in-repo via
   `ProjectReference` and packaging is explicitly deferred until a real
   external consumer justifies it.
2. **Single fat NuGet package**, bundling every project's compiled output
   into one `.nupkg` (the way `LayeredCraft.OptimizedEnums`'s generator
   package embeds its `.Core` project's DLL directly).
3. **Granular ala-carte packages per project, plus one umbrella
   meta-package, plus a new `SharpMud.Hosting` package** that wraps
   `Microsoft.Extensions.Hosting` to absorb the generic-plumbing problem
   from driver 1 (chosen).
4. **Ship only raw engine types, no `Hosting` package** — package the
   pieces but leave consumers to hand-roll their own `Program.cs` composition
   root from scratch, same as today's `SharpMud.Host` does.

## Decision Outcome

Chosen option: **Option 3 — granular packages, a `SharpMud.Hosting`
composition-root package, and a meta-package**, because it's the only option
that satisfies both "minimal friction" and "ala-carte flexibility" at once,
and because sharp-mud's actual target consumers (unknown third parties, not
another product on this team) make Option 1's core justification — no real
external consumer yet — not true here.

### Package set

No `LayeredCraft.` prefix — matching `EntityFrameworkCore.DynamoDb`'s own
naming (a product bigger than the org utility packages that do carry the
prefix, like `LayeredCraft.StructuredLogging`), not `LayeredCraft.SharpMud.*`.
Every package ID matches its project/assembly name 1:1 already, so no
`PackageId` overrides are needed except on the new meta-package.

| Package | Contents | Provider deps |
|---|---|---|
| `SharpMud.Engine` | `Thing`/`Behavior`/events/commands/sessions — unchanged from today | none |
| `SharpMud.Persistence` | `GameDbContext`, `ThingRepository`, EF Core `Configuration` classes — **provider-agnostic**, no provider `PackageReference` | `Microsoft.EntityFrameworkCore(.Relational)` only |
| `SharpMud.Persistence.Sqlite` | thin, adds `Microsoft.EntityFrameworkCore.Sqlite` + an `AddSharpMudSqlitePersistence(path)` extension | SQLite |
| `SharpMud.Persistence.DynamoDb` | same shape, wraps `EntityFrameworkCore.DynamoDb` | DynamoDB (provider is out of preview per its `v10.0.1`/EF Core 10 release; confirm the EF Core 11 line's actual release status against sharp-mud's target TFM at implementation time — see Open Items) |
| `SharpMud.Adapters.Telnet` | unchanged | none |
| `SharpMud.Adapters.Cli` | unchanged | none |
| `SharpMud.Hosting` | **new** — `SharpMudApplicationBuilder`/`SharpMudApplication` (see below), `SharpMudOptions` | none beyond `Microsoft.Extensions.Hosting` |
| `SharpMud` | **new**, meta-package — no code, `PackageReference`s to everything above | — |

Not built speculatively: Postgres/SqlServer/etc. adapters. A consumer can
already point core `GameDbContext` at any relational provider themselves
(`UseNpgsql(...)`, etc.) without us shipping a package for it — the same
posture EF Core's own ecosystem takes; nobody expects Microsoft to publish
every third-party provider.

### `SharpMud.Hosting` shape

A thin facade over `Microsoft.Extensions.Hosting`, not a reimplementation
of it — the relationship `WebApplicationBuilder` has to `HostApplicationBuilder`.
This is deliberately smaller than `minimal-lambda`'s `LambdaApplicationBuilder`
(considered as prior art, see Pros and Cons below): Lambda's execution model
is fundamentally *not* "run forever" (a function is invoked per-event), so
`minimal-lambda` had to build a custom middleware pipeline, invocation
builder, and on-init/on-shutdown builder factories on top of the generic
host. sharp-mud's execution model — a persistent process running background
services (`GameLoop`, the Telnet listener) with one graceful shutdown — is
exactly the case `HostApplicationBuilder`/`IHost` already models, so
`SharpMud.Hosting` only needs to be a named entry point plus sharp-mud's own
`BackgroundService` registrations, roughly:

```csharp
public sealed class SharpMudApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _hostBuilder;
    internal SharpMudApplicationBuilder(string[]? args) { /* wraps Host.CreateApplicationBuilder */ }
    public IServiceCollection Services => _hostBuilder.Services;
    // ... rest delegates straight through, same relationship WebApplicationBuilder has to its inner builder
    public SharpMudApplication Build() => new(_hostBuilder.Build());
}

public sealed class SharpMudApplication : IHost { /* wraps the built IHost; RunAsync delegates straight to it */ }
```

Consumer's `Program.cs`:

```csharp
var builder = SharpMudApplication.CreateBuilder(args);
builder.Services.AddSharpMudRuleset(registry => MyRulesetCommands.RegisterAll(registry, ...));
builder.Services.Configure<SharpMudOptions>(o => o.Transport = TransportMode.Telnet);

var mud = builder.Build();
await mud.RunAsync();
```

`GameLoop` and the Telnet listener become ordinary `BackgroundService`
registrations rather than hand-rolled `await`ed calls, so shutdown goes
through `IHost`'s own lifetime management. Worth verifying at implementation
time whether the generic host's default `ConsoleLifetime` already handles
`SIGTERM` correctly on Unix — if so, the hand-rolled `PosixSignalRegistration`
workaround `Program.cs` currently needs (added to fix a real bug: 
`Console.CancelKeyPress` never catches `SIGTERM`, see `docs/persistence.md`)
can be deleted entirely rather than re-solved.

`SharpMudOptions` is a real `IOptions<T>`-shaped class (per
`coding-standards.md`'s existing convention) covering code-level wiring
choices (`TransportMode`, `TelnetPort`) — it stays a **separate concern**
from `HostOptions.Parse`'s env-var/secrets path (`security.md`'s reason for
keeping that one manual, non-`IOptions<T>`, still applies and doesn't
change here).

This is the first sanctioned instance of a DI-extension/builder composition
pattern in this repo — `coding-standards.md`'s DI/composition section
currently reads as a blanket prohibition on this; that line described the
codebase's state at the time it was written, not a standing rule, and is
corrected in this same change (see Critical files in the plan).

### Repository reorganization

`src/SharpMud.Host`'s current role — the only project allowed to reference a
specific ruleset, per `engine-vs-ruleset.md` — is now recognized as *sample*
content, not shipped-package content: it's this repo's own reference
implementation of what any consumer's `Program.cs` looks like, not something
we distribute.

```
src/                                    (packaged)
  SharpMud.Engine/
  SharpMud.Hosting/                     new
  SharpMud.Persistence/
  SharpMud.Persistence.Sqlite/          new
  SharpMud.Persistence.DynamoDb/        new
  SharpMud.Adapters.Telnet/
  SharpMud.Adapters.Cli/

samples/                                (NOT packaged — reference implementation)
  SharpMud.Samples.Classic/             single project — merges
                                         SharpMud.Ruleset.Classic's content
                                         (moved from src/, unchanged
                                         internally) and SharpMud.Host's
                                         Program.cs (rewritten against
                                         SharpMud.Hosting) into one project
```

This is a genuinely different shape from today's repo, not just a rename:
today `Ruleset.Classic` and `Host` are two separate `src/` projects (the
dependency-direction rule in effect since the original engine-vs-ruleset
split — `Host` references `Ruleset.Classic`, never the reverse). The whole
point of `SharpMud.Hosting` is that a consumer needs **exactly one project**
of their own — ruleset code and `Program.cs` living together, per this
ADR's Decision Drivers. A two-project sample would demonstrate the opposite
of what this ADR set out to prove, so `SharpMud.Ruleset.Classic` and the old
`SharpMud.Host` are consolidated into one `samples/SharpMud.Samples.Classic/`
project — the ruleset behaviors/commands and the few lines of `Program.cs`
using `SharpMud.Hosting`'s builder live side by side in the same project,
exactly as an external consumer's own project would.

`engine-vs-ruleset.md`'s "Host is the only project allowed to know about a
specific ruleset" principle doesn't change in spirit — it's just no longer
*this repo's* separate `Host` project that matters; every consumer
(including our own sample) has exactly one project that knows about its
ruleset, and that project is also where their composition root lives.

### License and naming

MIT, matching every other LayeredCraft OSS package. WheelMUD (the prior art
this design is adapted from, see `docs/research/wheelmud-findings.md`) is
licensed MS-PL, but sharp-mud is a clean-room reimplementation — the
findings doc already documents "adopted vs. not adopted" from *reading*
WheelMUD's architecture, not from copying its source. MS-PL's
attribution/same-license conditions apply to redistributing WheelMUD's own
code or derivative works of it; they don't attach to independently-written
code inspired by its design (ideas and patterns aren't copyrightable, only
literal expression is). A `NOTICE.md` or README credit to WheelMUD as design
inspiration is good practice, matching this project's existing citation
discipline, but not a license-compatibility requirement.

### Versioning and CI

No new mechanism needed. `release-drafter`/`dependabot`/PR-title-check are
already installed (prior PR). Add `publish-preview.yaml` (push to `main` →
preview build) and `publish-release.yaml` (published GitHub Release → real
build) pointed at `SharpMud.slnx`, matching `structured-logging`'s/
`optimized-enums`' repo-level workflow files exactly — `release-drafter`
resolves the next SemVer from conventional-commit PR titles, no manual
`VersionPrefix` bump. Because every package packs from one solution in one
CI run, versioning is lockstep across all of them by construction — not a
separate design choice.

`publish-preview.yaml` passes `prereleaseIdentifier: alpha` (the reusable
workflow's input defaults to `preview`) — packages publish as
`X.Y.Z-alpha.<run_number>` while this whole package set is in its initial
alpha stage, not `-preview.<run_number>`. Revisit this input (drop it, or
switch to `preview`/`beta`) once the package set graduates out of alpha —
that's a one-line workflow change when it happens, not a decision this ADR
needs to pre-commit to a timeline for.

### Target frameworks

Multi-target `net10.0;net11.0` for the packages — `net10.0` (the current
stable/GA .NET release) so external consumers aren't forced onto the
preview SDK sharp-mud itself runs on (`global.json` pins `net11.0` to a
preview build), and `net11.0` to stay current with what this repo actually
targets. `net9.0` was considered and rejected — no reason to reach back an
extra major version when `net10.0` already covers "a stable SDK today."
Needs verification at implementation time that `src/` code actually
compiles clean against `net10.0` (no accidental `net11.0`-only API usage);
if something genuinely needs conditional compilation, that's a real, not
hypothetical, cost — see Negative Consequences.

### Documentation site

End-user documentation (getting started, per-package configuration,
"how do I actually build a MUD with this") publishes via GitHub Pages,
following `dynamodb-efcore-provider`'s established shape: Zensical (a
Python/`uv`-driven static site generator) building `docs/` per `zensical.toml`'s
curated `nav`, deployed via `actions/upload-pages-artifact`/
`actions/deploy-pages` on push to `main`, with a PR-preview build (no
deploy) on pull requests.

That repo's `docs/` folder mixes curated end-user pages (`getting-started.md`,
`configuration/`, `modeling/`, etc. — the ones actually listed in
`zensical.toml`'s `nav`) with internal design notes
(`multi-version-ef-strategy.md`, `alpha-exit-priorities.md`,
`spec-test-*-audit.md` — not in the nav, but still physically present in
the same folder). sharp-mud's `docs/` already has an established, different
meaning — ADRs, plans, subsystem docs, research notes, per this repo's
`engineering-workflow` skill — and that meaning predates this ADR and stays
exactly as it is. Reusing `docs/` as the Zensical site source would mean
every ADR/plan/research note either needs curating out of the nav by hand
forever, or risks being reachable at a direct URL on a public site even
when unlisted. To avoid that ambiguity entirely, the site's source content
lives in a **new, separate top-level directory** (`docsite/`, or similar —
exact name TBD at implementation time) — sharp-mud's internal `docs/` and
the public docs site are two different trees with two different audiences,
not one folder serving both.

Scope for *this* plan: the mechanism (workflow, `zensical.toml`, GitHub
Pages settings) plus a minimal skeleton (home page, a real "Getting
Started" walkthrough using the actual packages this ADR produces) — enough
to prove the pipeline works end-to-end, the same bar `SharpMud.Hosting`'s
sample is held to elsewhere in this ADR. A full content build-out
(per-package configuration references, a data-modeling guide, etc.,
matching the depth of `dynamodb-efcore-provider`'s site) is real,
substantial writing work independent of the packaging mechanics this ADR
is actually about — tracked as explicit follow-up in the plan's Open
Questions, not silently bundled into "done" for this plan.

### Positive Consequences

- A consumer needs exactly one project of their own (their `Ruleset` code
  and `Program.cs` can live together) — not two, and not a repo clone.
- Ala-carte packages mean a consumer who doesn't want Telnet, or wants
  DynamoDB instead of SQLite, isn't forced to take the other.
- `IThingRepository` already living in `SharpMud.Engine` (not `Persistence`)
  means a consumer can always skip the `Persistence` packages entirely and
  implement their own repository — de-risks publishing `Persistence` before
  its EF Core provider story is fully settled.
- Reuses proven org CI/packaging patterns wholesale (`devops-templates`
  reusable workflows, `release-drafter`-driven versioning) — near-zero new
  operational surface despite this being a genuinely public package, unlike
  a from-scratch pipeline.
- `SharpMud.Hosting` being a thin wrapper (not a Lambda-style
  reimplementation) keeps its own maintenance/test surface small.

### Negative Consequences

- **Public API surface becomes a real compatibility promise.**
  `coding-standards.md` already flags that every type in this codebase is
  `public` today (no `internal` usage anywhere) — that was a tolerable gap
  for an app; it's a real liability the moment these projects are published
  packages. Not fixed here — tracked as an Open Item below, an audit pass
  (default new/touched types to `internal`) before or shortly after first
  publish, not a blocker on this ADR.
- **Lockstep versioning across ~8 packages** means an unrelated fix to
  `SharpMud.Adapters.Telnet` forces a release (version bump) of
  `SharpMud.Engine` too, even if it didn't change. Accepted — this already
  matches the established org pattern (`optimized-enums` ships multiple
  packages from one repo/one version the same way) and multi-repo
  per-package versioning has no precedent here to justify its extra
  ceremony yet.
- **First instance of the DI-extension/builder composition pattern** in
  this repo — `SharpMud.Hosting` is new code with its own testing burden
  (`Microsoft.Extensions.Hosting.Testing` or equivalent), not a
  repackaging of something already proven here.
- Multi-targeting `net10.0` alongside `net11.0` is unverified against the
  current `net11.0`-only codebase; if it forces `#if`-gated code anywhere,
  that's a genuine ongoing cost, not a one-time setup cost.
- A separate `docsite/`-style directory for the docs site (rather than
  reusing `docs/`) is another tree to keep in sync — a getting-started page
  can drift from the actual package shape the same way any doc can drift
  from code, just in a second location now instead of one.

## Pros and Cons of the Options

### Option 1 — Monorepo + project references, defer NuGet

The pattern actually used in a related project
(`trivia-platform`'s ADR-0017, "Shared Trivia Engine in a Platform
Monorepo") — a shared engine consumed in-repo via `ProjectReference`,
packaging explicitly deferred until pain/real consumers justify it.

- Good, because it has real, working precedent from the same author.
- Good, because it avoids all packaging/SemVer/public-API-governance cost
  up front.
- Bad, because that ADR's core justification — *"no real second consumer
  exists yet to validate the abstraction against"* — is specifically false
  for sharp-mud: the whole point of the engine-vs-ruleset pivot is
  unknown, external, third-party consumers, not another in-house product in
  this same repo. You cannot `ProjectReference` into someone else's repo.
- Bad, because it doesn't answer the actual question this ADR needs to
  answer (Context) at all.

### Option 2 — Single fat package

Bundle every project's compiled output into one `.nupkg`, the way
`LayeredCraft.OptimizedEnums`'s generator package embeds its `.Core`
project's DLL directly.

- Good, because it's the simplest possible `dotnet add package` story.
- Bad, because that embedding pattern exists in `OptimizedEnums` for a
  source-generator-specific reason (an analyzer package must ship as one
  package) that doesn't apply to ordinary class libraries.
- Bad, because it forecloses the ala-carte flexibility explicitly named as
  a decision driver — a consumer wanting only `Engine` still gets
  `Adapters.Telnet`/`Persistence`/etc. bundled in regardless.
- Bad, because independent assembly versioning/trimming/AOT concerns get
  harder once everything is one physical package.

### Option 3 — Granular + meta-package + `SharpMud.Hosting` (chosen)

See Decision Outcome above.

- Good, because it satisfies both "minimal friction" (meta-package, plus
  `Hosting` absorbing the boilerplate) and "ala-carte flexibility"
  (granular packages) at once — the two decision drivers that were in
  tension.
- Good, because the granular split lines up with a real, load-bearing
  seam that already exists in the code (`IThingRepository` in `Engine`,
  provider-specific bits isolated in `Persistence.*`), not an arbitrary cut.
- Bad, because it's the most new surface area of any option — a new
  `Hosting` project, a new meta-package project, and two new `Persistence.*`
  projects, all needing their own `csproj`/tests/docs.

### Option 4 — Ship raw pieces only, no `Hosting` package

Package `Engine`/`Persistence`/`Adapters.*` as-is; consumers hand-roll their
own composition root the way today's `SharpMud.Host` does.

- Good, because it's less new code than Option 3 — no `SharpMud.Hosting`
  project to build or maintain.
- Bad, because it directly contradicts the stated goal ("as little friction
  as possible... a line or two in their own `Program.cs`") — per Context,
  ~130 of `Program.cs`'s ~140 lines are generic plumbing every consumer
  would otherwise reinvent by hand.
- Bad, because it leaves the actual problem this ADR exists to solve
  unsolved, just with the types now importable from a package instead of a
  cloned repo.

## Links

- [engine-vs-ruleset.md](../engine-vs-ruleset.md) — the "Host is the only
  project allowed to know about a specific ruleset" principle this ADR
  builds on
- [docs/research/wheelmud-findings.md](../research/wheelmud-findings.md) —
  WheelMUD prior art and license citation
- [PLAN-0006](../plans/0006-nuget-package-distribution.md)
- `coding-standards.md`'s DI/composition section (corrected in this change)
- WheelMUD license: <https://github.com/DavidRieman/WheelMUD/blob/main/src/LICENSE.txt>
  (MS-PL — see License and naming above for why this doesn't constrain
  sharp-mud's own license choice)
