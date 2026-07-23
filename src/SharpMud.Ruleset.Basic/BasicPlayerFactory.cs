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

    public BasicPlayerFactory(BasicRulesetOptions options)
    {
        _options = options;
    }

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
