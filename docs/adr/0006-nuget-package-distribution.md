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
| `SharpMud.Persistence` | `GameDbContext`, `ThingRepository`, EF Core `Configuration` classes — **provider-agnostic at the package-reference level** (no provider `PackageReference`), but see the caveat below the table — the *model config* isn't yet confirmed provider-agnostic in practice | `Microsoft.EntityFrameworkCore(.Relational)` only |
| `SharpMud.Persistence.Sqlite` | thin, adds `Microsoft.EntityFrameworkCore.Sqlite` + an `AddSharpMudSqlitePersistence(path)` extension | SQLite |
| `SharpMud.Persistence.DynamoDb` | same shape, wraps `EntityFrameworkCore.DynamoDb` | DynamoDB — pin `EntityFrameworkCore.DynamoDb 10.0.0`, the current stable/non-preview release (see the stability note below the table). Targets EF Core 10, not EF Core 11 — see Target frameworks below for why sharp-mud packages multi-target `net10.0;net11.0` rather than `net11.0` alone, which is exactly what makes this pin usable now instead of blocked on an EF Core 11 build. |
| `SharpMud.Adapters.Telnet` | unchanged, plus a new `AddSharpMudTelnetTransport(...)` DI extension (see `SharpMud.Hosting` shape below for why transport wiring lives *here*, not in `Hosting` itself) | none |
| `SharpMud.Adapters.Cli` | unchanged, plus a new `AddSharpMudCliTransport(...)` DI extension, same reasoning | none |
| `SharpMud.Hosting` | **new** — `SharpMudApplicationBuilder`/`SharpMudApplication` (see below; no separate `SharpMudOptions` type — see below for why), plus `SessionLoop` (parameter-object/service-class refactored — see below)/`PasswordHashing`/`PlayerLogin`/`LoginFlow`/`HostOptions` (trimmed to `DbPath`) moved in from today's `src/SharpMud.Host` **after decoupling `LoginFlow`/`PlayerLogin` from `HubWorldBuilder.CreatePlayer`** (see below — they are not ruleset-agnostic as currently written) | `Microsoft.Extensions.Hosting` **and** `Microsoft.Extensions.Identity.Core` — `PasswordHashing.cs` uses `Microsoft.AspNetCore.Identity`'s `PasswordHasher<TUser>`, supplied by that package (caught in PR review: an earlier version of this table said "none beyond `Microsoft.Extensions.Hosting`," which was wrong). **Not** `Adapters.Telnet`/`Adapters.Cli` (see below). |
| `SharpMud` | **new**, meta-package — no code, `ProjectReference`s (not `PackageReference`s — see the meta-package note below) to everything above | — |

**On `EntityFrameworkCore.DynamoDb 10.0.0`'s stability, since this has now
been flagged in review three times and is wrong every time:** confirmed
live, independently, against four separate sources, not just one version
list that could be stale or misread:
- NuGet's flat-container version index (`v3-flatcontainer/.../index.json`)
  lists `10.0.0` with no prerelease suffix.
- NuGet's registration index for that exact version
  (`v3/registration5-semver1/entityframeworkcore.dynamodb/10.0.0.json`)
  shows `"listed": true`, `"published": "2026-07-14T12:16:32.32+00:00"`.
- NuGet's Azure Search endpoint — the same restore/search-facing API
  `dotnet restore`/`nuget.exe` actually query — returns `"version":
  "10.0.0"` as the top hit with `prerelease=false`.
- `libraries.io`'s own API for this package (the same source cited *for*
  the preview claim, twice now) reports `"latest_stable_release_number":
  "10.0.0"` and `"latest_stable_release_published_at": "2026-07-14T..."` —
  i.e. it actually agrees `10.0.0` is stable; the preview claim has never
  matched the source it cites.

If a future review flags this again, re-verify against one of the four
URLs above directly rather than trusting a version list or a bot's summary
of one.

