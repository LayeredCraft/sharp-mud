namespace SharpMud.Engine.Core;

/// <summary>
/// One-time storage setup (e.g. EF Core's <c>EnsureCreatedAsync</c>) a
/// persistence package needs before the world can be loaded. Registered via
/// DI (<c>services.AddSingleton&lt;IStorageInitializer, ...&gt;()</c>)
/// rather than relying on hosted-service registration order between
/// packages, which would be fragile - the consumer always runs every
/// registered initializer before attempting to load anything, regardless of
/// which order the persistence/hosting packages happened to register in.
/// Lives alongside <see cref="IThingRepository"/> in
/// <c>SharpMud.Engine.Core</c> rather than <c>SharpMud.Hosting</c> - both
/// are persistence-lifecycle abstractions a <c>Persistence.*</c> provider
/// package implements, and keeping this one in Engine means providers only
/// ever need their existing <c>Persistence -&gt; Engine</c> reference, not
/// an extra reference to <c>Hosting</c> (see docs/architecture.md's
/// dependency-direction rule).
/// </summary>
public interface IStorageInitializer
{
    /// <summary>Runs any one-time setup this provider needs before the world is loaded.</summary>
    Task InitializeAsync(CancellationToken ct);
}
