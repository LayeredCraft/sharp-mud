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
