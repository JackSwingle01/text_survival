namespace text_survival.Environments.Grid;

/// <summary>
/// Represents a position on the tile grid.
/// Uses (X, Y) coordinates where (0,0) is top-left.
/// </summary>
public readonly record struct GridPosition(int X, int Y)
{
    /// <summary>
    /// Calculate Manhattan distance to another position.
    /// Used for visibility range and adjacency checks.
    /// </summary>
    public int ManhattanDistance(GridPosition other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>
    /// Check if another position is adjacent (4-way cardinal).
    /// </summary>
    public bool IsAdjacentTo(GridPosition other) =>
        ManhattanDistance(other) == 1;

    /// <summary>
    /// Get the 4 cardinal neighbor positions (N, E, S, W).
    /// Note: Does not check bounds - caller must validate.
    /// </summary>
    public IEnumerable<GridPosition> GetCardinalNeighbors() =>
    [
        new GridPosition(X, Y - 1),  // North
        new GridPosition(X + 1, Y),  // East
        new GridPosition(X, Y + 1),  // South
        new GridPosition(X - 1, Y)   // West
    ];

    /// <summary>
    /// Get all positions within a given Manhattan distance.
    /// Used for visibility calculations.
    /// </summary>
    public IEnumerable<GridPosition> GetPositionsInRange(int range)
    {
        for (int dx = -range; dx <= range; dx++)
        {
            int remainingRange = range - Math.Abs(dx);
            for (int dy = -remainingRange; dy <= remainingRange; dy++)
            {
                yield return new GridPosition(X + dx, Y + dy);
            }
        }
    }

    public override string ToString() => $"({X}, {Y})";
}
