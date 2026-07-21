using SharpMud.Engine.Core;

namespace SharpMud.Hosting;

/// <summary>
/// A consumer's world content and bootstrap logic - the ruleset-agnostic
/// generic-host equivalent of what today's <c>HubWorldBuilder</c> is for
/// the Classic ruleset. Registered once via DI
/// (<c>services.AddSingleton&lt;IWorldBuilder, MyWorldBuilder&gt;()</c>);
/// <see cref="WorldLoaderHostedService"/> calls it during startup to load a
/// persisted world or build a fresh one.
/// </summary>
public interface IWorldBuilder
{
    /// <summary>The root <see cref="Thing"/>'s fixed id - used to look up a previously-persisted world.</summary>
    ThingId RootId { get; }

    /// <summary>Builds a brand-new world when nothing is persisted yet, returning the world and the room new players start in.</summary>
    (World World, Thing StartingRoom) Build();

    /// <summary>Locates the starting room within a world reloaded from persistence.</summary>
    Thing FindStartingRoom(Thing root);
}
