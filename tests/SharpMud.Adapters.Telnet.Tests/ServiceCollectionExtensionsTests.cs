using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharpMud.Adapters.Telnet.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSharpMudTelnetTransport_RegistersHostedServiceAndOptions()
    {
        var services = new ServiceCollection();

        services.AddSharpMudTelnetTransport(4001);

        services.Should().Contain(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TelnetTransportBackgroundService));
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TelnetTransportOptions>().Port.Should().Be(4001);
    }
}
