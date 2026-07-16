# Coding standards

These are the conventions **already in practice** in this codebase — hold
new code to them, don't invent stricter or looser rules.

**Access modifiers**
- Default to the most restrictive access modifier that satisfies the
  requirement — don't make something `public` because it's convenient
  right now, make it as narrow as the actual callers require.
- Within a project, types and members are `internal` by default. Only
  what forms that project's actual public contract — the surface another
  project is meant to call — should be `public`.
- To give a test project access to `internal` members, use
  `InternalsVisibleTo` (see `SharpMud.Engine.csproj`'s existing
  `<InternalsVisibleTo Include="SharpMud.Persistence" />` for the pattern,
  even though that one's project-to-project rather than test-to-project);
  don't widen a member to `public` just so a test can reach it.

  **Known inconsistency** — there are currently zero `internal`
  declarations anywhere in `src/`; everything is `public` today. This
  predates the rule above rather than following it — treat it the same way
  as the `Directory.Build.props` gap below: don't block unrelated work on
  auditing a project's whole public surface, but new code should default
  `internal` and only go `public` with a reason, and narrowing an existing
  member is fair game the next time you're already substantially touching
  the type it's on.

**Naming**

| Element | Convention | Example |
|---|---|---|
| Classes, records, structs, enums | `PascalCase` | `LockableBehavior`, `ThingId` |
| Interfaces | `I`-prefixed `PascalCase` | `IThingRepository`, `ISession` |
| Public properties and methods | `PascalCase` | `IsLocked`, `AddBehavior()` |
| Private fields | `_camelCase` | `_random` |
| Protected fields | `_camelCase` | `_world` |
| Local variables and method parameters | `camelCase` | `thingId`, `cancellationToken` |
| Generic type parameters | `T` prefix + `PascalCase` | `TBehavior`, `TResult` |
| Constants (`const`) | `PascalCase` | `MaxLoginAttempts` |
| Static readonly fields | `PascalCase` | `DefaultTickInterval` |
| Configuration/options classes | `PascalCase` + `Options` suffix | `HostOptions`, `GameLoopOptions` |
| Async methods | end in `Async`, return `Task`/`Task<T>` | `SaveAsync()` |
| Boolean properties/variables | positive assertion | `IsValid`, `HasErrors`, `CanSubmit` (not `NotValid`, `NoErrors`) |
| Abbreviations in names | treated as ordinary words, `PascalCase`/`camelCase` applies through them | `DmvService` (not `DMVService`), `xmlParser` (not `XMLParser`) |

A couple of these have a repo-specific wrinkle beyond the table:

- `ValueTask` is reserved for the adapter/session I/O layer only (e.g.
  `ConsoleSession`) — everywhere else, async methods return `Task`/`Task<T>`
  even though the naming convention (`...Async`) is the same either way.
- Classes are `sealed` by default — it's not just a style preference, sealed
  types avoid virtual-dispatch overhead and let the JIT devirtualize calls.
  Only leave a class unsealed if it's genuinely designed for extension (rare
  — most extensibility here happens through `Behavior` composition, not
  inheritance).
- Test classes: `sealed class <TypeUnderTest>Tests`. Test methods:
  `MethodName_ExpectedBehavior_WhenCondition`.

**Type inference and instantiation**

