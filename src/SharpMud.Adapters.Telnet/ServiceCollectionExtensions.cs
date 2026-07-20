using Microsoft.Extensions.DependencyInjection;

namespace SharpMud.Adapters.Telnet;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers a background service that accepts Telnet connections on <paramref name="port"/> and runs each through <see cref="SharpMud.Hosting.LoginFlow"/>/<see cref="SharpMud.Hosting.SessionLoop"/>.</summary>
    public static IServiceCollection AddSharpMudTelnetTransport(this IServiceCollection services, int port)
    {
        services.AddSingleton(new TelnetTransportOptions(port));
        services.AddHostedService<TelnetTransportBackgroundService>();
        return services;
    }
}
