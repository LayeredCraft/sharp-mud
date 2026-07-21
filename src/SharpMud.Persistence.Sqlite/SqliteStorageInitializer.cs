using Microsoft.EntityFrameworkCore;
using SharpMud.Engine.Core;
using SharpMud.Persistence;

namespace SharpMud.Persistence.Sqlite;

// EnsureCreated only, never EnsureDeleted, at boot - creates the schema if
// missing but never wipes existing data. See docs/persistence.md Schema/
// Migrations: a genuinely changed C# model against an old .db file during
// dev means deleting the file by hand, not an automatic wipe every startup
// (which would defeat persistence entirely).
internal sealed class SqliteStorageInitializer : IStorageInitializer
{
    private readonly IDbContextFactory<GameDbContext> _contextFactory;

    public SqliteStorageInitializer(IDbContextFactory<GameDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(ct);
        await dbContext.Database.EnsureCreatedAsync(ct);
    }
}
