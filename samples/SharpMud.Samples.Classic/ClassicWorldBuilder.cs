using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;

namespace SharpMud.Samples.Classic;

/// <summary>The Classic ruleset's <see cref="IWorldBuilder"/> - wraps <see cref="HubWorldBuilder"/> for <see cref="SharpMud.Hosting.WorldLoaderHostedService"/>.</summary>
public sealed class ClassicWorldBuilder : IWorldBuilder
{
    public ThingId RootId => HubWorldBuilder.HubAreaId;

    public (World World, Thing StartingRoom) Build() => HubWorldBuilder.Build();

    public Thing FindStartingRoom(Thing root) =>
        HubWorldBuilder.FindStartingRoom(root) ?? root.Children.First(c => c.HasBehavior<RoomBehavior>());
}
