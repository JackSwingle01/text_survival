namespace text_survival.Environments.Grid;

/// <summary>
/// Cardinal directions for tile-to-tile movement and edge storage.
/// </summary>
public enum Direction { North, East, South, West }

public static class DirectionExtensions
{
    /// <summary>
    /// Get the neighbor position in this direction.
    /// </summary>
    public static GridPosition GetNeighbor(this Direction dir, GridPosition from) => dir switch
    {
        Direction.North => new GridPosition(from.X, from.Y - 1),
        Direction.East => new GridPosition(from.X + 1, from.Y),
        Direction.South => new GridPosition(from.X, from.Y + 1),
        Direction.West => new GridPosition(from.X - 1, from.Y),
        _ => from
    };

    /// <summary>
    /// Get the opposite direction.
    /// </summary>
    public static Direction Opposite(this Direction dir) => dir switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => dir
    };

    /// <summary>
    /// Get direction from one position to an adjacent position.
    /// Returns null if not adjacent.
    /// </summary>
    public static Direction? GetDirection(GridPosition from, GridPosition to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        if (dx == 0 && dy == -1) return Direction.North;
        if (dx == 1 && dy == 0) return Direction.East;
        if (dx == 0 && dy == 1) return Direction.South;
        if (dx == -1 && dy == 0) return Direction.West;
        return null;
    }

    /// <summary>
    /// Get all four cardinal directions.
    /// </summary>
    public static IEnumerable<Direction> All =>
        [Direction.North, Direction.East, Direction.South, Direction.West];
}
