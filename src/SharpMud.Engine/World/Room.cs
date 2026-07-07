namespace SharpMud.Engine.World;

public sealed class Room
{
    public required RoomId Id { get; init; }
    public required AreaId AreaId { get; init; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public bool IsGenerated { get; init; }

    public List<Exit> Exits { get; } = [];
    public List<ItemId> ItemsOnGround { get; } = [];
    public List<NpcId> Npcs { get; } = [];

    public Exit? FindExit(Direction direction) =>
        Exits.FirstOrDefault(exit => exit.Direction == direction);
}
