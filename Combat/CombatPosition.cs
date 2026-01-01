namespace text_survival.Combat;

/// <summary>
/// Represents a position on the combat grid.
/// Uses (X, Y) integer coordinates where each cell is ~2.5m.
/// </summary>
public readonly record struct CombatPosition(int X, int Y)
{
    /// <summary>
    /// Calculate Euclidean distance to another position in grid cells.
    /// </summary>
    public double DistanceTo(CombatPosition other)
    {
        int dx = X - other.X;
        int dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Get the direction vector toward another position (normalized-ish).
    /// Returns (0,0) if positions are the same.
    /// </summary>
    public (int dx, int dy) DirectionTo(CombatPosition other)
    {
        int dx = other.X - X;
        int dy = other.Y - Y;

        if (dx == 0 && dy == 0) return (0, 0);

        // Normalize to -1, 0, or 1 for each axis
        return (Math.Sign(dx), Math.Sign(dy));
    }

    /// <summary>
    /// Move one step toward another position.
    /// Allows diagonal movement.
    /// </summary>
    public CombatPosition StepToward(CombatPosition target)
    {
        var (dx, dy) = DirectionTo(target);
        return new CombatPosition(X + dx, Y + dy);
    }

    /// <summary>
    /// Move one step away from another position.
    /// </summary>
    public CombatPosition StepAway(CombatPosition threat)
    {
        var (dx, dy) = DirectionTo(threat);
        return new CombatPosition(X - dx, Y - dy);
    }

    /// <summary>
    /// Check if positions are the same cell.
    /// </summary>
    public bool SameCell(CombatPosition other) => X == other.X && Y == other.Y;

    public override string ToString() => $"({X}, {Y})";
}
