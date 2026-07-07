namespace SharpMud.Engine.World;

public sealed class ExitLockState
{
    public bool IsLocked { get; set; }
    public bool IsClosed { get; set; }
    public ItemId? RequiredKeyItemId { get; set; }
}

public sealed class Exit
{
    public required Direction Direction { get; init; }
    public required RoomId DestinationRoomId { get; init; }
    public bool IsOneWay { get; init; }
    public ExitLockState? Lock { get; set; }
}
