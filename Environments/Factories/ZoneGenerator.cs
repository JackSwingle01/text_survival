using text_survival.Environments.Features;

namespace text_survival.Environments.Factories;

public class ZoneGenerator
{
    public int TargetLocationCount { get; set; } = 100;
    public int InitialRevealedCount { get; set; } = 1; // Just camp - player must scout to discover locations

    // Location type weights for forest zone
    private static readonly List<(Func<Zone, Location> Factory, double Weight, int MinTraversal, int MaxTraversal)> ForestLocationWeights =
    [
        // Common locations
        (LocationFactory.MakeForest, 40.0, 8, 15),
        (LocationFactory.MakeClearing, 15.0, 5, 10),
        (LocationFactory.MakeHillside, 8.0, 12, 20),
        (LocationFactory.MakeRiverbank, 7.0, 10, 15),
        (LocationFactory.MakePlain, 5.0, 10, 18),

        // Moderate rarity
        (LocationFactory.MakeFrozenCreek, 5.0, 10, 16),
        (LocationFactory.MakeDeadwoodGrove, 4.0, 12, 18),
        (LocationFactory.MakeMarsh, 4.0, 15, 22),
        (LocationFactory.MakeShelteredValley, 3.0, 18, 25),
        (LocationFactory.MakeOverlook, 3.0, 15, 20),

        // Rare locations
        (LocationFactory.MakeCave, 2.0, 15, 25),
        (LocationFactory.MakeHotSpring, 1.5, 18, 28),
        (LocationFactory.MakeWolfDen, 1.5, 15, 22),
        (LocationFactory.MakeIceCrevasse, 0.5, 22, 30),
        (LocationFactory.MakeAbandonedCamp, 0.5, 12, 20),

        // Tier 1 locations
        (LocationFactory.MakeBurntStand, 4.0, 8, 14),
        (LocationFactory.MakeRockOverhang, 3.0, 10, 16),
        (LocationFactory.MakeGraniteOutcrop, 3.0, 12, 18),
        (LocationFactory.MakeMeltwaterPool, 2.0, 18, 25),

        // Tier 2 locations
        (LocationFactory.MakeAncientGrove, 2.0, 16, 24),
        (LocationFactory.MakeFlintSeam, 1.5, 18, 26),
        (LocationFactory.MakeGameTrail, 4.0, 6, 12),
        (LocationFactory.MakeDenseThicket, 3.0, 12, 18),
        (LocationFactory.MakeBoulderField, 2.5, 14, 22),
        (LocationFactory.MakeRockyRidge, 1.5, 20, 28)
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
