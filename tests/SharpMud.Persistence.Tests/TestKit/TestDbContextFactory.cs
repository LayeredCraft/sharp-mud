using Microsoft.EntityFrameworkCore;
using SharpMud.Samples.Classic;

namespace SharpMud.Persistence.Tests.TestKit;

// A temp-file SQLite DB, not in-memory - ThingRepository intentionally
// creates a fresh DbContext (and thus a fresh connection) per call, and
// SQLite in-memory DBs don't persist across separate connections without
// shared-cache mode. A real file matches production behavior anyway.
public sealed class TestDbContextFactory : IDbContextFactory<GameDbContext>, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sharpmud-test-{Guid.NewGuid()}.db");

    public GameDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var context = new GameDbContext(options, [new ClassicBehaviorMappingContributor()]);
        context.Database.EnsureCreated();
        return context;
    }

    public Task<GameDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
        Task.FromResult(CreateDbContext());

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