- Prefer `var` when the type is obvious from the right-hand side:
  ```csharp
  var session = new TelnetSession(client);   // type is right there
  var behavior = thing.GetBehavior<LockableBehavior>();   // method name says it
  ```
  Use an explicit type when the right-hand side doesn't make it obvious
  (e.g. a method call whose return type isn't clear from its name).
- Prefer target-typed `new()` when the left-hand side already states the
  type — don't repeat it:
  ```csharp
  ThingId id = new(Guid.NewGuid());
  private readonly List<string> _names = new();
  ```

**`IOptions<T>` configuration classes**

If a configuration class is bound via `IOptions<T>` (as opposed to the
`HostOptions.Parse`-style manual env-var parsing already in `SharpMud.Host`
— see `security.md` for why that one stays env-var-only), it must:

- Use the `Options` suffix, per the naming table above.
- Declare a `public const string SectionName` whose value matches the class
  name with the suffix stripped (e.g. `WorldGenOptions.SectionName = "WorldGen"`).
  This keeps the binding self-contained and refactor-safe — renaming the
  class doesn't silently orphan a hardcoded section-string elsewhere.
- Be registered with the Configuration Binder pattern:
  `services.Configure<TOptions>(configuration.GetSection(TOptions.SectionName))`,
  not a hand-rolled `GetSection(...).Bind(opt)` scattered at the call site.

**Nullable reference types**
- `<Nullable>enable</Nullable>` is on for every csproj — keep it on for any
  new project.
- Prefer `required` init-only properties for mandatory state
  (`public required ThingId Id { get; init; }`) over constructor
  boilerplate or nullable-and-checked-later fields.
- Use `= null!` only when a framework (EF Core) must set the value
  post-construction and `required init` isn't achievable — and leave a
  comment explaining why, matching `ExitBehavior.Destination`. Never use
  the null-forgiving operator anywhere else without an explanatory comment;
  an uncommented `!` just hides the question of who's actually guaranteeing
  the value is non-null.
- Guard arguments at method entry with `ArgumentNullException.ThrowIfNull(param)`,
  not `throw new ArgumentNullException(nameof(param))` — same effect, less
  boilerplate:
  ```csharp
  public void AttachTo(Thing owner)
  {
      ArgumentNullException.ThrowIfNull(owner);
      // ...
  }
  ```
- Use guard clauses / early returns at the top of a method rather than
  nesting the real logic inside a conditional — flatten the happy path
  instead of indenting it.

**Async**
- Thread `CancellationToken` as an explicit last parameter through public
  async APIs — don't drop it partway through a call chain.
- Never use `ConfigureAwait(false)` — this is an application, not a
  redistributed library; the existing code correctly omits it everywhere.
- Always `await` — never block on async code with `.Result`, `.Wait()`, or
  `.GetAwaiter().GetResult()`. Blocking on async code from a sync context is
  a deadlock risk and defeats the point of the async chain you just wrote.
- No `async void`, ever, including event handlers — exceptions thrown from
  `async void` can't be caught by the caller, they just crash the process.
  Use `async Task` (or a `Task`-returning handler if the framework allows
  it); if a genuinely fire-and-forget signature is unavoidable, that's a
  design question worth raising, not a default to reach for.
- Don't use `Task.Run()` to push work onto the thread pool as a substitute
  for a real async API or to "make something async" — this repo's
  I/O-bound work (sockets, EF Core) is naturally async already, and
  `Task.Run` around CPU-bound engine/ruleset logic just adds a thread-pool
  hop without a concrete need for it.
- Prefer `IAsyncEnumerable<T>` for streaming data sources instead of
  buffering everything into a `List<T>` first — `TelnetListener
  .AcceptSessionsAsync` already does this for incoming connections. Reach
  for it whenever a caller can start processing items as they arrive rather
  than waiting for the whole set to materialize.

**DI / composition**
- Register services inline in `Program.cs` via `ServiceCollection` — no
  `AddSharpMudX()` extension-method sprawl has been introduced, and no new
  registration pattern should be either without discussing it first, since
  it'd be the first of its kind in the repo.
- No static singletons, ever. If something feels like it wants to be a
  singleton, register it `AddSingleton` in DI instead.
- All dependencies are injected via the constructor into `private readonly`
  fields (`protected readonly` if a base class exposes the dependency to
  subclasses). Don't inject via public settable properties or method
  parameters — the one exception is where a framework requires it (e.g.
  `[FromServices]` in a minimal-API handler).
- Don't reach for `IServiceProvider` / the service-locator pattern to pull a
  dependency out of thin air. The only legitimate uses are the composition
  root (`Program.cs`) itself and a factory class whose *entire, explicit*
  purpose is resolving something at runtime (e.g. resolving a per-request
  or per-connection instance) — if you're tempted to inject
  `IServiceProvider` anywhere else, that's a sign the real dependency should
  just be constructor-injected instead.
- **Constructor style**: use traditional constructors with explicit
  `private readonly` field assignments in application code, not primary
  constructors:
  ```csharp
  public sealed class WanderManager : ITickable
  {
      private readonly IWorld _world;
      private readonly IRandomSource _random;

      public WanderManager(IWorld world, IRandomSource random)
      {
          _world = world;
          _random = random;
      }
  }
  ```
  Primary constructors are reserved for test classes (fixtures, test data
  builders), where the extra brevity doesn't cost you a place to put
  validation or additional setup logic later.

  **Known inconsistency** — most existing classes (`TelnetSession`,
  `TelnetListener`, `ThingEvents`, `BehaviorManager`, `GameLoop`,
  `WanderManager`, `HelpCommand`, `MoveCommand`, `ThingRepository`,
  `AttackCommand`, and others) currently use primary constructors in
  application code, predating this rule. Match the traditional-constructor
  style for **new** classes; don't block on migrating existing ones, and
  don't do a drive-by rewrite of a class you're touching for an unrelated
  reason — migrate a class's constructor style only when you're already
  substantially changing it, the same way the `Directory.Build.props` gap
  below is handled.

**Immutability and object modeling**
- Default to immutable: properties are `init`-only unless the type has a
  concrete reason to mutate after construction.
- DTOs, commands, queries, and value objects **must** be immutable — model
  them as `record` (reference-type data with structural equality) or
  `readonly record struct` (small value objects with value semantics):
  ```csharp
  public sealed record MoveCommandRequest(ThingId ActorId, Direction Direction);
  public readonly record struct Coordinates(int X, int Y);
  ```
- Domain entities and aggregate roots are regular `class`es, not records —
  they have identity, not structural equality, and any state change goes
  through a domain method (`thing.AddBehavior(...)`, `lockable.Unlock(key)`),
  never a public setter. `Behavior` subtypes follow this: they expose
  mutable state through methods/events, not `{ get; set; }`.
- **Required properties vs. constructor parameters**: use `required`
  properties for DTOs and configuration objects, where the shape is "all of
  these must be set, in whatever order" (`HostOptions`-style records already
  do this via positional records, which is equivalent). Use constructor
  parameters for domain entities and aggregate roots, where construction is
  itself a domain operation, not just data assembly.

