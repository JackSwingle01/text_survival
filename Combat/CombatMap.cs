namespace text_survival.Combat;

/// <summary>
/// Manages the 2D combat grid and actor positions.
/// AI layer queries this for distances; this class handles grid mechanics.
/// </summary>
public class CombatMap
{
    public const int GridSize = 25;
    public const double CellSizeMeters = 1.0;

    private readonly Dictionary<object, CombatPosition> _positions = new();

    public void SetPosition(object actor, CombatPosition position)
    {
        _positions[actor] = ClampToGrid(position);
    }

    /// <summary>
    /// Place an actor at a distance from another actor.
    /// Used when spawning enemies at initial encounter distance.
    /// </summary>
    public void SetPositionAtDistance(object actor, object relativeTo, double distanceMeters, double angleDegrees = 0)
    {
        if (!_positions.TryGetValue(relativeTo, out var refPos))
        {
            throw new InvalidOperationException($"Reference actor not on grid");
        }

        double cells = distanceMeters / CellSizeMeters;
        double angleRad = angleDegrees * Math.PI / 180.0;

        int dx = (int)Math.Round(cells * Math.Cos(angleRad));
        int dy = (int)Math.Round(cells * Math.Sin(angleRad));

        var newPos = new CombatPosition(refPos.X + dx, refPos.Y + dy);
        _positions[actor] = ClampToGrid(newPos);
    }

    public CombatPosition? GetPosition(object actor)
    {
        return _positions.TryGetValue(actor, out var pos) ? pos : null;
    }

    public IReadOnlyDictionary<object, CombatPosition> GetAllPositions() => _positions;

    public void RemoveActor(object actor)
    {
        _positions.Remove(actor);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Distance queries (what AI layer uses)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get distance between two actors in meters.
    /// </summary>
    public double GetDistanceMeters(object actorA, object actorB)
    {
        if (!_positions.TryGetValue(actorA, out var posA) ||
            !_positions.TryGetValue(actorB, out var posB))
        {
            return double.MaxValue;
        }

        return posA.DistanceTo(posB) * CellSizeMeters;
    }

    /// <summary>
    /// Get the distance zone between two actors.
    /// </summary>
    public DistanceZone GetZone(object actorA, object actorB)
    {
        double distance = GetDistanceMeters(actorA, actorB);
        return DistanceZoneHelper.GetZone(distance);
    }

    /// <summary>
    /// Get distance to nearest threat from a list.
    /// </summary>
    public double GetDistanceToNearest(object actor, IEnumerable<object> threats)
    {
        double minDistance = double.MaxValue;
        foreach (var threat in threats)
        {
            double dist = GetDistanceMeters(actor, threat);
            if (dist < minDistance) minDistance = dist;
        }
        return minDistance;
    }

    /// <summary>
    /// Find the nearest actor from a list.
    /// </summary>
    public object? GetNearestActor(object from, IEnumerable<object> candidates)
    {
        object? nearest = null;
        double minDistance = double.MaxValue;

        foreach (var candidate in candidates)
        {
            double dist = GetDistanceMeters(from, candidate);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = candidate;
            }
        }
        return nearest;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Movement (AI returns intent, this updates grid)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Move an actor toward a target by a distance in meters.
    /// Returns actual distance moved (may be less if already adjacent).
    /// </summary>
    public double MoveToward(object actor, object target, double distanceMeters)
    {
        if (!_positions.TryGetValue(actor, out var actorPos) ||
            !_positions.TryGetValue(target, out var targetPos))
        {
            return 0;
        }

        int steps = (int)Math.Round(distanceMeters / CellSizeMeters);
        if (steps < 1) steps = 1;

        var currentPos = actorPos;
        for (int i = 0; i < steps; i++)
        {
            if (currentPos.SameCell(targetPos)) break;
            currentPos = currentPos.StepToward(targetPos);
        }

        currentPos = ClampToGrid(currentPos);
        _positions[actor] = currentPos;

        return actorPos.DistanceTo(currentPos) * CellSizeMeters;
    }

    /// <summary>
    /// Move an actor away from a threat by a distance in meters.
    /// Returns actual distance moved.
    /// </summary>
    public double MoveAway(object actor, object threat, double distanceMeters)
    {
        if (!_positions.TryGetValue(actor, out var actorPos) ||
            !_positions.TryGetValue(threat, out var threatPos))
        {
            return 0;
        }

        int steps = (int)Math.Round(distanceMeters / CellSizeMeters);
        if (steps < 1) steps = 1;

        var currentPos = actorPos;
        for (int i = 0; i < steps; i++)
        {
            var nextPos = currentPos.StepAway(threatPos);
            nextPos = ClampToGrid(nextPos);

            // Stop if we can't move further (at grid edge)
            if (nextPos.SameCell(currentPos)) break;
            currentPos = nextPos;
        }

        _positions[actor] = currentPos;
        return actorPos.DistanceTo(currentPos) * CellSizeMeters;
    }

    /// <summary>
    /// Move laterally (circling behavior) - maintains distance while repositioning.
    /// </summary>
    public void MoveLateral(object actor, object relativeTo, bool clockwise = true)
    {
        if (!_positions.TryGetValue(actor, out var actorPos) ||
            !_positions.TryGetValue(relativeTo, out var refPos))
        {
            return;
        }

        // Calculate perpendicular direction
        var (dx, dy) = actorPos.DirectionTo(refPos);
        int perpX = clockwise ? -dy : dy;
        int perpY = clockwise ? dx : -dx;

        var newPos = new CombatPosition(actorPos.X + perpX, actorPos.Y + perpY);
        _positions[actor] = ClampToGrid(newPos);
    }

    /// <summary>
    /// Check if actor is at the edge of the grid (can flee).
    /// </summary>
    public bool IsAtEdge(object actor)
    {
        if (!_positions.TryGetValue(actor, out var pos)) return false;
        return pos.X == 0 || pos.X == GridSize - 1 ||
               pos.Y == 0 || pos.Y == GridSize - 1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Grid helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static CombatPosition ClampToGrid(CombatPosition pos)
    {
        return new CombatPosition(
            Math.Clamp(pos.X, 0, GridSize - 1),
            Math.Clamp(pos.Y, 0, GridSize - 1)
        );
    }

    /// <summary>
    /// Get center position of the grid.
    /// </summary>
    public static CombatPosition CenterPosition => new(GridSize / 2, GridSize / 2);
}
