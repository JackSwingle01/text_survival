using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Environments.Factories;

/// <summary>
/// Generates a world map with terrain and placed locations.
/// Uses a layered generation algorithm for natural terrain distribution.
/// </summary>
public class GridWorldGenerator
{
    public int Width { get; set; } = 16;
    public int Height { get; set; } = 16;
    public int TargetNamedLocations { get; set; } = 40;
    public int MinLocationSpacing { get; set; } = 2;  // Minimum tiles between named locations
    public int MountainRows { get; set; } = 3;

    // Terrain matrix used during generation
    private TerrainType[,] _terrain = null!;
    private Random _rng = null!;

    // Cluster shapes for terrain feature placement
    private static readonly List<(int dx, int dy)[]> SmallShapes =
    [
        [(0, 0)],                                    // single
        [(0, 0), (1, 0)],                            // duo_h
        [(0, 0), (0, 1)],                            // duo_v
    ];

    private static readonly List<(int dx, int dy)[]> MediumShapes =
    [
        [(0, 0)],                                    // single
        [(0, 0), (1, 0)],                            // duo_h
        [(0, 0), (0, 1)],                            // duo_v
        [(0, 0), (1, 0), (0, 1)],                    // trio_l
        [(0, 0), (1, 0), (2, 0)],                    // trio_line
    ];

    private static readonly List<(int dx, int dy)[]> LargeShapes =
    [
        [(0, 0), (1, 0), (0, 1)],                    // trio_l
        [(0, 0), (1, 0), (2, 0)],                    // trio_line
        [(0, 0), (1, 0), (0, 1), (1, 1)],            // quad_square
        [(0, 0), (1, 0), (2, 0), (1, 1)],            // quad_t
    ];

    // Location type weights with preferred terrain types
    // null terrain means location can be placed anywhere
    private static readonly List<(Func<Weather, Location> Factory, double Weight, TerrainType[]? PreferredTerrain)> LocationWeights =
    [
        // Forest locations
        (LocationFactory.MakeForest, 40.0, [TerrainType.Forest]),
        (LocationFactory.MakeDeadwoodGrove, 4.0, [TerrainType.Forest]),
        (LocationFactory.MakeAncientGrove, 2.0, [TerrainType.Forest]),
        (LocationFactory.MakeDenseThicket, 3.0, [TerrainType.Forest]),
        (LocationFactory.MakeWolfDen, 1.5, [TerrainType.Forest]),
        (LocationFactory.MakeBurntStand, 4.0, [TerrainType.Forest]),

        // Clearing/Plain locations
        (LocationFactory.MakeClearing, 15.0, [TerrainType.Clearing, TerrainType.Plain]),
        (LocationFactory.MakePlain, 5.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeGameTrail, 4.0, [TerrainType.Clearing, TerrainType.Plain]),
        (LocationFactory.MakeShelteredValley, 3.0, [TerrainType.Clearing, TerrainType.Forest]),
        (LocationFactory.MakeSnowfieldHollow, 4.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeAbandonedCamp, 0.5, [TerrainType.Clearing, TerrainType.Forest]),
        (LocationFactory.MakeOldCampsite, 0.6, [TerrainType.Clearing, TerrainType.Forest]),

        // Rock/Hills locations
        (LocationFactory.MakeStoneScatter, 10.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeHillside, 8.0, [TerrainType.Hills]),
        (LocationFactory.MakeOverlook, 3.0, [TerrainType.Hills, TerrainType.Rock]),
        (LocationFactory.MakeCave, 2.0, [TerrainType.Rock]),
        (LocationFactory.MakeRockOverhang, 3.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeGraniteOutcrop, 3.0, [TerrainType.Rock]),
        (LocationFactory.MakeFlintSeam, 1.5, [TerrainType.Rock]),
        (LocationFactory.MakeBoulderField, 2.5, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeRockyRidge, 1.5, [TerrainType.Hills, TerrainType.Rock]),
        (LocationFactory.MakeBearCave, 0.5, [TerrainType.Rock]),
        (LocationFactory.MakeTheLookout, 0.8, [TerrainType.Hills, TerrainType.Rock]),
        (LocationFactory.MakeIceCrevasse, 0.5, [TerrainType.Rock]),
        (LocationFactory.MakeBoneHollow, 3.5, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeWindGap, 3.5, [TerrainType.Hills]),
        (LocationFactory.MakeSunWarmedCliff, 4.0, [TerrainType.Rock, TerrainType.Hills]),

        // Water locations
        (LocationFactory.MakeRiverbank, 7.0, [TerrainType.Water]),
        (LocationFactory.MakeFrozenCreek, 5.0, [TerrainType.Water]),
        (LocationFactory.MakeMeltwaterPool, 2.0, [TerrainType.Water]),
        (LocationFactory.MakeBeaverDam, 1.0, [TerrainType.Water]),
        (LocationFactory.MakeIceShelf, 4.0, [TerrainType.Water]),
        (LocationFactory.MakeHotSpring, 1.5, [TerrainType.Water, TerrainType.Rock]),

        // Marsh locations
        (LocationFactory.MakeMarsh, 4.0, [TerrainType.Marsh]),
        (LocationFactory.MakePeatBog, 4.0, [TerrainType.Marsh])
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
        _rng = new Random();

        // Step 1: Generate layered terrain
        GenerateLayeredTerrain();

        // Step 2: Add mountain range along north edge
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
    /// Generate terrain using layered algorithm for natural distribution.
    /// Layer 1: Base (Forest/Plain via octave noise)
    /// Layer 2: Rock (scattered singles)
    /// Layer 3: Clearings (clusters in forest)
    /// Layer 4: Hills (clusters in plains)
    /// Layer 5: Water (small clusters)
    /// Layer 6: Marsh (expands from water edges)
    /// Rerolls if any terrain type has fewer than 10 tiles.
    /// </summary>
    private void GenerateLayeredTerrain()
    {
        const int minTilesPerTerrain = 10;
        const int maxAttempts = 20;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int seed = _rng.Next();

            // Layer 1: Base terrain - Forest/Plain split using octave noise
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    double noise = OctaveNoise(x, y, seed, scale: 8);
                    _terrain[x, y] = noise > 0.5 ? TerrainType.Forest : TerrainType.Plain;
                }
            }