**Expression-bodied members and pattern matching**
- Prefer expression-bodied members for simple, single-line implementations:
  `public bool IsLocked => _state == LockState.Locked;` reads better than
  the equivalent four-line property.
- Prefer pattern matching wherever it makes the code more readable — that's
  the bar, not "use pattern matching everywhere":
  - Prefer switch *expressions* over switch *statements* when every branch
    just produces a value.
  - Use `is` pattern matching for null checks and type checks (`if (thing
    is { } t)`, `if (behavior is LockableBehavior locked)`) instead of
    `!= null` plus a separate cast.

**LINQ**
- Method/fluent syntax exclusively — `things.Where(t => ...).Select(...)`,
  never query syntax (`from x in things where ... select ...`). No query
  syntax exists in this codebase today; keep it that way.
- Keep chains readable: break a chain across multiple lines, one method
  per line, once it's more than a method or two long. `ObjectMatcher`'s
  `Where(...)` / `FirstOrDefault()` split and `WanderManager`'s
  multi-line `.Select(...)` chain are the existing model to follow — a
  short chain (`_behaviors.OfType<T>().FirstOrDefault()`) is fine to keep
  on one line.

**Methods**
- Four parameters, max (not counting a trailing `CancellationToken`, which
  doesn't count against the limit). A method that needs a 5th parameter
  takes a parameter object instead — a `record`, per **Immutability and
  object modeling** above, not a loose 5th argument bolted on.
- No boolean flag parameters that change what the method *does*. A `bool`
  that makes a method take two different code paths internally
  (`Save(user, sendEmail: true)` doing one thing when `true` and a
  different thing when `false`) is a sign the method is actually two
  methods wearing one name — split it into two well-named ones instead.
  This doesn't apply to a `bool` that's genuinely just the data being set —
  `SetEchoAsync(bool enabled, ...)` isn't a flag branching behavior, it's a
  setter whose value happens to be a boolean; that's fine as-is.

**Collection types on API surfaces**
- Default to `IEnumerable<T>` for method parameters and return types when
  the caller only needs to iterate — it keeps the implementation flexible
  (a `List<T>`, a LINQ query, a database cursor can all satisfy it) and
  defers execution instead of forcing materialization the caller may not
  need.
- Use `IReadOnlyList<T>` or `IReadOnlyCollection<T>` when the caller needs
  indexed access or `Count` without being able to mutate — this is already
  the dominant pattern here (`Thing.Children`, `BehaviorManager.All`,
  `CommandRegistry.Commands`, every command's `Aliases` property all
  return `IReadOnlyList<T>`).
- Use `IList<T>`/`ICollection<T>` only when callers genuinely need to
  mutate the collection through that reference — don't reach for a mutable
  interface just because it's more permissive.
- Never expose a raw `List<T>`, `Dictionary<TKey, TValue>`, or other
  concrete collection type on a `public` or `internal` API surface — always
  the appropriate interface from the tiers above.
- Inside domain entities, back the collection with a `private List<T>` (or
  `Dictionary<TKey, TValue>`, etc.) field and expose it as
  `IReadOnlyCollection<T>`/`IReadOnlyList<T>` — mutation happens through a
  named domain method (`AddChild`, `Equip`), not by handing the caller the
  backing collection to mutate directly.

  **Known inconsistency** — two existing members violate this today:
  `EquippedBehavior.Equipped` is a `public Dictionary<EquipSlot, Thing?>`
  that `WearCommand`/`RemoveCommand` mutate directly via indexer
  (`equipped.Equipped[slot] = item`), and `PlayerBehavior.Aliases` is a
  `public List<string>`. Both predate this rule. Don't block unrelated work
  on fixing them, but don't add a third raw-collection property either —
  and if you're already touching either class for another reason, wrapping
  it behind `IReadOnlyDictionary`/`IReadOnlyList` plus a domain method
  (`Equip(slot, item)`, `Unequip(slot)`) is a welcome opportunistic fix,
  same treatment as the other known-inconsistency notes in this doc.

**Collections, strings, time, and concurrency**
- Prefer collection expressions (C# 12) over `new`-based initialization:
  ```csharp
  int[] values = [1, 2, 3];
  List<string> names = [];
  IReadOnlyList<string> aliases = [.. baseAliases, "n"];
  ```
- Prefer string interpolation (`$"..."`) for one-off string construction;
  reach for `StringBuilder` only when concatenating in a loop or other hot
  path. Never use `string.Format` — interpolation covers the same cases
  more readably. Use raw string literals (`"""..."""`) for embedded
  multi-line text (protocol snippets, test fixtures) instead of
  escaped `\n`/`\"` soup.
- Anything whose behavior depends on "now" (tick timestamps, a future
  login-lockout window, session timeouts) takes a `TimeProvider` via
  constructor injection instead of calling `DateTime.UtcNow`/
  `DateTimeOffset.UtcNow` directly — that's what makes it deterministically
  testable (fake/advance time in a test, `TimeProvider.System` in the real
  DI registration). Code that doesn't have time-dependent *behavior* to test
  (a one-off log timestamp, say) doesn't need the abstraction.
- For shared mutable state, default to lock-free options first —
  `ConcurrentDictionary`, `Interlocked`, or an immutable snapshot swap —
  before reaching for an explicit lock. When a real critical section is
  unavoidable, use `System.Threading.Lock` (.NET 9), not `lock (someObject)`
  on an arbitrary reference type. Note the engine's tick loop is already
  single-threaded/sequential by design (see `persistence.md`/`GameLoop`) —
  this guidance is for the parts of the codebase that aren't, like a
  connection-keyed tracker in `SharpMud.Host`.

**Error handling**

Core principle: **exceptions are for the unexpected.** An exception signals
a bug or an environmental failure — something that should never happen
during normal operation (a null that violates an invariant, a socket
dropping, a switch hitting a value that shouldn't exist). An *expected*
outcome — a business rule not being satisfied, an entity that doesn't
exist, structurally invalid input — is not a bug, and must be communicated
through a return value, not an exception. Using exceptions for control flow
hides intent: the method signature no longer tells the caller what can go
wrong, callers have to guess (or read the implementation) to find out, and
it bypasses the type system doing the job it's good at.

This is actually closer to what's already in this codebase than it might
look — `LoginFlow`/`ThingRepository` already return `Thing?`/nullable for
"not found" rather than throwing, and there's no `throw new` anywhere in
`src/` outside of exhaustiveness guards. The rules below make that existing
instinct explicit and give it a name, rather than changing direction:

- Never throw for an expected failure. Business outcomes — "this move is
  invalid," "no player with that username," "the command couldn't parse" —
  are represented as a result the caller checks, not an exception the
  caller catches.
- `Result<T>` (or equivalent) is a **domain/business-logic** concept, not
  an infrastructure one. Infrastructure code (repositories, adapters, I/O)
  uses **nullable** as its "not found" contract — `Thing?
  FindPlayerByUsernameAsync(...)` is the pattern, not
  `Result<Thing> FindPlayerByUsernameAsync(...)`. Don't wrap infrastructure
  lookups in `Result<T>` on top of the nullable contract that's already
  there.
- Catch the base `Exception` type only in a top-level boundary handler
  (matching `HostRunner`'s single catch-all today). Everywhere else, catch
  specific exception types you actually know how to handle (matching the
  typed `IOException`/`SocketException`/`OperationCanceledException`
  catches already in `SharpMud.Adapters.Telnet`/`SessionLoop`) — a broad
  `catch (Exception)` outside the top-level boundary hides bugs instead of
  handling them.
- When re-throwing, always use bare `throw;` — never `throw ex;`, which
  resets the stack trace and erases where the exception actually happened.
- Never swallow an exception silently. At an absolute minimum, log it and
  re-throw; if you're catching it, you owe the next person a record of what
  happened.
- Log an exception once, at the boundary where it's actually handled — not
  at every layer it passes through on the way there. A caught-logged-rethrown
  exception logged again three layers up just duplicates the same incident
  in the logs with no new information.
- Use `throw new ArgumentOutOfRangeException(...)` for exhaustiveness
  guards on switches over enums, matching existing usage — this is exactly
  the "should never happen" case exceptions are for, and it applies whether
  the switch is a statement or an expression (a `_ => throw new
  ArgumentOutOfRangeException(...)` discard arm on a switch expression).

**Adopting a `Result<T>` type is itself a design decision, not a drive-by
choice** — there's no `Result<T>` (FluentResults or hand-rolled) in this
codebase yet, so introducing one is the first instance of a new pattern.
Per `design-decisions.md`, that gets a light design dive and a decision
record (in `docs/architecture.md` or wherever ends up owning it) the first
time a real business-outcome case needs it — don't reach for a NuGet
package or roll a custom type inline the first time you hit this. FluentResults
is a reasonable default to evaluate then, but roll-your-own is equally on
the table; the point is deciding once, deliberately, not per-PR.

**Logging**

Serilog is the logging provider, consumed through the standard
`Microsoft.Extensions.Logging` abstraction — `ILogger<T>` constructor
injected into a `private readonly` field, same as any other dependency (see
**DI / composition** above). At call sites, use the
[`LayeredCraft.StructuredLogging`](https://github.com/LayeredCraft/structured-logging)
extension methods instead of raw `ILogger` calls — they drop the `Log`
prefix and keep every call structured:

```csharp
private readonly ILogger<LoginFlow> _logger;

_logger.Information("Player {Username} logged in", username);
_logger.Warning("Login attempt {AttemptNumber} failed for {Username}", attempt, username);
_logger.Error(ex, "Failed to save {ThingId}", thing.Id);
```

- **Always use the message-template form** — named placeholders
  (`{Username}`, `{ThingId}`) plus the values as separate arguments, never a
  pre-interpolated `$"..."` string as the message. This is the one place
  the "prefer string interpolation" rule above doesn't apply: interpolating
  the values into the string yourself throws away the structured
  properties Serilog would otherwise capture, which is the entire point of
  structured logging.
- Pass the exception as the first argument to `Error`/`Critical`
  (`logger.Error(ex, "...")`), not folded into the message text — this is
  the mechanism for the "log once, at the boundary where it's handled" rule
  in **Error handling** above.
- Scopes (`BeginScope`, `BeginScopeWith`) and performance helpers
  (`TimeOperation`, `TimeAsync`) are available from the same package for
  request/connection-scoped context or timing — reach for them when you
  actually need that context, not as a default wrapper around every method.
- Configure Serilog in code in `Program.cs` (the composition root), not
  through an `appsettings.json` `Logging` section — this repo has no
  `appsettings.json` (see `security.md`), and that doesn't change just
  because Serilog's own docs default to it.
- In tests, use the package's `TestLogger` (implements `ILogger`, captures
  entries in memory, has `AssertLogEntry`/`AssertLogCount`/etc.) instead of
  a hand-rolled fake or `NSubstitute.For<ILogger<T>>()` — see
  `testing.md` for the rest of this repo's test-double conventions.

**Code structure and organization**
- No God classes. If a class has grown enough unrelated responsibility that
  it's hard to describe in one sentence, that's the signal to split it —
  the `Behavior` composition model and one-manager-per-concern pattern
  (`WanderManager`, `CombatManager`) already exist specifically to avoid
  this, so reaching for a God class usually means fighting the grain of
  the existing design rather than following it.
- No `#region` blocks, anywhere (none exist in this codebase today — keep
  it that way). If a class feels like it needs regions to stay navigable,
  the class is too large; split it instead of organizing the sprawl.
- No static classes except true stateless utility helpers (matching what's
  already here: `ObjectMatcher`, `CommandGuards`, `PasswordHashing`, etc. —
  all pure functions, no state). Static **mutable** state is never
  permitted, full stop — that's just a singleton wearing a different hat,
  and it's covered by the same "no static singletons, ever" rule as
  **DI / composition** above.
- Extension methods use C# 14 extension block syntax, not the legacy
  `static class` + `this`-parameter pattern:
  ```csharp
  public static class DirectionExtensions
  {
      extension(Direction direction)
      {
          public string ToDisplayString() => direction switch { ... };
      }
  }
  ```
  Call sites don't change (`direction.ToDisplayString()` either way) — this
  is purely about how the extension is declared.

  A few more rules on extension classes specifically:
  - Name the class after the extended type plus `Extensions`
    (`DirectionExtensions` for `Direction`) — don't fold unrelated
    extensions for different types into one shared "Extensions" grab-bag
    class.
  - One extended type per file, one static class per file, filename
    matches the class name — `DirectionExtensions` lives in
    `DirectionExtensions.cs`, not folded into `Direction.cs` alongside the
    `enum` it extends. This is the same "one public type per file" rule
    from **File / namespace organization** below, applied to extension
    classes specifically.
  - The extension class follows the same access-modifier rules as any
    other type — `internal` if it's not meant to cross an assembly
    boundary, `public` if it is. There's nothing special about extension
    classes that forces them public.
  - When extending a type from a third-party or BCL assembly (not a type
    this repo owns), the extension class lives in the project that owns
    the *dependency*, not wherever the first caller happens to be — e.g. an
    extension on an EF Core type belongs in `SharpMud.Persistence`, not in
    whichever project first needed it.
- Don't qualify instance member access with `this.` unless it's required
  to resolve a naming ambiguity (e.g. a constructor parameter shadowing a
  field of the same name) — the existing code already doesn't do this
  anywhere, keep it that way.

**Member ordering within a class**

Order members top-to-bottom by access modifier, and within each access
level by kind:

1. Public constants and static members
2. Public properties
3. Public constructors
4. Public methods
5. Internal members (same sub-order as above: constants/static, properties,
   constructors, methods)
6. Protected members (same sub-order)
7. Private members (same sub-order)

Separate each group with a single blank line. The point is that anyone
reading the class top-down sees its public contract first and its
implementation detail last, in a consistent place every time — not
alphabetical order, not chronological-by-when-it-was-added.

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
