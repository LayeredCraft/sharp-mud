using Microsoft.Extensions.DependencyInjection;

namespace SharpMud.Adapters.Cli;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers a background service that runs a single local stdin/stdout session, login-free per SPEC.md.</summary>
    public static IServiceCollection AddSharpMudCliTransport(this IServiceCollection services)
    {
        services.AddHostedService<CliTransportBackgroundService>();
        return services;
    }
}
