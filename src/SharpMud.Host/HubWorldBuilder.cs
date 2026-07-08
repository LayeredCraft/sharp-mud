using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Ruleset.Classic;

namespace SharpMud.Host;

// Content, not engine or ruleset - mirrors WheelMUD's separate "Universe"
// project (docs/research/wheelmud-findings.md). References Ruleset.Classic
// directly since the cave rat/sword need ruleset-specific behaviors
// (CombatantBehavior, WearableBehavior's EquipSlot) - only Host is allowed
// to know about a specific ruleset (docs/engine-vs-ruleset.md).
public static class HubWorldBuilder
{
    public static (World World, Thing StartingRoom) Build()
    {
        var world = new World();

        var area = CreateArea(world, "The Town");

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

        Connect(world, townSquare, marketStreet, Direction.North);
        Connect(world, townSquare, templeSteps, Direction.East);
        Connect(world, townSquare, southernGate, Direction.South);
        Connect(world, townSquare, oldWell, Direction.West);
        Connect(world, marketStreet, generalStore, Direction.East);

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

    public static Thing CreatePlayer(World world, string name, Thing startingRoom)
    {
        var player = new Thing { Id = ThingId.New(), Name = name };
        player.Behaviors.Add(new PlayerBehavior());
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

    private static Thing CreateArea(World world, string name)
    {
        var area = new Thing { Id = ThingId.New(), Name = name };
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
