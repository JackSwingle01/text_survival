using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Environments.Factories;

/// <summary>
/// Generates a world map with terrain and placed locations.
/// </summary>
public class GridWorldGenerator
{
    public int Width { get; set; } = 32;
    public int Height { get; set; } = 32;
    public int TargetNamedLocations { get; set; } = 40;
    public int MinLocationSpacing { get; set; } = 3;  // Minimum tiles between named locations

    // Terrain matrix used during generation
    private TerrainType[,] _terrain = null!;

    // Location type weights (same as ZoneGenerator)
    private static readonly List<(Func<Weather, Location> Factory, double Weight)> LocationWeights =
    [
        // Common locations
        (LocationFactory.MakeForest, 40.0),
        (LocationFactory.MakeClearing, 15.0),
        (LocationFactory.MakeStoneScatter, 10.0),
        (LocationFactory.MakeHillside, 8.0),
        (LocationFactory.MakeRiverbank, 7.0),
        (LocationFactory.MakePlain, 5.0),

        // Moderate rarity
        (LocationFactory.MakeFrozenCreek, 5.0),
        (LocationFactory.MakeDeadwoodGrove, 4.0),
        (LocationFactory.MakeMarsh, 4.0),
        (LocationFactory.MakeShelteredValley, 3.0),
        (LocationFactory.MakeOverlook, 3.0),

        // Rare locations
        (LocationFactory.MakeCave, 2.0),
        (LocationFactory.MakeHotSpring, 1.5),
        (LocationFactory.MakeWolfDen, 1.5),
        (LocationFactory.MakeIceCrevasse, 0.5),
        (LocationFactory.MakeAbandonedCamp, 0.5),

        // Tier 1 locations
        (LocationFactory.MakeBurntStand, 4.0),
        (LocationFactory.MakeRockOverhang, 3.0),
        (LocationFactory.MakeGraniteOutcrop, 3.0),
        (LocationFactory.MakeMeltwaterPool, 2.0),

        // Tier 2 locations
        (LocationFactory.MakeAncientGrove, 2.0),
        (LocationFactory.MakeFlintSeam, 1.5),
        (LocationFactory.MakeGameTrail, 4.0),
        (LocationFactory.MakeDenseThicket, 3.0),
        (LocationFactory.MakeBoulderField, 2.5),
        (LocationFactory.MakeRockyRidge, 1.5),

        // Tier 3 locations
        (LocationFactory.MakeBearCave, 0.5),
        (LocationFactory.MakeBeaverDam, 1.0),

        // Tier 4 locations
        (LocationFactory.MakeTheLookout, 0.8),
        (LocationFactory.MakeOldCampsite, 0.6),

        // New natural locations
        (LocationFactory.MakePeatBog, 4.0),
        (LocationFactory.MakeIceShelf, 4.0),
        (LocationFactory.MakeBoneHollow, 3.5),
        (LocationFactory.MakeWindGap, 3.5),
        (LocationFactory.MakeSnowfieldHollow, 4.0),
        (LocationFactory.MakeSunWarmedCliff, 4.0)
    ];

    /// <summary>
    /// Generate a complete world map.
    /// Returns the map and the camp location.
    /// </summary>
    public (GameMap Map, Location Camp) Generate(Weather weather)
    {
        var map = new GameMap(Width, Height);
        map.Weather = weather;
        _terrain = new TerrainType[Width, Height];

        // Step 1: Generate base terrain types
        GenerateBaseTerrain();

        // Step 2: Add mountain range along one edge
        GenerateMountainRange();

        // Step 3: Create terrain-only locations for all positions
        InitializeTerrainLocations(map, weather);

        // Step 4: Place camp near center (replaces terrain location)
        var (campPos, camp) = PlaceCamp(map, weather);

        // Step 5: Place named locations across the map (replaces terrain locations)
        PlaceNamedLocations(map, weather, campPos);

        // Step 6: Set initial position and visibility around camp
        map.CurrentPosition = campPos;
        map.UpdateVisibility();
        camp.MarkExplored();

        return (map, camp);
    }