            // Layer 2: Rock - scattered single tiles
            int rockCount = _rng.Next(25, 41);
            for (int i = 0; i < rockCount; i++)
            {
                var (x, y) = RandomPosition(avoidMountainRows: true);
                if (_terrain[x, y] == TerrainType.Forest || _terrain[x, y] == TerrainType.Plain)
                {
                    _terrain[x, y] = TerrainType.Rock;
                }
            }

            // Layer 3: Clearings - clusters placed in forest
            int clearingClusters = _rng.Next(12, 21);
            PlaceClusters(clearingClusters, TerrainType.Clearing, TerrainType.Forest, MediumShapes);

            // Layer 4: Hills - clusters placed in plains
            int hillClusters = _rng.Next(10, 17);
            PlaceClusters(hillClusters, TerrainType.Hills, TerrainType.Plain, MediumShapes);

            // Layer 5: Water - small clusters scattered
            int waterFeatures = _rng.Next(10, 17);
            PlaceClusters(waterFeatures, TerrainType.Water, null, SmallShapes,
                allowedBase: [TerrainType.Forest, TerrainType.Plain, TerrainType.Clearing]);

            // Layer 6: Marsh - expand from water edges
            ExpandMarshFromWater();

            // Validate terrain distribution
            if (ValidateTerrainCounts(minTilesPerTerrain))
                return; // Success - terrain is valid
        }

        // If we get here after max attempts, just use whatever we have
    }

    /// <summary>
    /// Check that all passable terrain types have at least the minimum tile count.
    /// </summary>
    private bool ValidateTerrainCounts(int minCount)
    {
        var counts = new Dictionary<TerrainType, int>();

        // Count tiles per terrain type (excluding mountain rows)
        for (int x = 0; x < Width; x++)
        {
            for (int y = MountainRows; y < Height; y++)
            {
                var terrain = _terrain[x, y];
                if (!counts.ContainsKey(terrain))
                    counts[terrain] = 0;
                counts[terrain]++;
            }
        }

        // Check passable terrain types have minimum count
        var requiredTypes = new[] {
            TerrainType.Forest, TerrainType.Plain, TerrainType.Clearing,
            TerrainType.Hills, TerrainType.Rock, TerrainType.Water, TerrainType.Marsh
        };

        foreach (var type in requiredTypes)
        {
            if (!counts.TryGetValue(type, out int count) || count < minCount)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Place clusters of a terrain type on valid base terrain.
    /// </summary>
    private void PlaceClusters(int count, TerrainType placeType, TerrainType? requiredBase,
        List<(int dx, int dy)[]> shapePool, TerrainType[]? allowedBase = null)
    {
        for (int i = 0; i < count; i++)
        {
            var (x, y) = RandomPosition(avoidMountainRows: true);

            // Check base terrain requirement
            if (requiredBase.HasValue && _terrain[x, y] != requiredBase.Value)
                continue;
            if (allowedBase != null && !allowedBase.Contains(_terrain[x, y]))
                continue;

            // Pick and transform a random shape
            var shape = shapePool[_rng.Next(shapePool.Count)];
            shape = RotateAndFlip(shape);

            // Verify all tiles in shape are valid
            bool valid = true;
            foreach (var (dx, dy) in shape)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= Width || ny < MountainRows || ny >= Height)
                {
                    valid = false;
                    break;
                }
                if (requiredBase.HasValue && _terrain[nx, ny] != requiredBase.Value)
                {
                    valid = false;
                    break;
                }
                if (allowedBase != null && !allowedBase.Contains(_terrain[nx, ny]))
                {
                    valid = false;
                    break;
                }
            }

            if (!valid) continue;

            // Place the cluster
            foreach (var (dx, dy) in shape)
            {
                _terrain[x + dx, y + dy] = placeType;
            }
        }
    }

    /// <summary>
    /// Expand marsh terrain from water edges.
    /// </summary>
    private void ExpandMarshFromWater()
    {
        var marshCandidates = new List<(int x, int y)>();

        // Find tiles adjacent to water
        for (int x = 0; x < Width; x++)
        {
            for (int y = MountainRows; y < Height; y++)
            {
                if (_terrain[x, y] != TerrainType.Forest &&
                    _terrain[x, y] != TerrainType.Plain &&
                    _terrain[x, y] != TerrainType.Clearing)
                    continue;

                // Check if adjacent to water
                bool adjacentToWater = false;
                foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < Width && ny >= 0 && ny < Height &&
                        _terrain[nx, ny] == TerrainType.Water)
                    {
                        adjacentToWater = true;
                        break;
                    }
                }

                if (adjacentToWater)
                    marshCandidates.Add((x, y));
            }
        }

        // Convert ~50% of candidates to marsh
        foreach (var (x, y) in marshCandidates)
        {
            if (_rng.NextDouble() < 0.5)
            {
                _terrain[x, y] = TerrainType.Marsh;
            }
        }
    }

    /// <summary>
    /// Get a random position on the grid.
    /// </summary>
    private (int x, int y) RandomPosition(bool avoidMountainRows = false)
    {
        int x = _rng.Next(0, Width);
        int minY = avoidMountainRows ? MountainRows : 0;
        int y = _rng.Next(minY, Height);
        return (x, y);
    }

    /// <summary>
    /// Rotate and flip a shape randomly.
    /// </summary>
    private (int dx, int dy)[] RotateAndFlip((int dx, int dy)[] shape)
    {
        int rotations = _rng.Next(0, 4);
        bool flip = _rng.Next(0, 2) == 1;

        var result = new (int dx, int dy)[shape.Length];
        for (int i = 0; i < shape.Length; i++)
        {
            var (dx, dy) = shape[i];

            // Apply rotations (90° each)
            for (int r = 0; r < rotations; r++)
            {
                (dx, dy) = (-dy, dx);
            }

            // Apply horizontal flip
            if (flip)
            {
                dx = -dx;
            }

            result[i] = (dx, dy);
        }

        return result;
    }

    /// <summary>
    /// Generate octave noise value for a position.
    /// Uses 3 octaves with bilinear interpolation.
    /// </summary>
    private double OctaveNoise(int x, int y, int seed, double scale)
    {
        double value = 0;
        double amplitude = 1;
        double frequency = 1;
        double maxValue = 0;

        for (int octave = 0; octave < 3; octave++)
        {
            double sampleX = x * frequency / scale;
            double sampleY = y * frequency / scale;

            double noise = InterpolatedNoise(sampleX, sampleY, seed + octave * 1000);

            value += noise * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }

        return value / maxValue;
    }

    /// <summary>
    /// Bilinear interpolation of hash-based noise.
    /// </summary>
    private double InterpolatedNoise(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        double fx = x - x0;
        double fy = y - y0;

        // Smoothstep for smoother interpolation
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);

        double n00 = HashNoise(x0, y0, seed);
        double n10 = HashNoise(x1, y0, seed);
        double n01 = HashNoise(x0, y1, seed);
        double n11 = HashNoise(x1, y1, seed);

        double nx0 = n00 + fx * (n10 - n00);
        double nx1 = n01 + fx * (n11 - n01);

        return nx0 + fy * (nx1 - nx0);
    }

    /// <summary>
    /// Hash function to generate pseudo-random value from coordinates.
    /// </summary>
    private static double HashNoise(int x, int y, int seed)
    {
        int n = x + y * 57 + seed * 131;
        n = (n << 13) ^ n;
        int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
        return 1.0 - m / 1073741824.0;
    }

    /// <summary>
    /// Add a mountain range along the north edge with a randomized pass.
    /// </summary>
    private void GenerateMountainRange()
    {
        // Randomize pass position (keep it somewhat central)
        int passStart = _rng.Next(Width / 4, Width * 3 / 4);
        int passWidth = 2;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < MountainRows; y++)
            {
                // Leave a gap for the pass
                bool isPass = x >= passStart && x < passStart + passWidth;
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
            terrainHazardLevel: 0,
            windFactor: 0.4,
            overheadCoverLevel: 0.3,
            visibilityFactor: 0.8)
        {
            Terrain = TerrainType.Forest
        };

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
    /// Place named locations across the map using terrain-aware selection.
    /// Locations are matched to their preferred terrain types.
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
            int y = Utils.RandInt(MountainRows, Height - 1);  // Avoid mountain range

            var pos = new GridPosition(x, y);
            var location = map.GetLocationAt(pos);

            if (location == null || !location.IsPassable || !location.IsTerrainOnly)
                continue;

            // Check minimum spacing from other locations
            bool tooClose = placedPositions.Any(p => p.ManhattanDistance(pos) < MinLocationSpacing);
            if (tooClose)
                continue;

            // Get the terrain at this position and generate a matching location
            var terrain = _terrain[x, y];
            var namedLocation = GenerateLocationForTerrain(weather, terrain);

            if (namedLocation == null)
                continue; // No suitable location for this terrain

            // Set the location's terrain type to match
            namedLocation.Terrain = terrain;

            map.SetLocation(x, y, namedLocation);
            placedPositions.Add(pos);
        }
    }

    /// <summary>
    /// Generate a location that matches the given terrain type.
    /// Uses weighted selection filtered to locations that prefer this terrain.
    /// </summary>
    private Location? GenerateLocationForTerrain(Weather weather, TerrainType terrain)
    {
        // Filter to locations that prefer this terrain
        var validLocations = LocationWeights
            .Where(l => l.PreferredTerrain != null && l.PreferredTerrain.Contains(terrain))
            .ToList();

        if (validLocations.Count == 0)
            return null;

        double totalWeight = validLocations.Sum(w => w.Weight);
        double roll = Utils.RandDouble(0, totalWeight);

        double cumulative = 0;
        foreach (var (factory, weight, _) in validLocations)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return factory(weather);
            }
        }

        // Fallback to first valid location
        return validLocations[0].Factory(weather);
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
