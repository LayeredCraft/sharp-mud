using SharpMud.Engine.Core;
using SharpMud.Hosting;

namespace SharpMud.Samples.Classic;

/// <summary>The Classic ruleset's <see cref="IPlayerFactory"/> - wraps <see cref="HubWorldBuilder.CreatePlayer"/>.</summary>
public sealed class ClassicPlayerFactory : IPlayerFactory
{
    public Thing CreatePlayer(World world, string username, string passwordHash, Thing startingRoom) =>
        HubWorldBuilder.CreatePlayer(world, username, passwordHash, startingRoom);
}
