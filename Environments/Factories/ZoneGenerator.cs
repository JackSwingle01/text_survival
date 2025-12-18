using text_survival.Environments.Features;

namespace text_survival.Environments.Factories;

public class ZoneGenerator
{
    public int TargetLocationCount { get; set; } = 100;
    public int MaxConnections { get; set; } = 4;
    public double ExtraEdgeProbability { get; set; } = 0.25;

    // Location type weights for forest zone
    private static readonly List<(Func<Zone, Location> Factory, double Weight, int MinTraversal, int MaxTraversal)> ForestLocationWeights =
    [
        (LocationFactory.MakeForest, 55.0, 8, 15),
        (LocationFactory.MakeClearing, 20.0, 5, 10),
        (LocationFactory.MakeHillside, 10.0, 12, 20),
        (LocationFactory.MakeRiverbank, 8.0, 10, 15),
        (LocationFactory.MakeCave, 5.0, 15, 25),
        (LocationFactory.MakePlain, 2.0, 10, 18)
    ];

    public Zone GenerateForestZone(string name, string description, double baseTemp = 25)
    {
        var zone = new Zone(name, description, baseTemp);

        // Create starting location
        var start = CreateStartingLocation(zone);
        zone.Graph.Add(start);

        // Track locations with available connection slots
        var availableLocations = new List<Location> { start };

        // Generate locations until we reach target
        while (zone.Graph.All.Count < TargetLocationCount)
        {
            if (availableLocations.Count == 0)
                break;

            // Pick a random location with available slots
            var parent = availableLocations[Utils.RandInt(0, availableLocations.Count - 1)];

            // Generate new location with traversal time
            var newLocation = GenerateRandomLocation(zone);
            zone.Graph.Add(newLocation);

            // Connect directly
            parent.AddBidirectionalConnection(newLocation);

            // Update availability
            if (GetConnectionCount(newLocation) < MaxConnections)
                availableLocations.Add(newLocation);
            if (GetConnectionCount(parent) >= MaxConnections)
                availableLocations.Remove(parent);
        }

        // Add extra connections for interconnectedness
        AddExtraConnections(zone);

        // Calculate distances from start
        CalculateDistances(zone, start);

        return zone;
    }

    private Location CreateStartingLocation(Zone zone)
    {
        var start = new Location("Forest Clearing", zone)
        {
            Exposure = 0.4,
            Terrain = TerrainType.Clear,
            BaseTraversalMinutes = 5
        };

        var forageFeature = new ForageFeature(1.0);
        forageFeature.AddResource(Items.ItemFactory.MakeBerry, 0.4);
        forageFeature.AddResource(Items.ItemFactory.MakeStick, 0.8);
        forageFeature.AddResource(Items.ItemFactory.MakeFirewood, 0.5);
        forageFeature.AddResource(Items.ItemFactory.MakeDryGrass, 0.6);
        forageFeature.AddResource(Items.ItemFactory.MakePlantFibers, 0.5);
        forageFeature.AddResource(Items.ItemFactory.MakeSmallStone, 0.3);
        start.Features.Add(forageFeature);

        start.Features.Add(new EnvironmentFeature(EnvironmentFeature.LocationType.Forest));

        start.Explore();
        start.DistanceFromStart = 0;
        return start;
    }

    private Location GenerateRandomLocation(Zone zone)
    {
        double totalWeight = ForestLocationWeights.Sum(w => w.Weight);
        double roll = Utils.RandDouble(0, totalWeight);

        double cumulative = 0;
        foreach (var (factory, weight, minTraversal, maxTraversal) in ForestLocationWeights)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                var location = factory(zone);
                location.BaseTraversalMinutes = Utils.RandInt(minTraversal, maxTraversal);
                return location;
            }
        }

        var fallback = LocationFactory.MakeForest(zone);
        fallback.BaseTraversalMinutes = Utils.RandInt(8, 15);
        return fallback;
    }

    private void AddExtraConnections(Zone zone)
    {
        var locations = zone.Graph.All.ToList();

        // Shuffle to avoid bias
        for (int i = locations.Count - 1; i > 0; i--)
        {
            int j = Utils.RandInt(0, i);
            (locations[i], locations[j]) = (locations[j], locations[i]);
        }

        foreach (var location in locations)
        {
            if (GetConnectionCount(location) >= MaxConnections)
                continue;

            // Find candidates not already connected
            var candidates = locations
                .Where(l => l != location
                    && GetConnectionCount(l) < MaxConnections
                    && !location.Connections.Contains(l))
                .ToList();

            foreach (var candidate in candidates)
            {
                if (GetConnectionCount(location) >= MaxConnections)
                    break;

                if (Utils.DetermineSuccess(ExtraEdgeProbability))
                {
                    location.AddBidirectionalConnection(candidate);
                }
            }
        }
    }

    private int GetConnectionCount(Location location)
    {
        return location.Connections.Count;
    }

    private void CalculateDistances(Zone zone, Location start)
    {
        var visited = new HashSet<Location>();
        var queue = new Queue<(Location Location, int Distance)>();

        queue.Enqueue((start, 0));
        visited.Add(start);

        while (queue.Count > 0)
        {
            var (current, distance) = queue.Dequeue();
            current.DistanceFromStart = distance;

            foreach (var neighbor in current.Connections)
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue((neighbor, distance + 1));
                }
            }
        }
    }
}
