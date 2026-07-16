# Persistence / database conventions

- EF Core against SQLite in dev, targeting a custom DynamoDB EF Core
  provider for production (see `docs/persistence.md` — don't assume
  relational-only just because EF Core is in play).
- One `IEntityTypeConfiguration<T>` class per `Behavior` subtype under
  `src/SharpMud.Persistence/Configurations/`.
- `Behavior` subtypes persist via TPH (table-per-hierarchy) with a
  `BehaviorType` discriminator — new Behaviors need a config class, not a
  new table.
- Graph edges EF can't map directly (owning-`Thing` back-reference,
  self-referencing parent, cross-Thing exit destination) are **shadow FK
  columns**, resolved manually post-load — follow the pattern in
  `ExitBehaviorConfiguration`/`ThingConfiguration`, don't add a normal
  navigation property for these.
- **No migrations.** Schema is created via `EnsureCreatedAsync()` at boot.
  Never call `EnsureDeleted` — a comment in `Program.cs` calls this out
  explicitly because data loss on redeploy is the failure mode it guards
  against. If the project ever needs real migrations (e.g. moving off
  SQLite), that's a deliberate decision to record in `docs/persistence.md`
  first, not something to slip in incidentally.
- No raw SQL, anywhere. Everything goes through EF Core/LINQ.

**Repositories**

- **No generic repositories** — no `IRepository<T>`/`Repository<T>` with
  `GetById`/`Add`/`Update`/`Delete` methods parameterized over an entity
  type. A generic repository just re-implements a subset of
  `DbSet<T>`/LINQ behind an extra interface, without adding any actual
  domain meaning — it hides *what operation is happening* behind a
  one-size-fits-all shape, so callers (and future readers) can't tell
  what's actually allowed to happen to that data from the method signature
  alone. It also invites exposing arbitrary querying (`Where`,
  `OrderBy`, ...) generically, which is exactly what "query services return
  DTOs, don't expose `IQueryable<T>`" below is trying to prevent.
- A repository is only justified for a **DDD aggregate root** — a type
  that owns a consistency boundary and whose sub-objects have no
  independent existence outside it. `Thing` is that aggregate root here
  (a `Thing` and its attached `Behavior`s save/load together as one unit);
  `Behavior` subtypes don't get their own repositories, because they're
  not independently addressable outside their owning `Thing` — this is
  why there's exactly one repository in this codebase, `IThingRepository`,
  not one per `Behavior` type or one per database table.
- Repositories are aggregate-specific and named after the aggregate root
  (`IThingRepository`/`ThingRepository` for `Thing`), not a generic
  `IDataAccess` or `IRepository<Thing>`.
- Repository methods express domain intent, not CRUD verbs —
  `LoadTreeAsync`, `SaveTreeAsync`, `FindPlayerByUsernameAsync` say what's
  actually happening; a repository method named `Get`/`GetAll`/`Update` is
  a sign the method is really just wrapping a raw query or an unconditional
  overwrite, not expressing a real operation on the aggregate.
- Mutate state through aggregate methods, never direct property
  assignment from outside the aggregate — `Thing.AttachLoadedChild(...)`
  and `Behaviors.Add(...)` (which fires `OnAddBehavior`) are the model,
  matching what `ThingRepository` already does during rehydration. The
  shadow-FK writes in `SaveTreeAsync`
  (`context.Entry(thing).Property("ParentId").CurrentValue = ...`) aren't
  an exception to this — those are EF Core persistence plumbing for
  columns that don't exist as real properties on the domain type at all,
  not a bypass of a real one.
- Commit once per command via a unit of work — one logical operation,
  one `SaveChangesAsync`.

  **Known inconsistency** — `ThingRepository.SaveTreeAsync` currently
  calls `SaveChangesAsync` twice (once after removing the old rows, once
  after re-inserting the subtree), to avoid a primary-key conflict within
  a single change-tracking batch. This works, but it's two commits for
  one logical "save the tree" operation, not one. Revisiting this (e.g.
  restructuring the delete/insert into a single `SaveChangesAsync`, or
  deciding two commits is the accepted shape for this specific
  delete-and-reinsert pattern and documenting why) is open, tracked work —
  don't fix it as a drive-by, but don't copy this shape into a new
  repository method without checking whether it's actually unavoidable
  there too.
- Use `AsNoTracking()` for read-only projections — a query whose result
  is never going to be mutated and saved back doesn't need EF's change
  tracker paying attention to it. This isn't in active use yet (there's no
  `AsNoTracking()` anywhere in `SharpMud.Persistence` today), largely
  because `ThingRepository` currently uses a fresh, short-lived
  `DbContext` per call that's disposed right after — the cost this rule
  guards against barely shows up yet. It matters starting with the first
  genuinely read-only projection query (see **query services** below).

**Query services**

There is currently no separate query-service/read-model layer in this
codebase — commands read game state directly off the in-memory `World`/
`Thing` graph (already fully loaded per `docs/persistence.md`), not via a
per-view database query. If/when a real query service is introduced (a
read path that goes back to the database rather than the in-memory graph
— for an admin/reporting view, say), it follows these rules:

- Query services return DTOs or read models, never the aggregate entity
  itself (`Thing`) — a query service handing back a live `Thing` blurs the
  line between "read this for display" and "load this aggregate to
  mutate it," which is exactly the distinction `IThingRepository` vs. a
  query service is meant to draw.
- Never expose `IQueryable<T>` outside `SharpMud.Persistence` — build and
  execute the query inside infrastructure, return a materialized DTO/read
  model. An `IQueryable<T>` leaking out lets a caller outside
  infrastructure bolt on arbitrary `Where`/`OrderBy`/`Include` calls,
  which defeats the point of having an infrastructure boundary at all.

Introducing this layer for the first time is itself a design decision —
see `design-decisions.md` — not something to back into incidentally while
adding one query.

**EF Core configuration**

All EF Core mapping and configuration lives in `SharpMud.Persistence`
(infrastructure) via `IEntityTypeConfiguration<T>` — this is already fully
established by the one-config-class-per-`Behavior` rule above; there's no
separate rule needed here beyond making explicit that mapping concerns
never leak into `SharpMud.Engine`/`SharpMud.Ruleset.Classic`.
