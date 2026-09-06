using text_survival.Actors;
using text_survival.Environments.Grid;

namespace text_survival.Environments.Navigation;

public enum PathStatus { Found, AlreadyThere, NoRoute, BudgetExceeded }
public record PathRequest(GridPosition Origin, GridPosition Destination, int MaxExpandedNodes = 10000);
public record PathResult(PathStatus Status, IReadOnlyList<GridPosition> Steps, double CostMinutes = 0);

/// <summary>Replaceable search algorithm; the navigation view owns legal crossings and costs.</summary>
public interface IPathfinder
{
    PathResult FindPath(PathRequest request, NavigationView navigation);
}

public sealed class NavigationView(GameMap map, Actor? mover = null)
{
    public bool Contains(GridPosition position) => map.GetLocationAt(position)?.IsPassable == true;

    public IEnumerable<(GridPosition Position, double CostMinutes)> Neighbors(GridPosition origin)
    {
        var from = map.GetLocationAt(origin);
        if (from == null) yield break;
        foreach (var next in map.GetTravelOptionsFrom(from))
        {
            var position = map.GetPosition(next);
            double cost = mover != null
                ? TravelProcessor.GetTraversalMinutes(from, next, mover, mover.Inventory, map)
                : Math.Max(TravelProcessor.MinimumCrossingMinutes,
                    from.BaseTraversalMinutes + next.BaseTraversalMinutes + map.GetEdgeTraversalModifier(origin, position));
            yield return (position, cost);
        }
    }
}

public static class Navigation
{
    public static PathResult FindRoute(GameMap map, GridPosition origin, GridPosition destination,
        Actor? mover = null, int maxExpandedNodes = 10000) =>
        map.Pathfinder.FindPath(new PathRequest(origin, destination, maxExpandedNodes), new NavigationView(map, mover));

    public static Location? NextStep(Actor mover, Location destination)
    {
        var map = mover.Map;
        var result = FindRoute(map, map.GetPosition(mover.CurrentLocation), map.GetPosition(destination), mover);
        return result.Steps.Count == 0 ? null : map.GetLocationAt(result.Steps[0]);
    }
}

/// <summary>Deterministic weighted baseline. A* can replace this without changing callers.</summary>
public sealed class DijkstraPathfinder : IPathfinder
{
    public PathResult FindPath(PathRequest request, NavigationView navigation)
    {
        if (request.Origin == request.Destination)
            return new(PathStatus.AlreadyThere, []);
        if (!navigation.Contains(request.Origin) || !navigation.Contains(request.Destination))
            return new(PathStatus.NoRoute, []);
        var frontier = new PriorityQueue<GridPosition, (double Cost, int Order)>();
        var costs = new Dictionary<GridPosition, double> { [request.Origin] = 0 };
        var parents = new Dictionary<GridPosition, GridPosition>();
        int order = 0, expanded = 0;
        frontier.Enqueue(request.Origin, (0, order++));
        while (frontier.TryDequeue(out var position, out var priority))
        {
            if (priority.Cost != costs[position]) continue;
            if (position == request.Destination)
            {
                var steps = new List<GridPosition>();
                for (var p = position; p != request.Origin; p = parents[p]) steps.Add(p);
                steps.Reverse();
                return new(PathStatus.Found, steps, priority.Cost);
            }
            if (expanded++ >= request.MaxExpandedNodes) return new(PathStatus.BudgetExceeded, []);
            foreach (var (next, edgeCost) in navigation.Neighbors(position))
            {
                double cost = priority.Cost + edgeCost;
                if (costs.TryGetValue(next, out var existing) && cost >= existing) continue;
                costs[next] = cost;
                parents[next] = position;
                frontier.Enqueue(next, (cost, order++));
            }
        }
        return new(PathStatus.NoRoute, []);
    }
}
