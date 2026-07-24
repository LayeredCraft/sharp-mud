using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpMud.Engine.Core;
using SharpMud.Engine.Help;
using SharpMud.Persistence;

namespace SharpMud.Persistence.Sqlite;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ThingRepository"/> and <see cref="HelpRepository"/>
    /// (ADR-0010), both backed by SQLite at <paramref name="dbPath"/>, plus
    /// the default help-search stack (<see cref="StubEmbeddingProvider"/>/
    /// <see cref="CosineHelpSearchIndex"/>) - registered here, not left
    /// opt-in, since <c>help</c> (unlike <c>helptopic</c>/<c>helpindex</c>)
    /// is always part of <see cref="Engine.Commands.Builtin.BuiltinCommands.RegisterAll"/>,
    /// the same "always-available infrastructure" shape <see
    /// cref="IThingRepository"/> already has here.
    /// </summary>
    public static IServiceCollection AddSharpMudSqlitePersistence(this IServiceCollection services, string dbPath)
    {
        ArgumentNullException.ThrowIfNull(dbPath);

        services.AddDbContextFactory<GameDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<IThingRepository, ThingRepository>();
        services.AddSingleton<IHelpRepository, HelpRepository>();
        services.AddSingleton<IEmbeddingProvider, StubEmbeddingProvider>();
        services.AddSingleton<IHelpSearchIndex, CosineHelpSearchIndex>();
        services.AddSingleton<IStorageInitializer, SqliteStorageInitializer>();

        return services;
    }
}
