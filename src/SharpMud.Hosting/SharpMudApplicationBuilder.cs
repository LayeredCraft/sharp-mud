using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharpMud.Hosting;

/// <summary>
/// A thin facade over <see cref="HostApplicationBuilder"/> - the same
/// relationship <c>WebApplicationBuilder</c> has to its inner builder, not
/// a reimplementation of hosting. See
/// docs/adr/0006-nuget-package-distribution.md for why sharp-mud's
/// persistent-process/<see cref="Microsoft.Extensions.Hosting.BackgroundService"/>
/// execution model doesn't need anything more than that.
/// </summary>
public sealed class SharpMudApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _hostBuilder;

    internal SharpMudApplicationBuilder(string[]? args)
    {
        _hostBuilder = Host.CreateApplicationBuilder(args);
        _hostBuilder.Services.AddSharpMudHostingCore();
    }

    /// <inheritdoc />
    public IDictionary<object, object> Properties => ((IHostApplicationBuilder)_hostBuilder).Properties;

    /// <inheritdoc />
    public IConfigurationManager Configuration => _hostBuilder.Configuration;

    /// <inheritdoc />
    public IHostEnvironment Environment => _hostBuilder.Environment;

    /// <inheritdoc />
    public ILoggingBuilder Logging => _hostBuilder.Logging;

    /// <inheritdoc />
    public IMetricsBuilder Metrics => _hostBuilder.Metrics;

    /// <inheritdoc />
    public IServiceCollection Services => _hostBuilder.Services;

    /// <inheritdoc />
    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull =>
        _hostBuilder.ConfigureContainer(factory, configure);

    /// <summary>Builds the configured services into a <see cref="SharpMudApplication"/>, ready to run.</summary>
    public SharpMudApplication Build() => new(_hostBuilder.Build());
}
