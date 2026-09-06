using text_survival.Actors;
using text_survival.Environments.Grid;

namespace text_survival.Environments.Perception;

/// <summary>Observer-local spatial queries. Never changes exploration, actor state or time.</summary>
public static class Sight
{
    public static bool CanSeeActor(Actor observer, Actor target)
    {
        if (observer.Map != target.Map) return false;
        var capacities = observer.GetCapacities();
        if (capacities.Sight <= 0 || capacities.Consciousness <= 0) return false;
        return CanSeeTile(observer.Map, observer.Map.GetPosition(observer.CurrentLocation),
            observer.Map.GetPosition(target.CurrentLocation), capacities.Sight);
    }

    public static bool CanSeeTile(GameMap map, GridPosition origin, GridPosition target, double sightCapacity = 1)
    {
        var location = map.GetLocationAt(origin);
        if (location == null || map.GetLocationAt(target) == null) return false;
        double budget = GetSightRange(location, sightCapacity) * (map.Weather?.VisibilityFactor ?? 1);
        return HasLineOfSight(map, origin, target, budget);
    }

    public static IEnumerable<GridPosition> VisibleTiles(GameMap map, GridPosition origin, double sightCapacity = 1)
    {
        var location = map.GetLocationAt(origin);
        if (location == null) yield break;
        double budget = GetSightRange(location, sightCapacity) * (map.Weather?.VisibilityFactor ?? 1);
        int range = (int)Math.Ceiling(budget);
        for (int x = Math.Max(0, origin.X - range); x <= Math.Min(map.Width - 1, origin.X + range); x++)
            for (int y = Math.Max(0, origin.Y - range); y <= Math.Min(map.Height - 1, origin.Y + range); y++)
            {
                var position = new GridPosition(x, y);
                if (map.GetLocationAt(position) != null && HasLineOfSight(map, origin, position, budget))
                    yield return position;
            }
    }

    /// <summary>Maximum open-ground sight budget; intervening tiles consume it along each ray.</summary>
    public static int GetSightRange(Location location, double sightCapacity = 1.0)
    {
        // Existing hill terrain and overlook visibility values encode vantage height.
        // Weather.Elevation is regional, not a heightmap, so it cannot give local advantage.
        double vantage = Math.Max(location.VisibilityFactor,
            location.Terrain == TerrainType.Hills ? TerrainType.Hills.BaseVisibility() : 0);
        double bonus = Math.Clamp((vantage - 1.3) * 20, 0, 8);
        return (int)Math.Round((11 + bonus) * Math.Clamp(sightCapacity, 0, 1));
    }

    private static double SightAbsorption(Location location) =>
        location.Terrain == TerrainType.Mountain || location.VisibilityFactor <= 0
            ? double.PositiveInfinity
            : Math.Max(1, 1 / (location.VisibilityFactor * location.VisibilityFactor));

    /// <summary>
    /// Traverse every cell intersected by a centre-to-centre ray, charging its exact
    /// segment length. Reveal the obstructing destination before charging its cost.
    /// </summary>
    private static bool HasLineOfSight(GameMap map, GridPosition origin, GridPosition target, double budget)
    {
        if (target == origin) return true;
        int dx = target.X - origin.X, dy = target.Y - origin.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (budget <= 0 || distance > budget) return false;

        int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
        double strideX = dx == 0 ? double.PositiveInfinity : distance / Math.Abs(dx);
        double strideY = dy == 0 ? double.PositiveInfinity : distance / Math.Abs(dy);
        double nextX = strideX / 2, nextY = strideY / 2, travelled = 0;
        int x = origin.X, y = origin.Y;

        while (x != target.X || y != target.Y)
        {
            var location = map.GetLocationAt(x, y);
            if (location == null) return false;
            double exit = Math.Min(nextX, nextY);
            budget -= (exit - travelled) * SightAbsorption(location);
            if (budget <= 0) return false;
            travelled = exit;

            bool crossX = nextX <= exit + 1e-9, crossY = nextY <= exit + 1e-9;
            // A diagonal cannot peek through the seam between two opaque tiles.
            if (crossX && crossY && IsOpaque(map, x + stepX, y) && IsOpaque(map, x, y + stepY))
                return false;
            if (crossX) { x += stepX; nextX += strideX; }
            if (crossY) { y += stepY; nextY += strideY; }
        }
        return true;
    }

    private static bool IsOpaque(GameMap map, int x, int y) =>
        map.GetLocationAt(x, y) is not { } location || double.IsPositiveInfinity(SightAbsorption(location));

}
