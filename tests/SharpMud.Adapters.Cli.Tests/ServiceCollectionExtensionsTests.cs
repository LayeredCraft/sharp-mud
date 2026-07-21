using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharpMud.Adapters.Cli.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSharpMudCliTransport_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddSharpMudCliTransport();

        services.Should().Contain(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(CliTransportBackgroundService));
    }
}
