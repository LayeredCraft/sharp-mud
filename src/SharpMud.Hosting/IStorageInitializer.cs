namespace SharpMud.Hosting;

/// <summary>
/// One-time storage setup (e.g. EF Core's <c>EnsureCreatedAsync</c>) a
/// persistence package needs before <see cref="WorldLoaderHostedService"/>
/// can query it. Registered via DI
/// (<c>services.AddSingleton&lt;IStorageInitializer, ...&gt;()</c>) rather
/// than relying on hosted-service registration order between packages,
/// which would be fragile - <see cref="WorldLoaderHostedService"/> always
/// runs every registered initializer before attempting to load anything,
/// regardless of which order the persistence/hosting packages happened to
/// register in.
/// </summary>
public interface IStorageInitializer
{
    Task InitializeAsync(CancellationToken ct);
}