    /// <summary>
    /// Create terrain-only locations for all positions.
    /// Uses position-based seeds for deterministic environmental details.
    /// </summary>
    private void InitializeTerrainLocations(GameMap map, Weather weather)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var terrain = _terrain[x, y];
                // Create deterministic seed from position for environmental details
                var positionSeed = HashCode.Combine(x, y, Width);
                var location = LocationFactory.MakeTerrainLocation(terrain, weather, positionSeed);
                map.SetLocation(x, y, location);
            }
        }
    }

    /// <summary>
    /// Generate base terrain using simple noise-like distribution.
    /// </summary>
    private void GenerateBaseTerrain()
    {
        // Use a simple pseudo-random distribution based on position
        // This creates clusters of similar terrain
        var random = new Random();
        double[,] noise = GenerateSimpleNoise(Width, Height, random);

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                double value = noise[x, y];
                _terrain[x, y] = value switch
                {
                    < 0.15 => TerrainType.Water,     // Frozen lakes/rivers
                    < 0.25 => TerrainType.Marsh,    // Marshland
                    < 0.55 => TerrainType.Forest,   // Most common - snowy forest
                    < 0.70 => TerrainType.Clearing, // Open clearings
                    < 0.80 => TerrainType.Plain,    // Snowy plains
                    < 0.90 => TerrainType.Hills,    // Hilly terrain
                    _ => TerrainType.Rock           // Rocky areas
                };
            }
        }
    }

    /// <summary>
    /// Generate simple smoothed noise for terrain distribution.
    /// </summary>
    private double[,] GenerateSimpleNoise(int width, int height, Random random)
    {
        double[,] noise = new double[width, height];

        // Generate base random values
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                noise[x, y] = random.NextDouble();
            }
        }

        // Smooth the noise by averaging with neighbors (creates natural-looking clusters)
        double[,] smoothed = new double[width, height];
        for (int pass = 0; pass < 3; pass++)  // Multiple smoothing passes
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double sum = noise[x, y];
                    int count = 1;

                    // Average with neighbors
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                sum += noise[nx, ny];
                                count++;
                            }
                        }
                    }

                    smoothed[x, y] = sum / count;
                }
            }

            // Copy back for next pass
            Array.Copy(smoothed, noise, width * height);
        }

        return noise;
    }

    /// <summary>
    /// Add a mountain range along the north edge with a pass.
    /// </summary>
    private void GenerateMountainRange()
    {
        // Mountains along top edge (y = 0, 1, 2)
        int mountainDepth = 3;
        int passCenter = Width / 2;
        int passWidth = 2;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < mountainDepth; y++)
            {
                // Leave a gap for the pass
                bool isPass = Math.Abs(x - passCenter) <= passWidth / 2;
                if (!isPass)
                {
                    _terrain[x, y] = TerrainType.Mountain;
                }
                else
                {
                    // Pass terrain - rocky and hazardous
                    _terrain[x, y] = TerrainType.Rock;
                }
            }
        }
    }

    /// <summary>
    /// Place the camp near the center of the map.
    /// </summary>
    private (GridPosition CampPos, Location Camp) PlaceCamp(GameMap map, Weather weather)
    {
        int centerX = Width / 2;
        int centerY = Height / 2;

        // Find a suitable spot near center (prefer forest/clearing)
        var campPos = FindSuitablePosition(map, centerX, centerY, 5,
            terrain => terrain == TerrainType.Forest || terrain == TerrainType.Clearing);

        // Create camp location
        var camp = CreateCampLocation(weather);
        camp.Terrain = TerrainType.Clearing;

        // Place on map
        map.SetLocation(campPos.X, campPos.Y, camp);

        return (campPos, camp);
    }

    /// <summary>
    /// Create the starting camp location (same as ZoneGenerator).
    /// </summary>
    private Location CreateCampLocation(Weather weather)
    {
        var camp = new Location(
            name: "Forest Camp",
            tags: "[Shaded] [Shelter]",
            weather: weather,
            traversalMinutes: 5,
            terrainHazardLevel: 0,
            windFactor: 0.4,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.8);

        camp.Features.Add(FeatureFactory.CreateMixedForestForage(density: 1.5));
        camp.Features.Add(new ShelterFeature(
            name: "Overhang",
            tempInsulation: 0.55,
            overheadCoverage: 0.3,
            windCoverage: 0.4
        ));
        camp.Features.Add(BeddingFeature.CreateCampBedding());
        camp.MarkExplored();

        return camp;
    }

    /// <summary>
    /// Place named locations across the map.
    /// </summary>
    private void PlaceNamedLocations(GameMap map, Weather weather, GridPosition campPos)
    {
        var placedPositions = new List<GridPosition> { campPos };
        int attempts = 0;
        int maxAttempts = TargetNamedLocations * 10;

        while (placedPositions.Count <= TargetNamedLocations && attempts < maxAttempts)
        {
            attempts++;

            // Pick a random position
            int x = Utils.RandInt(0, Width - 1);
            int y = Utils.RandInt(MinLocationSpacing, Height - 1);  // Avoid mountain range

            var pos = new GridPosition(x, y);
            var location = map.GetLocationAt(pos);

            if (location == null || !location.IsPassable || !location.IsTerrainOnly)
                continue;

            // Check minimum spacing from other locations
            bool tooClose = placedPositions.Any(p => p.ManhattanDistance(pos) < MinLocationSpacing);
            if (tooClose)
                continue;

            // Generate and place a location
            var namedLocation = GenerateRandomLocation(weather);

            // Adjust terrain to match location type if needed
            AdjustTerrainForLocation(namedLocation);

            map.SetLocation(x, y, namedLocation);
            placedPositions.Add(pos);
        }
    }

    /// <summary>
    /// Set the location's terrain type to match its theme.
    /// </summary>
    private void AdjustTerrainForLocation(Location location)
    {
        // Match terrain to location theme based on name/tags
        string name = location.Name.ToLower();
        string tags = location.Tags.ToLower();

        if (name.Contains("creek") || name.Contains("river") || name.Contains("pool") || name.Contains("dam"))
            location.Terrain = TerrainType.Water;
        else if (name.Contains("marsh") || name.Contains("bog"))
            location.Terrain = TerrainType.Marsh;
        else if (name.Contains("cave") || name.Contains("crevasse") || name.Contains("boulder") || name.Contains("granite") || name.Contains("flint"))
            location.Terrain = TerrainType.Rock;
        else if (name.Contains("hill") || name.Contains("ridge") || name.Contains("overlook") || name.Contains("lookout"))
            location.Terrain = TerrainType.Hills;
        else if (name.Contains("clearing") || name.Contains("plain") || name.Contains("trail"))
            location.Terrain = TerrainType.Clearing;
        else if (tags.Contains("forest") || name.Contains("grove") || name.Contains("thicket") || name.Contains("forest"))
            location.Terrain = TerrainType.Forest;
    }

    /// <summary>
    /// Generate a random location using weighted selection.
    /// </summary>
    private Location GenerateRandomLocation(Weather weather)
    {
        double totalWeight = LocationWeights.Sum(w => w.Weight);
        double roll = Utils.RandDouble(0, totalWeight);

        double cumulative = 0;
        foreach (var (factory, weight) in LocationWeights)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return factory(weather);
            }
        }

        return LocationFactory.MakeForest(weather);
    }

    /// <summary>
    /// Find a suitable position near a target, with optional terrain predicate.
    /// </summary>
    private GridPosition FindSuitablePosition(GameMap map, int targetX, int targetY, int searchRadius,
        Func<TerrainType, bool>? predicate = null)
    {
        // Spiral outward from target
        for (int radius = 0; radius <= searchRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;  // Only check perimeter

                    int x = targetX + dx;
                    int y = targetY + dy;

                    if (x < 0 || x >= Width || y < 0 || y >= Height)
                        continue;

                    var terrain = _terrain[x, y];
                    if (terrain.IsPassable())
                    {
                        if (predicate == null || predicate(terrain))
                            return new GridPosition(x, y);
                    }
                }
            }
        }

        // Fallback to target if nothing found
        return new GridPosition(targetX, targetY);
    }
}
