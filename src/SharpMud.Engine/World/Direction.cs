namespace SharpMud.Engine.World;

public enum Direction
{
    North,
    South,
    East,
    West,
    NorthEast,
    NorthWest,
    SouthEast,
    SouthWest,
    Up,
    Down,
}

public static class DirectionExtensions
{
    public static Direction Opposite(this Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.South => Direction.North,
        Direction.East => Direction.West,
        Direction.West => Direction.East,
        Direction.NorthEast => Direction.SouthWest,
        Direction.SouthWest => Direction.NorthEast,
        Direction.NorthWest => Direction.SouthEast,
        Direction.SouthEast => Direction.NorthWest,
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    public static string ToDisplayString(this Direction direction) => direction switch
    {
        Direction.North => "north",
        Direction.South => "south",
        Direction.East => "east",
        Direction.West => "west",
        Direction.NorthEast => "northeast",
        Direction.NorthWest => "northwest",
        Direction.SouthEast => "southeast",
        Direction.SouthWest => "southwest",
        Direction.Up => "up",
        Direction.Down => "down",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };
}