Not built speculatively: Postgres/SqlServer/etc. adapters. A consumer can
already point core `GameDbContext` at any relational provider themselves
(`UseNpgsql(...)`, etc.) without us shipping a package for it — the same
posture EF Core's own ecosystem takes; nobody expects Microsoft to publish
every third-party provider.

**Caveat, flagged in PR review and not yet resolved by this ADR:**
"provider-agnostic" above is currently only verified at the *package
reference* level — no provider-specific `PackageReference` in
`SharpMud.Persistence.csproj` — not at the *model configuration* level.
`ThingConfiguration`/`BehaviorConfiguration` already call
`builder.ToTable(...)` today, which is a `Microsoft.EntityFrameworkCore
.Relational` API; `EntityFrameworkCore.DynamoDb`'s own docs confirm
`ToTable(...)` is one of its supported table-mapping mechanisms, and its
single-property `HasKey(...)` fallback should cover `ThingConfiguration`'s
`HasKey(x => x.Id)`/`BehaviorConfiguration`'s `HasKey(x => x.PersistenceKey)`
— but `BehaviorConfiguration` also uses `HasDiscriminator<string>(...)`
for TPH inheritance mapping, and DynamoDB's item model has no verified
equivalent for that. This needs to be checked against the DynamoDB
provider's actual modeling docs, not assumed, before
`SharpMud.Persistence.DynamoDb` is treated as a drop-in swap over the same
shared `Configurations/` — see PLAN-0006's explicit verification task and
Open Questions entry. If TPH/discriminator support turns out to be
missing, the fallback is splitting provider-specific configuration
(a Dynamo-specific partial config, or a different inheritance-mapping
strategy for that provider) rather than assuming one shared config tree
works unmodified everywhere.

**Meta-package mechanics, flagged in PR review:** the `SharpMud` package's
dependency list on the other `SharpMud.*` packages must be expressed as
`ProjectReference` in `src/SharpMud/SharpMud.csproj`, not a literal
`PackageReference`. Verified experimentally (a throwaway two-project repro):
`dotnet pack` automatically translates a `ProjectReference` to a packable
project into a `<dependency>` entry in the resulting `.nuspec`, at that
project's own version — with no requirement that the referenced package
already exists on any feed. A literal `PackageReference` would instead fail
restore on the very first `dotnet pack SharpMud.slnx`, since none of the
sibling packages exist on any feed until *after* that first pack/publish —
exactly the chicken-and-egg problem flagged in review. This is a wording
fix, not an architecture change: the monorepo/single-CI-run packing model
described under Versioning and CI above already assumed this mechanism, it
just wasn't spelled out precisely enough to rule out the broken reading.

### `SharpMud.Hosting` shape

A thin facade over `Microsoft.Extensions.Hosting`, not a reimplementation
of it — the relationship `WebApplicationBuilder` has to `HostApplicationBuilder`.
This is deliberately smaller than `minimal-lambda`'s `LambdaApplicationBuilder`
(considered as prior art, see Pros and Cons below): Lambda's execution model
is fundamentally *not* "run forever" (a function is invoked per-event), so
`minimal-lambda` had to build a custom middleware pipeline, invocation
builder, and on-init/on-shutdown builder factories on top of the generic
host. sharp-mud's execution model — a persistent process running background
services (`GameLoop`, plus whichever transport(s) a consumer registers —
see the transport-wiring note below) with one graceful shutdown — is
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
builder.Services.AddSingleton<ITickable, MyCombatManager>(); // ruleset-specific game-loop work

if (useTelnet) // parsed from SHARPMUD_MODE/--telnet, same precedence as today — see below
    builder.Services.AddSharpMudTelnetTransport(port: telnetPort);
else
    builder.Services.AddSharpMudCliTransport();

