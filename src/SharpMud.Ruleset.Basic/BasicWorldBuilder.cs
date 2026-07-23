using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic;

/// <summary>
/// The default small world a fresh <c>SharpMud.Ruleset.Basic</c> consumer
/// gets for free - two rooms and one fightable NPC, enough to walk around
/// and issue <c>attack</c>/<c>flee</c> against something without writing any
/// world content of their own. A real game still wants its own <see
/// cref="IWorldBuilder"/>; this is the quick-start default.
/// </summary>
public sealed class BasicWorldBuilder : IWorldBuilder
{
    // Fixed, not ThingId.New() - so a fresh boot can ask the repository
    // "does this already exist?" instead of always rebuilding. See
    // docs/persistence.md.
    /// <summary>The fixed id of the default world's root area - stable across restarts so a persisted world can be found again.</summary>
    public static readonly ThingId AreaId = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    /// <inheritdoc/>
    public ThingId RootId => AreaId;

    /// <summary>Builds the default two-room world (a Clearing and an Old Watchtower) with one fightable NPC (a wild boar) in the watchtower.</summary>
    public (World World, Thing StartingRoom) Build()
    {
        var world = new World();

        var area = new Thing { Id = AreaId, Name = "The Basic World" };
        area.Behaviors.Add(new AreaBehavior());
        world.Register(area);

        var clearing = CreateRoom(world, area, "Clearing",
            "A quiet clearing ringed by tall grass. A worn path leads north.");
        var watchtower = CreateRoom(world, area, "Old Watchtower",
            "A crumbling stone watchtower, long abandoned. Something rustles nearby.");

        Connect(world, clearing, watchtower, Direction.North);

        var boar = new Thing { Id = ThingId.New(), Name = "wild boar" };
        boar.Behaviors.Add(new NpcBehavior());
        boar.Behaviors.Add(new CombatantBehavior
        {
            MaxHitPoints = 8,
            CurrentHitPoints = 8,
            ArmorClass = 8,
            DamageMin = 1,
            DamageMax = 3,
            ExperienceReward = 10,
        });
        watchtower.Add(boar);
        world.Register(boar);

        return (world, clearing);
    }

    /// <inheritdoc/>
    public Thing FindStartingRoom(Thing root) =>
        root.Children.FirstOrDefault(c => c.HasBehavior<RoomBehavior>() && c.Name == "Clearing")
        ?? root.Children.First(c => c.HasBehavior<RoomBehavior>());

    private static Thing CreateRoom(World world, Thing area, string name, string description)
    {
        var room = new Thing { Id = ThingId.New(), Name = name, Description = description };
        room.Behaviors.Add(new RoomBehavior());
        area.Add(room);
        world.Register(room);
        return room;
    }

    // Two exit Things per connection - one per direction (docs/engine-vs-ruleset.md
    // Decisions), each a child of the room it exits from.
    private static void Connect(World world, Thing a, Thing b, Direction direction)
    {
        var aToB = new Thing { Id = ThingId.New(), Name = direction.ToDisplayString() };
        aToB.Behaviors.Add(new ExitBehavior { Direction = direction, Destination = b });
        a.Add(aToB);
        world.Register(aToB);

        var bToA = new Thing { Id = ThingId.New(), Name = direction.Opposite().ToDisplayString() };
        bToA.Behaviors.Add(new ExitBehavior { Direction = direction.Opposite(), Destination = a });
        b.Add(bToA);
        world.Register(bToA);
    }
}
