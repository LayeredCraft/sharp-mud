using SharpMud.Engine.Characters;

namespace SharpMud.Engine.World;

// Phase 1 of the content-authoring evolution in docs/world-model.md:
// hardcoded rooms in C#, just to get the engine loop working. Data-driven
// world files and the generated frontier are later phases.
public static class WorldBuilder
{
    public static (World World, RoomId StartingRoomId) BuildHub()
    {
        var world = new World();
        var areaId = AreaId.New();

        var townSquare = CreateRoom(world, areaId, "Town Square",
            "A weathered stone fountain bubbles quietly at the center of a wide square. Cobblestone streets lead off in every direction.");
        var marketStreet = CreateRoom(world, areaId, "Market Street",
            "Shuttered stalls line a narrow street that smells faintly of spice and woodsmoke.");
        var templeSteps = CreateRoom(world, areaId, "Temple Steps",
            "Broad marble steps climb toward a temple whose doors are carved with worn, unreadable script.");
        var southernGate = CreateRoom(world, areaId, "Southern Gate",
            "A heavy iron gate stands open, leading out toward the dark line of the wilderness beyond.");
        var oldWell = CreateRoom(world, areaId, "Old Well",
            "A mossy stone well sits abandoned here, its rope long since rotted away.");
        var generalStore = CreateRoom(world, areaId, "General Store",
            "Shelves crowded with dusty goods line the walls of this cramped little shop.");

        Connect(townSquare, marketStreet, Direction.North);
        Connect(townSquare, templeSteps, Direction.East);
        Connect(townSquare, southernGate, Direction.South);
        Connect(townSquare, oldWell, Direction.West);
        Connect(marketStreet, generalStore, Direction.East);

        world.RegisterNpc(new Npc
        {
            Id = NpcId.New(),
            Name = "cave rat",
            RoomId = oldWell.Id,
            MaxHitPoints = 6,
            CurrentHitPoints = 6,
            ArmorClass = 8,
            DamageMin = 1,
            DamageMax = 3,
            ExperienceReward = 10,
        });

        world.RegisterItem(new Item
        {
            Id = ItemId.New(),
            Name = "rusty sword",
            Description = "A short sword, pitted with rust but still sharp.",
            Slot = EquipSlot.MainHand,
        }, townSquare.Id);

        world.RegisterItem(new Item
        {
            Id = ItemId.New(),
            Name = "leather cap",
            Description = "A worn leather cap, cracked at the seams.",
            Slot = EquipSlot.Head,
        }, generalStore.Id);

        world.RegisterItem(new Item
        {
            Id = ItemId.New(),
            Name = "gold coin",
            Description = "A small, tarnished gold coin.",
        }, oldWell.Id);

        return (world, townSquare.Id);
    }

    private static Room CreateRoom(World world, AreaId areaId, string name, string description)
    {
        var room = new Room
        {
            Id = RoomId.New(),
            AreaId = areaId,
            Name = name,
            Description = description,
        };
        world.RegisterRoom(room);
        return room;
    }

    // Bidirectional by default (docs/world-model.md: construction-time
    // convenience, not a runtime invariant - one-way/locked exits still work
    // by adding an Exit without a matching reverse).
    private static void Connect(Room a, Room b, Direction direction)
    {
        a.Exits.Add(new Exit { Direction = direction, DestinationRoomId = b.Id });
        b.Exits.Add(new Exit { Direction = direction.Opposite(), DestinationRoomId = a.Id });
    }
}
