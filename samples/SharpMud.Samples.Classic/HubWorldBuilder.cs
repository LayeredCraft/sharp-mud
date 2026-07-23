using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Samples.Classic;

// Content, not engine or ruleset - mirrors WheelMUD's separate "Universe"
// project (docs/research/wheelmud-findings.md). Lives alongside the ruleset
// code in this sample rather than a separate project, per ADR-0006 - a
// consumer's own world content and their ruleset are the two things that
// make their game theirs, both belong in their one project.
public static class HubWorldBuilder
{
    // Fixed, not ThingId.New() - so a fresh boot can ask the repository
    // "does this already exist?" (LoadTreeAsync(HubAreaId)) instead of
    // always rebuilding. See docs/persistence.md.
    public static readonly ThingId HubAreaId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    public static (World World, Thing StartingRoom) Build()
    {
        var world = new World();

        var area = CreateArea(world, "The Town", HubAreaId);

        var townSquare = CreateRoom(world, area, "Town Square",
            "A weathered stone fountain bubbles quietly at the center of a wide square. Cobblestone streets lead off in every direction.");
        var marketStreet = CreateRoom(world, area, "Market Street",
            "Shuttered stalls line a narrow street that smells faintly of spice and woodsmoke.");
        var templeSteps = CreateRoom(world, area, "Temple Steps",
            "Broad marble steps climb toward a temple whose doors are carved with worn, unreadable script.");
        var southernGate = CreateRoom(world, area, "Southern Gate",
            "A heavy iron gate stands open, leading out toward the dark line of the wilderness beyond.");
        var oldWell = CreateRoom(world, area, "Old Well",
            "A mossy stone well sits abandoned here, its rope long since rotted away.");
        var generalStore = CreateRoom(world, area, "General Store",
            "Shelves crowded with dusty goods line the walls of this cramped little shop.");

        RoomConnector.Connect(world, townSquare, marketStreet, Direction.North);
        RoomConnector.Connect(world, townSquare, templeSteps, Direction.East);
        RoomConnector.Connect(world, townSquare, southernGate, Direction.South);
        RoomConnector.Connect(world, townSquare, oldWell, Direction.West);
        RoomConnector.Connect(world, marketStreet, generalStore, Direction.East);

        var caveRat = new Thing { Id = ThingId.New(), Name = "cave rat" };
        caveRat.Behaviors.Add(new NpcBehavior());
        caveRat.Behaviors.Add(new CombatantBehavior
        {
            MaxHitPoints = 6,
            CurrentHitPoints = 6,
            ArmorClass = 8,
            DamageMin = 1,
            DamageMax = 3,
            ExperienceReward = 10,
        });
        caveRat.Behaviors.Add(new WanderingBehavior());
        oldWell.Add(caveRat);
        world.Register(caveRat);

        var sword = CreateItem(world, "rusty sword", "A short sword, pitted with rust but still sharp.", EquipSlot.MainHand);
        townSquare.Add(sword);

        var cap = CreateItem(world, "leather cap", "A worn leather cap, cracked at the seams.", EquipSlot.Head);
        generalStore.Add(cap);

        var coin = CreateItem(world, "gold coin", "A small, tarnished gold coin.", null);
        oldWell.Add(coin);

        return (world, townSquare);
    }

    public static Thing CreatePlayer(World world, string username, string passwordHash, Thing startingRoom)
    {
        var player = new Thing { Id = ThingId.New(), Name = username };
        player.Behaviors.Add(new PlayerBehavior { Username = username, PasswordHash = passwordHash });
        player.Behaviors.Add(new EquippedBehavior());

        // Dice-roll character creation (docs/character.md) isn't implemented
        // yet - this gives the v1 local CLI a playable character without it.
        player.Behaviors.Add(new StatsBehavior
        {
            Race = Race.Human,
            Class = CharacterClass.Warrior,
            Strength = 10,
            Dexterity = 10,
            Constitution = 10,
            Intelligence = 10,
            Wisdom = 10,
            Charisma = 10,
            MaxHitPoints = 20,
            CurrentHitPoints = 20,
            MaxMana = 10,
            CurrentMana = 10,
            MaxStamina = 10,
            CurrentStamina = 10,
        });
        player.Behaviors.Add(new CombatantBehavior
        {
            MaxHitPoints = 20,
            CurrentHitPoints = 20,
            ArmorClass = 10,
            DamageMin = 1,
            DamageMax = 4,
        });

        startingRoom.Add(player);
        world.Register(player);
        return player;
    }

    // Rooms are found by name after a load (see FindStartingRoom) - simple
    // and sufficient for the hand-built hub; would not scale to a large
    // data-driven or procedurally generated world (see docs/persistence.md
    // Open Items).
    public static Thing? FindStartingRoom(Thing hubArea) =>
        hubArea.Children.FirstOrDefault(c => c.HasBehavior<RoomBehavior>() && c.Name == "Town Square");

    private static Thing CreateArea(World world, string name, ThingId id)
    {
        var area = new Thing { Id = id, Name = name };
        area.Behaviors.Add(new AreaBehavior());
        world.Register(area);
        return area;
    }

    private static Thing CreateRoom(World world, Thing area, string name, string description)
    {
        var room = new Thing { Id = ThingId.New(), Name = name, Description = description };
        room.Behaviors.Add(new RoomBehavior());
        area.Add(room);
        world.Register(room);
        return room;
    }

    private static Thing CreateItem(World world, string name, string description, EquipSlot? slot)
    {
        var item = new Thing { Id = ThingId.New(), Name = name, Description = description };
        item.Behaviors.Add(new ItemBehavior());
        if (slot is { } s)
            item.Behaviors.Add(new WearableBehavior { Slot = s });

        world.Register(item);
        return item;
    }
}
