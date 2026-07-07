namespace SharpMud.Engine;

// UUIDv7 (not v4) - these become EF Core primary keys, and v7's embedded
// timestamp keeps inserts roughly sequential, avoiding the B-tree
// fragmentation random v4 GUIDs cause (see docs/persistence.md).

public readonly record struct RoomId(Guid Value)
{
    public static RoomId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct AreaId(Guid Value)
{
    public static AreaId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct ItemId(Guid Value)
{
    public static ItemId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct NpcId(Guid Value)
{
    public static NpcId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
