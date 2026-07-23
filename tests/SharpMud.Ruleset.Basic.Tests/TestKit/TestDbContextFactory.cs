using Microsoft.EntityFrameworkCore;
using SharpMud.Persistence;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic.Tests.TestKit;

// A temp-file SQLite DB, not in-memory - matches
// SharpMud.Persistence.Tests.TestKit.TestDbContextFactory's reasoning.
public sealed class TestDbContextFactory : IDbContextFactory<GameDbContext>, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sharpmud-basic-test-{Guid.NewGuid()}.db");

    public GameDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var context = new GameDbContext(options, [new BasicBehaviorMappingContributor(), new RpgBehaviorMappingContributor()]);
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
