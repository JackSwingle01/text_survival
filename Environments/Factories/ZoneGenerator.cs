using text_survival.Environments.Features;

namespace text_survival.Environments.Factories;

public class ZoneGenerator
{
    public int TargetLocationCount { get; set; } = 100;
    public int InitialRevealedCount { get; set; } = 1; // Just camp - player must scout to discover locations

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

        // Create starting location (camp)
        var start = CreateStartingLocation(zone);
        zone.Graph.Add(start);

        // Generate all locations first (but don't connect them yet)
        var allLocations = new List<Location>();
        while (allLocations.Count < TargetLocationCount - 1) // -1 because start is already created
        {
            var newLocation = GenerateRandomLocation(zone);
            allLocations.Add(newLocation);
        }

        // Connect a few initial locations to the starting area
        int initialToReveal = Math.Min(InitialRevealedCount - 1, allLocations.Count); // -1 for start
        for (int i = 0; i < initialToReveal; i++)
        {
            var location = allLocations[i];
            start.AddBidirectionalConnection(location);
            zone.Graph.Add(location);
            location.Explore();
        }

        // Put the rest in the unrevealed pool
        for (int i = initialToReveal; i < allLocations.Count; i++)
        {
            zone.AddUnrevealedLocation(allLocations[i]);
        }

        return zone;
    }

    private Location CreateStartingLocation(Zone zone)
    {
        var start = new Location(
            name: "Forest Camp",
            tags: "[Shaded] [Shelter]",
            parent: zone,
            traversalMinutes: 5,
            terrainHazardLevel: 0,
            windFactor: 0.4,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.8);

        // Starting location - matches Forest for abundant fuel
        var forageFeature = new ForageFeature(2.0)
            .AddSticks(3.0, 0.2, 0.6)
            .AddLogs(1.5, 1.5, 3.5)
            .AddTinder(2.0, 0.02, 0.08)
            .AddBerries(0.3, 0.05, 0.15)
            .AddPlantFiber(0.5, 0.05, 0.15);
        start.Features.Add(forageFeature);

        // Natural shelter from dense forest provides protection at camp
        start.Features.Add(new ShelterFeature(
            name: "Overhang",
            tempInsulation: 0.55,    // Fire effectiveness increases from 40% to 55%
            overheadCoverage: 0.3,   // Some protection from snow/rain
            windCoverage: 0.4        // Moderate wind protection from trees
        ));

        start.Explore();
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
}
