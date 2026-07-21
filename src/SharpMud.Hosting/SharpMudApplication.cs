using Microsoft.Extensions.Hosting;

namespace SharpMud.Hosting;

/// <summary>
/// A running (or ready-to-run) sharp-mud host - wraps the built
/// <see cref="IHost"/> directly, the same relationship <c>WebApplication</c>
/// has to its inner host. See
/// docs/adr/0006-nuget-package-distribution.md.
/// </summary>
public sealed class SharpMudApplication : IHost, IAsyncDisposable
{
    private readonly IHost _host;

    internal SharpMudApplication(IHost host)
    {
        _host = host;
    }

    /// <inheritdoc />
    public IServiceProvider Services => _host.Services;

    /// <summary>Creates a new <see cref="SharpMudApplicationBuilder"/> - the entry point for a consumer's own <c>Program.cs</c>.</summary>
    public static SharpMudApplicationBuilder CreateBuilder(string[]? args = null) => new(args);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) => _host.StartAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => _host.StopAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ((IAsyncDisposable)_host).DisposeAsync();
}
