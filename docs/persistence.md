# Persistence

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [character.md](character.md) and
[world-model.md](world-model.md) for the entities being persisted.

## Strategy

Repository interfaces live in `SharpMud.Engine`; implementations live in
`SharpMud.Persistence` (see [architecture.md](architecture.md) for the project
split and dependency direction). Game logic depends only on the interfaces —
never on EF Core, a specific provider, or SQL directly.

```csharp
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(AccountId id, CancellationToken ct);
    Task<Account?> GetByExternalAuthIdAsync(string provider, string externalId, CancellationToken ct);
    Task SaveAsync(Account account, CancellationToken ct);
}

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(PlayerId id, CancellationToken ct);
    Task<IReadOnlyList<Player>> GetByAccountIdAsync(AccountId accountId, CancellationToken ct);
    Task SaveAsync(Player player, CancellationToken ct);
}

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(RoomId id, CancellationToken ct);
    Task<IReadOnlyList<Room>> GetAreaAsync(AreaId areaId, CancellationToken ct);
    Task SaveAsync(Room room, CancellationToken ct);
    Task SaveManyAsync(IEnumerable<Room> rooms, CancellationToken ct); // batched write for a frontier generation "chunk"
}
```

`IAccountRepository.GetByExternalAuthIdAsync` backs the OAuth login flow;
`IPlayerRepository.GetByAccountIdAsync` backs character-select — see
[accounts-auth.md](accounts-auth.md) for both.

## Provider Plan

- **v1: SQLite via EF Core** — zero infra, single file, transactional, easy
  local dev and debugging.
- **Planned**: MongoDB (existing EF Core provider) and a **custom DynamoDB EF
  Core provider** (in active co-development with Jonas Ha) — the intended
  production backend once ready, pairing with an AWS deployment (see
  [architecture.md](architecture.md) / SPEC.md deployment section).
- Switching providers is a connection-string/DI-registration change in `Host`
  — repository interfaces and all Engine code are unaffected.

## Write Frequency

v1 default: persist immediately on state-changing actions (e.g. every room
move — see the movement walkthrough in [commands.md](commands.md)) rather
than batching. Simpler to reason about and guarantees no lost progress on
crash/disconnect; revisit if write volume becomes a real concern once a
provider with per-write cost (e.g. DynamoDB) is in use.

## Schema / Migrations

Drop-and-recreate during early dev: no migration history tracked yet while
the domain model is still churning — `EnsureDeleted`+`EnsureCreated` (or
equivalent) on schema change, zero migration-file maintenance burden.
Switches to real EF Core Migrations once there's actual player data worth
preserving across schema changes (i.e. once the game is running with real
players, not just local dev).

## Open Items

- Batched/periodic-save vs. save-on-every-change — flagged above, no decision
  yet on when (if ever) to switch.
- Exact trigger point for switching from drop-and-recreate to tracked
  migrations (soft launch? first external playtester? not yet defined).