var mud = builder.Build();
await mud.RunAsync();
```

**`AddSharpMudRuleset` must register engine built-ins itself, flagged in PR
review.** Today's `Program.cs` calls `BuiltinCommands.RegisterAll(registry)`
*before* `ClassicCommands.RegisterAll(...)` — that's where `look`/`move`/
`quit`/inventory and the rest of the core verbs come from. An earlier
version of this ADR's example only showed the ruleset callback running,
which would mean a consumer following this pattern gets a `Huh?` for every
built-in command. `AddSharpMudRuleset` registers `BuiltinCommands` itself
(engine-level, not ruleset-specific — `Hosting` already depends on
`Engine`) before invoking the consumer's callback, so a consumer only ever
adds *their* ruleset's commands on top, never has to remember to call
`BuiltinCommands.RegisterAll` themselves.

**Ruleset `ITickable`s need a registration path too, flagged in PR
review.** Command registration alone doesn't reproduce today's wiring —
`Program.cs` also constructs `CombatManager` (implements `ITickable`) and
registers it with `GameLoop` so `kill`-started combat actually advances
each tick; `WanderManager`/`LinkdeadSweeper` are the engine-level
equivalent, already `Hosting`'s to register directly since neither is
ruleset-specific. Rather than inventing a second registration callback
alongside `AddSharpMudRuleset` (a growing, ad hoc list of "and also
register X" hooks), `SharpMud.Hosting`'s `GameLoop` `BackgroundService`
constructor-injects `IEnumerable<ITickable>` and registers whatever's
resolved at startup — the consumer just does ordinary DI
(`services.AddSingleton<ITickable, MyCombatManager>()`), the same pattern
already used for `IPlayerFactory` above. No dedicated tickable-registration
API needed; this is what DI resolving a collection of an interface is for.

`GameLoop` becomes an ordinary `BackgroundService` registration inside
`SharpMud.Hosting` rather than a hand-rolled `await`ed call, so shutdown
goes through `IHost`'s own lifetime management. Worth verifying at
implementation time whether the generic host's default `ConsoleLifetime`
already handles `SIGTERM` correctly on Unix — if so, the hand-rolled
`PosixSignalRegistration` workaround `Program.cs` currently needs (added to
fix a real bug: `Console.CancelKeyPress` never catches `SIGTERM`, see
`docs/persistence.md`) can be deleted entirely rather than re-solved.

**The shutdown-time whole-world save needs an explicit owner, flagged in
PR review.** Today's `Program.cs` does one final
`repository.SaveTreeAsync(hubArea, ...)` after the session loop/listener
wind down but before `gameLoopTask` is awaited — a whole-world snapshot
specifically for NPC state (wander position, live combat HP) that isn't
tied to any player session and so isn't already covered by each session's
own on-disconnect save (`docs/persistence.md`). Moving to `IHost`-managed
`BackgroundService`s doesn't give this an owner automatically — a
`BackgroundService`'s work stops when the host stops, it doesn't imply a
save happens on the way out. This needs an explicit hosted service
`StopAsync` override (or an `IHostApplicationLifetime.ApplicationStopping`
callback) inside `SharpMud.Hosting` that performs this save — not
something to leave to "the tests will catch it if it's missing," since
losing NPC state on every graceful redeploy would be a silent regression,
not a loud one.

**Transport wiring does *not* live in `SharpMud.Hosting`, flagged in PR
review.** An earlier version of this ADR had `Hosting` itself instantiate
`TelnetListener` (from `SharpMud.Adapters.Telnet`) and the CLI session type
(from `SharpMud.Adapters.Cli`) as `BackgroundService`s, branching on
`SharpMudOptions.Transport`. That's wrong: it would force `Hosting` to
reference *both* adapter packages, so every `Hosting` consumer takes both
transports regardless of need — directly contradicting the ala-carte
package split this ADR chose Option 3 for. `HostRunner.cs` today already
shows the actual coupling (`using SharpMud.Adapters.Telnet;` — it directly
constructs `TelnetListener`), confirming this isn't hypothetical.

The fix flips the dependency direction: `SharpMud.Adapters.Telnet` and
`SharpMud.Adapters.Cli` each take a reference *on* `SharpMud.Hosting` (not
the other way around) and expose their own DI extension —
`AddSharpMudTelnetTransport(...)`/`AddSharpMudCliTransport(...)` — that
registers a `BackgroundService` doing what `HostRunner.RunTelnetAsync`/the
CLI branch do today, built on `Hosting`'s shared `SessionLoop`/`LoginFlow`.
A consumer calls whichever extension matches the package(s) they actually
referenced; nothing in `Hosting` itself knows Telnet or CLI exist.
The `TransportMode` enum idea from an earlier draft of this ADR is dropped
entirely, since "which transport(s) run" is now just "which adapter
extension(s) got called," not something `Hosting` branches on internally.

**`SharpMudOptions` is removed from this ADR's scope, flagged in PR
review.** An earlier draft had both `SharpMudOptions` and the trimmed
`HostOptions` owning `DbPath`, with no stated precedence between them —
two sources of truth for the same setting, exactly the kind of drift risk
`security.md` already argues against for deployment config. Once
`TransportMode` is gone (moved to adapter extensions, above) and `DbPath`
is gone (redundant with `HostOptions.Parse`, below), there's nothing left
that actually needs a code-configured `IOptions<T>` binding at the
`Hosting` level today — so `SharpMudOptions` isn't introduced as a concrete
type in this plan at all, rather than inventing an empty placeholder class
now and papering over the precedence question. `HostOptions.Parse`
(env-var/CLI-arg driven) stays the single source of truth for `DbPath`,
per `security.md`'s existing reasoning for keeping deployment config
manual rather than `IOptions<T>`-bound. If a genuine code-configured
setting emerges during implementation, that's the time to introduce
`SharpMudOptions` for real — not before there's an actual second setting
to justify the type existing.

This is the first sanctioned instance of a DI-extension/builder composition
pattern in this repo — `coding-standards.md`'s DI/composition section
currently reads as a blanket prohibition on this; that line described the
codebase's state at the time it was written, not a standing rule, and is
corrected in this same change (see Critical files in the plan).

**`LoginFlow`/`PlayerLogin` need decoupling from ruleset-specific character
creation before they can move into `SharpMud.Hosting`, flagged in PR
review.** As written today, `LoginFlow.MaybeCreateAsync` and
`PlayerLogin.ResolveOrCreateAsync` both call `HubWorldBuilder.CreatePlayer(...)`
directly — and `HubWorldBuilder.cs` itself `using`s `SharpMud.Ruleset.Classic`
(it sets `Race`, `CharacterClass`, `StatsBehavior` on the new player). An
earlier version of this ADR described moving `LoginFlow`/`PlayerLogin` into
`Hosting` "unchanged" on the premise that they only depend on
`SharpMud.Engine.*` — true of their own `using` statements, false once you
follow the `HubWorldBuilder.CreatePlayer` call site, which is genuinely
ruleset-coupled. Moving them unchanged would mean `SharpMud.Hosting` either
fails to compile (no reference to a consumer's ruleset) or has to reference
sample/ruleset code, either way breaking the package boundary this whole
ADR exists to establish.

**Fix, revised after further review:** the first draft of this fix added a
raw `Func<World, string, string, Thing, Thing> createPlayer` parameter to
both methods — but `LoginFlow.RunAsync` and `PlayerLogin.ResolveOrCreateAsync`
already sit at `coding-standards.md`'s 4-parameter limit today (`session`/
`world`/`repository`/`startingRoom` for `LoginFlow`; `world`/`repository`/
`name`/`startingRoom` for `PlayerLogin`, both plus a trailing
`CancellationToken` which doesn't count against the limit). A 5th
parameter — raw `Func<>` or not — pushes both over that limit, caught in
PR review.

The actual fix: `LoginFlow`/`PlayerLogin` stop being `public static class`
utility holders and become ordinary constructor-injected service classes,
matching this repo's existing DI/composition convention (`coding-standards.md`
already requires constructor injection into `private readonly` fields for
exactly this kind of dependency) — the same convention `TelnetHostContext`
was introduced for in the first place (see its own doc comment: *"a
parameter object rather than a growing parameter list, per
coding-standards.md's 4-parameter rule"*, ADR-0002). A new interface:

```csharp
public interface IPlayerFactory
{
    Thing CreatePlayer(World world, string username, string passwordHash, Thing startingRoom);
}
```

`LoginFlow`/`PlayerLogin` take `IThingRepository`/`IPlayerFactory` via
constructor injection instead of as method parameters — their `RunAsync`/
`ResolveOrCreateAsync` methods drop back to 3 parameters plus a trailing
`CancellationToken` (`session`/`world`/`startingRoom`), under the limit
again. The consumer's sample registers `services.AddSingleton<IPlayerFactory,
ClassicPlayerFactory>()`, a thin wrapper around `HubWorldBuilder.CreatePlayer`;
a different consumer registers their own ruleset's equivalent. This settles
the "raw `Func<>` vs. named type, DI vs. direct parameter" question a
previous draft of this ADR left open, rather than leaving it open — a named
interface registered via DI is both the fix for the parameter-count
violation and the more idiomatic shape per this repo's own conventions.

**`HostOptions.cs` needs splitting, not a straight move, flagged in PR
review.** Today's `HostOptions` record bundles `DbPath` with `UseTelnet`/
`TelnetPort`, parsed together from `SHARPMUD_MODE`/`SHARPMUD_TELNET_PORT`/
`--telnet`. Moving it into `SharpMud.Hosting` unchanged contradicts the
"no `TransportMode` in `Hosting`" decision above — `UseTelnet`/`TelnetPort`
are exactly the transport-selection concept that decision just removed
from `Hosting`. Split:
- `SharpMud.Hosting.HostOptions` keeps only `DbPath`, parsed from
  `SHARPMUD_DB_PATH` — genuinely transport-agnostic.
- Which transport(s) to run becomes composition-root logic in the
  consumer's own `Program.cs` (deciding whether to call
  `AddSharpMudTelnetTransport(...)`/`AddSharpMudCliTransport()`) rather
  than a shared options type any package parses — an adapter package
  shouldn't have opinions about *whether* the app wants it, only about its
  own configuration once it's wanted.
- The Telnet port specifically becomes `AddSharpMudTelnetTransport(int
  port)`'s own parameter (or a small `SharpMud.Adapters.Telnet`-local
  options type, implementation's call) — not something `Hosting` parses on
  the adapter's behalf.

This has a concrete consequence flagged in PR review: `Dockerfile` still
sets `SHARPMUD_MODE=telnet`/`SHARPMUD_TELNET_PORT` and expects them to
control which transport actually starts. Since that decision now lives in
the sample's `Program.cs`, the sample must actually parse those same
variables (and `--telnet`, matching today's arg-wins-over-env precedence)
and call `AddSharpMudTelnetTransport`/`AddSharpMudCliTransport`
accordingly — this isn't automatic just because `HostOptions` used to
parse them. Skipping this would mean the container's documented default
silently stops starting the Telnet server.

**`TelnetHostContext` needs a home, flagged in PR review.** This record
(`src/SharpMud.Host/TelnetHostContext.cs`) exists today specifically to
satisfy `coding-standards.md`'s 4-parameter rule for `HostRunner.RunTelnetAsync`
(see its own doc comment, citing ADR-0002) — bundling `World`/`Parser`/
`Registry`/`Repository`/`StartingRoom`/`Port`/`Logger` into one parameter
object. Once `HostRunner`'s logic becomes a DI-constructed
`BackgroundService` inside `SharpMud.Adapters.Telnet` (per the transport-
wiring fix above), most of those fields are ordinary constructor-injected
DI dependencies (`World`, `ICommandParser`, `ICommandRegistry`,
`IThingRepository`, `ILogger<TelnetSession>` are all already
DI-registered services) — the manually-assembled parameter-object pattern
`TelnetHostContext` exists for is superseded by DI itself, not carried
forward as a moved file. `Port` becomes `AddSharpMudTelnetTransport(int
port)`'s own parameter, per the `HostOptions` split above. `StartingRoom`
is the one field with no obvious DI-singleton home — it's a specific
`Thing` instance produced by world-building, so where the new
`BackgroundService` gets it is tied to the still-open world-builder
registration point (see Open Items below), not resolved by this note
alone.

**`SessionLoop` needs the same parameter-object/service-class fix as
`LoginFlow`/`PlayerLogin`, flagged in PR review — this plan already fixed
that exact issue elsewhere and missed the sibling.** `SessionLoop.RunAsync`
today takes six non-`CancellationToken` parameters (`World`,
`ICommandParser`, `ICommandRegistry`, `ISession`, `Thing player`,
`IThingRepository`), well past `coding-standards.md`'s 4-parameter limit —
publishing it into `SharpMud.Hosting` "otherwise unchanged" would ship the
first public `Hosting` API already violating this repo's own standard,
for consumers to copy. Same fix as `LoginFlow`/`PlayerLogin`: `SessionLoop`
becomes a constructor-injected service class taking `World`/
`ICommandParser`/`ICommandRegistry`/`IThingRepository` via the constructor,
leaving `RunAsync(ISession session, Thing player, CancellationToken ct)` —
2 parameters plus the trailing token, under the limit.

### Repository reorganization

`src/SharpMud.Host`'s current role as the only project allowed to reference
a specific ruleset (per `engine-vs-ruleset.md`) splits in two, not one —
**not** "all of `Host` becomes sample content," a distinction an earlier
version of this ADR blurred (caught in PR review). `Host`'s composition
root (`Program.cs`) is genuinely sample content — this repo's own reference
implementation of what any consumer's `Program.cs` looks like. But
`SessionLoop`/`LoginFlow`/`PasswordHashing`/`PlayerLogin`/`HostOptions` are
not: `SessionLoop` only depends on `SharpMud.Engine.*` today; `PasswordHashing`
depends on nothing ruleset/sample-specific either (it needs
`Microsoft.Extensions.Identity.Core`, not `SharpMud.Ruleset.Classic` — see
the package table above); `LoginFlow`/`PlayerLogin` are ruleset-agnostic
too *once* decoupled from `HubWorldBuilder.CreatePlayer` per the
`SharpMud.Hosting` shape section above; `HostOptions` is ruleset-agnostic
*once trimmed* to just `DbPath` per the split above (its `UseTelnet`/
`TelnetPort` fields don't move with it). `SessionLoop` specifically is
documented as *"shared by every transport"* (`SPEC.md`,
`docs/networking.md`). All of it moves into `SharpMud.Hosting` — the
package — not `samples/`; otherwise a consumer installing `SharpMud.Hosting`
would still have no way to accept a login or run a session loop without
copying sample code, which is the opposite of this ADR's "minimal
friction" decision driver.

`HostRunner.cs` (the Telnet-specific connection-accept loop) does **not**
move into `SharpMud.Hosting` as-is either — its logic becomes
`SharpMud.Adapters.Telnet`'s own `AddSharpMudTelnetTransport(...)`
extension, per the transport-wiring fix above, since `Hosting` itself must
not reference `Adapters.Telnet`/`Adapters.Cli`.

```
src/                                    (packaged)
  SharpMud.Engine/
  SharpMud.Hosting/                     new — SharpMudApplicationBuilder/
                                         SharpMudApplication (no separate
                                         SharpMudOptions type), plus
                                         SessionLoop (service-class,
                                         parameter-count fixed)/
                                         PasswordHashing/HostOptions
                                         (trimmed to DbPath)/PlayerLogin/
                                         LoginFlow (the latter two decoupled
                                         from HubWorldBuilder.CreatePlayer,
                                         via a new IPlayerFactory) moved in
                                         from src/SharpMud.Host. Registers
                                         BuiltinCommands + IEnumerable<ITickable>
                                         automatically. Needs
                                         Microsoft.Extensions.Identity.Core
                                         (PasswordHashing) in addition to
                                         Microsoft.Extensions.Hosting.
  SharpMud.Persistence/
  SharpMud.Persistence.Sqlite/          new
  SharpMud.Persistence.DynamoDb/        new
  SharpMud.Adapters.Telnet/             gains a ProjectReference to
                                         SharpMud.Hosting + an
                                         AddSharpMudTelnetTransport(...)
                                         extension (absorbs HostRunner.cs)
  SharpMud.Adapters.Cli/                same shape, AddSharpMudCliTransport(...)

