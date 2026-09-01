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
    /// Get all four cardinal directions.
    /// </summary>
    public static IEnumerable<Direction> All =>
        [Direction.North, Direction.East, Direction.South, Direction.West];
}
