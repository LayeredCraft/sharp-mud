using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpMud.Engine.Core;
using SharpMud.Hosting;
using SharpMud.Persistence;

namespace SharpMud.Persistence.Sqlite;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ThingRepository"/> backed by SQLite at <paramref name="dbPath"/>.</summary>
    public static IServiceCollection AddSharpMudSqlitePersistence(this IServiceCollection services, string dbPath)
    {
        ArgumentNullException.ThrowIfNull(dbPath);

        services.AddDbContextFactory<GameDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<IThingRepository, ThingRepository>();
        services.AddSingleton<IStorageInitializer, SqliteStorageInitializer>();

        return services;
    }
}
