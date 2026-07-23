using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic;

/// <summary>
/// The Basic ruleset's <see cref="IPlayerFactory"/> - without this, <see
/// cref="LoginFlow"/>/<see cref="PlayerLogin"/> (which constructor-inject
/// <see cref="IPlayerFactory"/>) can't create a fresh CLI/Telnet player at
/// all, and the quick-start fails at first login.
/// </summary>
public sealed class BasicPlayerFactory : IPlayerFactory
{
    private readonly BasicRulesetOptions _options;

    /// <summary>Creates the factory against the configured <see cref="BasicRulesetOptions"/> (starting HP/AC/damage).</summary>
    public BasicPlayerFactory(BasicRulesetOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Creates a player <see cref="Thing"/> with <see
    /// cref="PlayerBehavior"/>, <see cref="EquippedBehavior"/>, <see
    /// cref="BasicStatsBehavior"/>, and <see cref="CombatantBehavior"/>
    /// (seeded from <see cref="BasicRulesetOptions"/>), adds it to <paramref
    /// name="startingRoom"/>, and registers it with <paramref name="world"/>.
    /// </summary>
    public Thing CreatePlayer(World world, string username, string passwordHash, Thing startingRoom)
    {
        var player = new Thing { Id = ThingId.New(), Name = username };
        player.Behaviors.Add(new PlayerBehavior { Username = username, PasswordHash = passwordHash });
        player.Behaviors.Add(new EquippedBehavior());
        player.Behaviors.Add(new BasicStatsBehavior());
        player.Behaviors.Add(new CombatantBehavior
        {
            MaxHitPoints = _options.StartingHitPoints,
            CurrentHitPoints = _options.StartingHitPoints,
            ArmorClass = _options.StartingArmorClass,
            DamageMin = _options.StartingDamageMin,
            DamageMax = _options.StartingDamageMax,
        });

        startingRoom.Add(player);
        world.Register(player);
        return player;
    }
}
