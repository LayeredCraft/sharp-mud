using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpMud.Engine.Core;
using SharpMud.Persistence;

namespace SharpMud.Persistence.DynamoDb;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ThingRepository"/> backed by DynamoDB.
    /// <paramref name="configureClient"/> is optional - omit it to use the
    /// standard AWS credential chain and environment-configured region; see
    /// EntityFrameworkCore.DynamoDb's Client Setup docs for overriding the
    /// endpoint (e.g. DynamoDB Local for development).
    /// </summary>
    /// <remarks>
    /// No <c>IStorageInitializer</c> is registered here (this project has
    /// no reference to <c>SharpMud.Hosting</c>) - unlike SQLite, this
    /// provider does not create or migrate DynamoDB tables; the table(s)
    /// must already exist.
    /// </remarks>
    public static IServiceCollection AddSharpMudDynamoDbPersistence(this IServiceCollection services, Action<AmazonDynamoDBConfig>? configureClient = null)
    {
        services.AddDbContextFactory<GameDbContext>(options => options.UseDynamo(dynamoOptions =>
        {
            if (configureClient is not null)
                dynamoOptions.ConfigureDynamoDbClientConfig(configureClient);
        }));
        services.AddSingleton<IThingRepository, ThingRepository>();

        return services;
    }
}
