using SharpMud.Engine.Core;

namespace SharpMud.Hosting;

/// <summary>
/// Creates a fresh player <see cref="Thing"/> at character creation -
/// registered once via DI (<c>services.AddSingleton&lt;IPlayerFactory,
/// MyPlayerFactory&gt;()</c>) so <see cref="LoginFlow"/>/
/// <see cref="PlayerLogin"/> stay ruleset-agnostic instead of calling a
/// specific ruleset's character-creation logic directly.
/// </summary>
public interface IPlayerFactory
{
    Thing CreatePlayer(World world, string username, string passwordHash, Thing startingRoom);
}
