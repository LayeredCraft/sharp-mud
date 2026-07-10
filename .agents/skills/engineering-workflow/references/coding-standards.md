# Coding standards

These are the conventions **already in practice** in this codebase — hold
new code to them, don't invent stricter or looser rules.

**Naming**
- Interfaces are `I`-prefixed (`IThingRepository`, `ISession`).
- Async methods end in `Async` and return `Task`/`Task<T>`; `ValueTask` is
  reserved for the adapter/session I/O layer only (e.g. `ConsoleSession`).
- Private fields are `_camelCase`.
- Classes are `sealed` by default. Only leave a class unsealed if it's
  genuinely designed for extension (rare — most extensibility here happens
  through `Behavior` composition, not inheritance).
- Test classes: `sealed class <TypeUnderTest>Tests`. Test methods:
  `MethodName_ExpectedBehavior_WhenCondition`.

**Nullable reference types**
- `<Nullable>enable</Nullable>` is on for every csproj — keep it on for any
  new project.
- Prefer `required` init-only properties for mandatory state
  (`public required ThingId Id { get; init; }`) over constructor
  boilerplate or nullable-and-checked-later fields.
- Use `= null!` only when a framework (EF Core) must set the value
  post-construction and `required init` isn't achievable — and leave a
  comment explaining why, matching `ExitBehavior.Destination`.

**Async**
- Thread `CancellationToken` as an explicit last parameter through public
  async APIs — don't drop it partway through a call chain.
- Never use `ConfigureAwait(false)` — this is an application, not a
  redistributed library; the existing code correctly omits it everywhere.

**DI / composition**
- Register services inline in `Program.cs` via `ServiceCollection` — no
  `AddSharpMudX()` extension-method sprawl has been introduced, and no new
  registration pattern should be either without discussing it first, since
  it'd be the first of its kind in the repo.
- No static singletons, ever. If something feels like it wants to be a
  singleton, register it `AddSingleton` in DI instead.

**Error handling**
- Exceptions, not result types. No custom exception hierarchy exists —
  don't introduce one without a concrete need (e.g. a caller that must
  branch on failure *kind*, not just fail).
- `try/catch` belongs at I/O boundaries only (socket/stream code in
  `SharpMud.Adapters.Telnet`) plus a single top-level catch-all in
  `HostRunner` as the last-resort backstop. Don't add defensive try/catch
  inside engine/ruleset logic — let it throw and surface at the boundary.
- Use `throw new ArgumentOutOfRangeException(...)` for exhaustiveness
  guards on switches over enums, matching existing usage.

**File / namespace organization**
- Namespace matches folder path exactly, file-scoped namespace syntax.
- One public type per file, filename == type name.
- Organize by feature folder (`Core/`, `Behaviors/`, `Commands/`,
  `Sessions/`, `Configurations/`), not by technical layer.

**Known inconsistency** — there is no `Directory.Build.props`; `Nullable`,
`ImplicitUsings`, and `TargetFramework` are repeated per-csproj. If you add
a new project, copy the settings from an existing csproj rather than
introducing a third variant. Consolidating into `Directory.Build.props` is
open, uncommitted cleanup — do it as its own PR if you pick it up, not as a
drive-by in a feature change.
