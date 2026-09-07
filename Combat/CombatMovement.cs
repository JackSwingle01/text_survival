using text_survival.Actors.Animals;
using text_survival.Environments.Grid;

namespace text_survival.Combat;

public static class CombatMovement
{
    // Tactical metres per action, not a conversion from world-time seconds.
    public static int Allowance(Unit unit) => unit.actor is Animal animal ? animal.AnimalType switch
    {
        AnimalType.CaveBear => 4,
        AnimalType.Bear => 5,
        AnimalType.Wolf or AnimalType.Hyena => 6,
        AnimalType.SaberTooth => 7,
        _ => 3
    } : 3;

    public static Unit? PursuitTarget(Unit unit, CombatScenario scenario)
    {
        if (unit.Awareness != AwarenessState.Engaged || unit.actor is not Animal animal ||
            animal.BehaviorType == AnimalBehaviorType.Prey) return null;
        var target = scenario.GetNearestEnemy(unit);
        return target != null && unit.DetermineAttack(target) && unit.Boldness > unit.PerceivedThreat(target)
            ? target : null;
    }

    /// <summary>Find a reachable closing position without crossing occupied cells or cutting corners.</summary>
    public static GridPosition Pursue(Unit unit, Unit target, CombatScenario scenario)
    {
        var occupied = scenario.Units.Where(u => u != unit && u.actor.IsAlive).Select(u => u.Position).ToHashSet();
        var costs = new Dictionary<GridPosition, double> { [unit.Position] = 0 };
        var queue = new PriorityQueue<GridPosition, double>();
        queue.Enqueue(unit.Position, 0);
        double budget = Allowance(unit);
        while (queue.TryDequeue(out var current, out var cost))
        {
            if (cost > costs[current]) continue;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var next = new GridPosition(current.X + dx, current.Y + dy);
                    if (next.X < 0 || next.Y < 0 || next.X >= CombatScenario.MAP_SIZE || next.Y >= CombatScenario.MAP_SIZE || occupied.Contains(next)) continue;
                    if (dx != 0 && dy != 0 && (occupied.Contains(new(current.X + dx, current.Y)) || occupied.Contains(new(current.X, current.Y + dy)))) continue;
                    double nextCost = cost + (dx != 0 && dy != 0 ? Math.Sqrt(2) : 1);
                    if (nextCost > budget || costs.TryGetValue(next, out var existing) && existing <= nextCost) continue;
                    costs[next] = nextCost;
                    queue.Enqueue(next, nextCost);
                }
        }
        return costs.Keys.OrderBy(p => p.DistanceTo(target.Position)).ThenBy(p => costs[p]).First();
    }
}