samples/                                (NOT packaged — reference implementation)
  SharpMud.Samples.Classic/             single project — merges
                                         SharpMud.Ruleset.Classic's content
                                         (moved from src/, its own code
                                         unchanged internally, but the
                                         csproj itself needs OutputType=Exe
                                         + the package/project references
                                         Program.cs needs — a plain library
                                         csproj isn't runnable as-is, see
                                         below) and only SharpMud.Host's
                                         Program.cs + appsettings.json
                                         (rewritten against
                                         SharpMud.Hosting) — not the rest
                                         of Host, see above
```

**The merged project's `.csproj` needs real changes, not just a `git mv`,
flagged in PR review.** `SharpMud.Ruleset.Classic.csproj` today is a plain
class library — no `OutputType`, no `Serilog`/config/adapter
`PackageReference`s or `ProjectReference`s, none of which `Program.cs`
needs until it moves in. Once merged: `OutputType` becomes `Exe`;
`ProjectReference`s to `SharpMud.Hosting` and whichever transport
package(s) the sample wants are added; whatever `Program.cs` still needs
directly (logging/config packages not already pulled in transitively via
`SharpMud.Hosting`) gets added too. Treating the merge as "just move the
files" would leave a project that has `Program.cs` in it but doesn't
build, let alone run.

This is a genuinely different shape from today's repo, not just a rename:
today `Ruleset.Classic` and `Host` are two separate `src/` projects (the
dependency-direction rule in effect since the original engine-vs-ruleset
split — `Host` references `Ruleset.Classic`, never the reverse). The whole
point of `SharpMud.Hosting` is that a consumer needs **exactly one project**
of their own — ruleset code and `Program.cs` living together, per this
ADR's Decision Drivers. A two-project sample would demonstrate the opposite
of what this ADR set out to prove, so `SharpMud.Ruleset.Classic` and
`Program.cs` — specifically `Program.cs`, not the whole old `SharpMud.Host`
project, per the split above — are consolidated into one
`samples/SharpMud.Samples.Classic/` project: the ruleset behaviors/commands
and the few lines of `Program.cs` using `SharpMud.Hosting`'s builder live
side by side in the same project, exactly as an external consumer's own
project would.

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
- [ADR-0007](0007-narrow-meta-package-scope.md) — narrows this ADR's
  meta-package package set (Engine + Hosting + Persistence only, not
  persistence providers or transport adapters); the rest of this ADR is
  unchanged
- `coding-standards.md`'s DI/composition section (corrected in this change)
- WheelMUD license: <https://github.com/DavidRieman/WheelMUD/blob/main/src/LICENSE.txt>
  (MS-PL — see License and naming above for why this doesn't constrain
  sharp-mud's own license choice)
