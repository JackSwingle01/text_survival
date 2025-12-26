using text_survival.Environments.Features;

namespace text_survival.Environments.Factories;

public class ZoneGenerator
{
    public int TargetLocationCount { get; set; } = 100;
    public int InitialRevealedCount { get; set; } = 1; // Just camp - player must scout to discover locations

    // Location type weights for forest zone
    private static readonly List<(Func<Weather, Location> Factory, double Weight, int MinTraversal, int MaxTraversal)> ForestLocationWeights =
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
        (LocationFactory.MakeRockyRidge, 1.5, 20, 28),

        // Tier 3 locations - state machines / special features
        (LocationFactory.MakeBearCave, 0.5, 22, 30),   // Rare, dangerous, rewarding
        (LocationFactory.MakeBeaverDam, 1.0, 15, 22),  // Moderate, ecosystem resource

        // Tier 4 locations - vantage points and story locations
        (LocationFactory.MakeTheLookout, 0.8, 16, 24), // Climb risk, scouting reward
        (LocationFactory.MakeOldCampsite, 0.6, 12, 20) // Salvage site, narrative mystery
    ];

    public (List<Location> revealedLocations, List<Location> unrevealedLocations) GenerateForestZone(Weather weather)
    {
        var revealed = new List<Location>();
        var unrevealed = new List<Location>();

        // Create starting location (camp)
        var start = CreateStartingLocation(weather);
        revealed.Add(start);

        // Generate all locations first (but don't connect them yet)
        var allLocations = new List<Location>();
        while (allLocations.Count < TargetLocationCount - 1) // -1 because start is already created
        {
            var newLocation = GenerateRandomLocation(weather);
            allLocations.Add(newLocation);
        }

        // Connect a few initial locations to the starting area
        int initialToReveal = Math.Min(InitialRevealedCount - 1, allLocations.Count); // -1 for start
        for (int i = 0; i < initialToReveal; i++)
        {
            var location = allLocations[i];
            start.AddBidirectionalConnection(location);
            revealed.Add(location);
            location.Explore();
        }

        // Put the rest in the unrevealed pool
        for (int i = initialToReveal; i < allLocations.Count; i++)
        {
            unrevealed.Add(allLocations[i]);
        }

        return (revealed, unrevealed);
    }

    private Location CreateStartingLocation(Weather weather)
    {
        var start = new Location(
            name: "Forest Camp",
            tags: "[Shaded] [Shelter]",
            weather: weather,
            traversalMinutes: 5,
            terrainHazardLevel: 0,
            windFactor: 0.4,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.8);

        // Starting location - matches Forest for abundant fuel
        start.Features.Add(FeatureFactory.CreateMixedForestForage(density: 1.5));

        // Natural shelter from dense forest provides protection at camp
        start.Features.Add(new ShelterFeature(
            name: "Overhang",
            tempInsulation: 0.55,    // Fire effectiveness increases from 40% to 55%
            overheadCoverage: 0.3,   // Some protection from snow/rain
            windCoverage: 0.4        // Moderate wind protection from trees
        ));

        // Bedding for sleeping at camp
        start.Features.Add(BeddingFeature.CreateCampBedding());

        start.Explore();
        return start;
    }

    private Location GenerateRandomLocation(Weather weather)
    {
        double totalWeight = ForestLocationWeights.Sum(w => w.Weight);
        double roll = Utils.RandDouble(0, totalWeight);

        double cumulative = 0;
        foreach (var (factory, weight, minTraversal, maxTraversal) in ForestLocationWeights)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                var location = factory(weather);
                location.BaseTraversalMinutes = Utils.RandInt(minTraversal, maxTraversal);
                return location;
            }
        }

        var fallback = LocationFactory.MakeForest(weather);
        fallback.BaseTraversalMinutes = Utils.RandInt(8, 15);
        return fallback;
    }

    /// <summary>
    /// Generate the mountain pass chain - linear progression from connectTo to far side.
    /// Returns list of pass locations in order (approach -> proper -> far side).
    /// </summary>
    public static List<Location> GenerateMountainPass(Weather weather, Location connectTo)
    {
        var approach = LocationFactory.MakePassApproach(weather);
        var lower = LocationFactory.MakeLowerPass(weather);
        var proper = LocationFactory.MakePassProper(weather);
        var upperDescent = LocationFactory.MakeUpperDescent(weather);
        var lowerDescent = LocationFactory.MakeLowerDescent(weather);
        var farSide = LocationFactory.MakeFarSide(weather);

        // Chain the pass locations
        connectTo.AddBidirectionalConnection(approach);
        approach.AddBidirectionalConnection(lower);
        lower.AddBidirectionalConnection(proper);
        proper.AddBidirectionalConnection(upperDescent);
        upperDescent.AddBidirectionalConnection(lowerDescent);
        lowerDescent.AddBidirectionalConnection(farSide);

        return [approach, lower, proper, upperDescent, lowerDescent, farSide];
    }
}
