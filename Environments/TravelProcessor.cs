using text_survival.Actors.Player;

namespace text_survival.Environments;

public static class TravelProcessor
{
    public static int GetTraversalMinutes(Location location, Player player)
    {
        if (location.BaseTraversalMinutes == 0) return 0;

        double multiplier = GetTerrainMultiplier(location.Terrain);

        // Weather from location's zone
        var weather = location.Parent.Weather;
        if (weather.WindSpeed > 0.5)
            multiplier *= 1 + (weather.WindSpeed * 0.3 * location.Exposure);
        if (weather.Precipitation > 0.5)
            multiplier *= 1 + (weather.Precipitation * 0.2);

        // Player state
        double encumbrance = player.inventoryManager.GetEncumbrance();
        if (encumbrance > 0.5)
            multiplier *= 1 + (encumbrance * 0.4);

        double speed = player.Speed;
        if (speed < 1.0)
            multiplier *= 1 + (1 - speed);  // Slower = longer time
        else if (speed > 1.0)
            multiplier *= 1 / speed;  // Faster = shorter time

        return (int)Math.Ceiling(location.BaseTraversalMinutes * multiplier);
    }

    public static int GetPathMinutes(List<Location> path, Player player)
    {
        return path.Skip(1).Sum(loc => GetTraversalMinutes(loc, player));
    }

    public static List<Location>? FindPath(Location start, Location target)
    {
        if (start == target) return [start];

        var visited = new HashSet<Location>();
        var queue = new Queue<List<Location>>();
        queue.Enqueue([start]);

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var current = path.Last();

            if (current == target)
                return path;

            if (visited.Contains(current))
                continue;

            visited.Add(current);

            foreach (var connection in current.Connections)
            {
                if (connection.Explored &&
                    !visited.Contains(connection))
                {
                    queue.Enqueue([.. path, connection]);
                }
            }
        }

        return null;
    }

    public static List<Location> BuildRoundTripPath(List<Location> outboundPath)
    {
        // outbound: [Camp, TrailA, Dest]
        // return portion: [TrailA, Camp]
        // full: [Camp, TrailA, Dest, TrailA, Camp]

        var returnPath = outboundPath.Take(outboundPath.Count - 1).Reverse();
        return outboundPath.Concat(returnPath).ToList();
    }

    public static List<Location>? BuildRoundTripPath(Location origin, Location destination)
    {
        var outboundPath = FindPath(origin, destination);
        if (outboundPath is null) return null;
        return BuildRoundTripPath(outboundPath);
    }

    public static double GetTerrainMultiplier(TerrainType terrain) => terrain switch
    {
        TerrainType.Clear => 1.0,
        TerrainType.Rough => 1.4,
        TerrainType.Snow => 1.6,
        TerrainType.Steep => 1.8,
        TerrainType.Water => 2.0,
        TerrainType.Hazardous => 1.5,
        _ => 1.0
    };

    public static List<Location> GetReachableSites(Location from)
    {
        var sites = new List<Location>();
        var visited = new HashSet<Location>();
        var queue = new Queue<Location>();

        foreach (var connection in from.Connections.Where(c => c.Explored))
            queue.Enqueue(connection);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current)) continue;
            visited.Add(current);

            if (current.IsSite && current != from)
                sites.Add(current);
            else
            {
                foreach (var next in current.Connections.Where(c => c.Explored && !visited.Contains(c)))
                    queue.Enqueue(next);
            }
        }
        return sites;
    }
}