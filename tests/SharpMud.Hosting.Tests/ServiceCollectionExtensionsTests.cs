using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpMud.Hosting;

namespace SharpMud.Hosting.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSharpMudHostingCore_RegistersGameLoopAfterShutdownSave_SoGameLoopStopsFirst()
    {
        // The generic host stops IHostedServices in reverse registration
        // order - GameLoopHostedService must be registered after
        // ShutdownSaveHostedService so its tick loop fully quiesces before
        // the shutdown snapshot is taken, not concurrently with it.
        var services = new ServiceCollection();

        services.AddSharpMudHostingCore();

        var hostedServiceOrder = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .ToList();

        var shutdownSaveIndex = hostedServiceOrder.IndexOf(typeof(ShutdownSaveHostedService));
        var gameLoopIndex = hostedServiceOrder.IndexOf(typeof(GameLoopHostedService));

        shutdownSaveIndex.Should().BeGreaterThanOrEqualTo(0);
        gameLoopIndex.Should().BeGreaterThan(shutdownSaveIndex);
    }
}
