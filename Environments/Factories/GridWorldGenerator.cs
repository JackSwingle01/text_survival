using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Environments.Factories;

/// <summary>
/// Generates a world map with terrain and placed locations.
/// Uses a layered generation algorithm for natural terrain distribution.
/// </summary>
public class GridWorldGenerator
{
    public int Width { get; set; } = 224;
    public int Height { get; set; } = 82;
    public int TargetNamedLocations { get; set; } = 300;
    public int MinLocationSpacing { get; set; } = 10;  // Minimum tiles between named locations
    // Retained for callers configuring older generators; geography no longer reserves northern rows.
    public int MountainRows { get; set; } = 18;
    private ValleyLayout _layout = null!;
    private List<GridPosition> _land = [];
    private double _terrainDensity;
    private HashSet<GridPosition> _paintable = [];

    // Terrain matrix used during generation
    private TerrainType[,] _terrain = null!;
    private Random _rng = null!;

    // Positions adjacent to rivers (for adding WaterFeature)
    private HashSet<GridPosition> _riverAdjacentPositions = new();


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

    // Elite locations that should only spawn in outer 20% of map
    private static readonly HashSet<Func<Weather, Location>> EliteLocationFactories =
    [
        LocationFactory.MakeOldCampsite,
        LocationFactory.MakeBearCave,
        LocationFactory.MakeTheLookout,
        LocationFactory.MakeRockShelter,
        LocationFactory.MakeAncientGrove
    ];

    // Location type weights with preferred terrain types
    // null terrain means location can be placed anywhere
    private static readonly List<(Func<Weather, Location> Factory, double Weight, TerrainType[]? PreferredTerrain)> LocationWeights =
    [
        // Forest locations (rebalanced - was 40.0 for MakeForest alone)
        (LocationFactory.MakeForest, 8.0, [TerrainType.Forest]),
        (LocationFactory.MakeDeadwoodGrove, 4.0, [TerrainType.Forest]),
        (LocationFactory.MakeAncientGrove, 2.0, [TerrainType.Forest]),
        (LocationFactory.MakeDenseThicket, 3.0, [TerrainType.Forest]),
        (LocationFactory.MakeWolfDen, 1.5, [TerrainType.Forest]),
        (LocationFactory.MakeBurntStand, 4.0, [TerrainType.Forest]),
        // New forest locations
        (LocationFactory.MakeFallenGiant, 6.0, [TerrainType.Forest]),
        (LocationFactory.MakeHollowOak, 5.0, [TerrainType.Forest]),
        (LocationFactory.MakeFungalGrove, 4.0, [TerrainType.Forest]),
        (LocationFactory.MakeBirchStand, 5.0, [TerrainType.Forest]),
        (LocationFactory.MakeMossyHollow, 4.0, [TerrainType.Forest]),
        (LocationFactory.MakeTangledRoots, 3.0, [TerrainType.Forest]),

        // Clearing/Plain locations (rebalanced)
        (LocationFactory.MakeClearing, 6.0, [TerrainType.Clearing]),
        (LocationFactory.MakePlain, 5.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeGameTrail, 4.0, [TerrainType.Clearing, TerrainType.Plain]),
        (LocationFactory.MakeShelteredValley, 3.0, [TerrainType.Clearing, TerrainType.Forest]),
        (LocationFactory.MakeSnowfieldHollow, 4.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeAbandonedCamp, 0.5, [TerrainType.Clearing, TerrainType.Forest]),
        (LocationFactory.MakeOldCampsite, 0.6, [TerrainType.Clearing, TerrainType.Forest]),
        // New plains/clearing locations
        (LocationFactory.MakeSaltLick, 3.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeTallGrass, 4.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeStandingStones, 2.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeHerdCrossing, 3.0, [TerrainType.Plain, TerrainType.Clearing]),

        // Rock/Hills locations (rebalanced)
        (LocationFactory.MakeStoneScatter, 5.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeHillside, 5.0, [TerrainType.Hills]),
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
        // New rock/hills locations
        (LocationFactory.MakeTalusSlope, 4.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeSplitRock, 3.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeFossilBed, 2.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeShaleOutcrop, 3.0, [TerrainType.Rock]),
        (LocationFactory.MakeChimneyRock, 2.0, [TerrainType.Rock, TerrainType.Hills]),

        // Water locations
        (LocationFactory.MakeRiverbank, 7.0, [TerrainType.Water]),
        (LocationFactory.MakeFrozenCreek, 5.0, [TerrainType.Water]),
        (LocationFactory.MakeMeltwaterPool, 2.0, [TerrainType.Water]),
        (LocationFactory.MakeBeaverDam, 1.0, [TerrainType.Water]),
        (LocationFactory.MakeIceShelf, 4.0, [TerrainType.Water]),
        (LocationFactory.MakeHotSpring, 1.5, [TerrainType.Water, TerrainType.Rock]),
        // New water locations
        (LocationFactory.MakeSpringSeep, 3.0, [TerrainType.Water]),
        (LocationFactory.MakeFishRun, 2.0, [TerrainType.Water]),

        // Marsh locations
        (LocationFactory.MakeMarsh, 4.0, [TerrainType.Marsh]),
        (LocationFactory.MakePeatBog, 4.0, [TerrainType.Marsh]),
        // New marsh locations
        (LocationFactory.MakeReedBed, 4.0, [TerrainType.Marsh]),
        (LocationFactory.MakeCranberryBog, 3.0, [TerrainType.Marsh]),

        // Animal-focused locations (new)
        (LocationFactory.MakeRavensPerch, 2.0, [TerrainType.Forest, TerrainType.Clearing]),
        (LocationFactory.MakeFoxEarth, 2.0, [TerrainType.Forest]),
        (LocationFactory.MakeOwlHollow, 2.0, [TerrainType.Forest]),
        (LocationFactory.MakeEaglesCrag, 1.5, [TerrainType.Rock, TerrainType.Hills]),

        // Design doc locations
        (LocationFactory.MakeCreekFalls, 2.5, [TerrainType.Water]),
        (LocationFactory.MakeOpenPines, 5.0, [TerrainType.Forest]),
        (LocationFactory.MakeYoungGrowth, 3.0, [TerrainType.Forest]),
        (LocationFactory.MakeCliffFace, 2.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeRootHollow, 2.5, [TerrainType.Forest]),
        (LocationFactory.MakeDeerMeadow, 3.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeRabbitWarren, 3.0, [TerrainType.Forest, TerrainType.Clearing]),

        // Batch 3: Ready-now locations using existing features
        // Water
        (LocationFactory.MakeIceShoveRidge, 2.5, [TerrainType.Water]),
        (LocationFactory.MakeOverflowIce, 2.0, [TerrainType.Water]),
        (LocationFactory.MakeMineralSpring, 1.5, [TerrainType.Water, TerrainType.Rock]),
        (LocationFactory.MakeSinkholePool, 1.5, [TerrainType.Water]),
        // Elevation
        (LocationFactory.MakeSnowfieldBasin, 3.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeMoraineField, 2.5, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeScreeChute, 2.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeKrummholzZone, 2.5, [TerrainType.Forest, TerrainType.Hills]),
        // Human traces
        (LocationFactory.MakeFlintKnappingSite, 1.5, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeKillSite, 1.0, [TerrainType.Plain, TerrainType.Clearing]),
        (LocationFactory.MakeRockShelter, 2.0, [TerrainType.Rock]),
        (LocationFactory.MakeCairnMarker, 2.0, [TerrainType.Hills, TerrainType.Rock]),
        // Megafauna
        (LocationFactory.MakeMammothWallow, 1.5, [TerrainType.Plain, TerrainType.Marsh]),
        // Resource
        (LocationFactory.MakePyriteOutcrop, 2.0, [TerrainType.Rock, TerrainType.Hills]),
        (LocationFactory.MakeGlacialTongue, 1.0, [TerrainType.Rock]),
        // Unique
        (LocationFactory.MakeDeadfallMaze, 2.0, [TerrainType.Forest]),
        (LocationFactory.MakeSmokeTree, 0.8, [TerrainType.Forest]),
        (LocationFactory.MakeThermalVent, 1.0, [TerrainType.Rock, TerrainType.Hills])
    ];

    /// <summary>
    /// Get the count of unique location types that can be generated.
    /// Used by Discovery Log to determine total discoverable locations.
    /// </summary>
    public static int GetUniqueLocationCount()
    {
        // Count distinct factory methods in LocationWeights
        return LocationWeights.Select(w => w.Factory).Distinct().Count();
    }

    /// <summary>
    /// Generate a complete world map.
    /// Returns the map and the camp location.
    /// </summary>
    /// <summary>
    /// Generate a world. Pass <paramref name="seed"/> for a reproducible layout (tests, the
    /// NPC simulation harness); omit it for normal gameplay.
    /// </summary>
    public (GameMap Map, Location Camp) Generate(Weather weather, int? seed = null)
    {
        var map = new GameMap(Width, Height);
        map.Weather = weather;
        _terrain = new TerrainType[Width, Height];
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();

        // Build connectivity and barriers first; the biome pass can only paint open land.
        _layout = new ValleyLayout(Width, Height, _rng);
        _terrain = _layout.Terrain;
        _land = _layout.Reachable.Where(p => _layout.Structures[p.X, p.Y] == TileStructure.None
            && !_layout.Pass.Contains(p) && !_layout.WaterCrossings.Contains(p)).ToList();
        _paintable = _land.ToHashSet();
        _terrainDensity = _land.Count / (96.0 * (96 - 18));
        GenerateLayeredTerrain();
        _riverAdjacentPositions.Clear();
        foreach (var p in _layout.Reachable)
            if (p.GetCardinalNeighbors().Any(n => map.IsInBounds(n.X, n.Y)
                && _terrain[n.X, n.Y] == TerrainType.DeepWater)) _riverAdjacentPositions.Add(p);

        // Step 4: Create terrain-only locations for all positions
        InitializeTerrainLocations(map, weather);
        foreach (var (positions, type) in _layout.Barriers)
            if (map.GetLocationAt(positions.A)?.IsPassable == true && map.GetLocationAt(positions.B)?.IsPassable == true)
                map.AddEdge(positions.A, positions.B, new TileEdge(type) { Bidirectional = true, Impassable = true });

        // Step 5: Place camp in the western starting basin (replaces terrain location)
        var (campPos, camp) = PlaceCamp(map, weather);

        // Step 6: Place named locations across the map (replaces terrain locations)
        PlaceNamedLocations(map, weather, campPos);

        // Step 7: Name the stages of the crossing along the pass corridor
        PlacePassLocations(map, weather);

        // Step 8: Set initial position and visibility around camp
        map.CurrentPosition = campPos;
        map.UpdateVisibility();
        camp.MarkExplored();

        return (map, camp);
    }

    /// <summary>
    /// Create terrain-only locations for all positions.
    /// Uses position-based seeds for deterministic environmental details.
    /// Adds WaterFeature to tiles adjacent to rivers.
    /// </summary>
    private void InitializeTerrainLocations(GameMap map, Weather weather)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var terrain = _terrain[x, y];
                // Create deterministic seed from position for environmental details
                var positionSeed = unchecked(x * 374761393 + y * 668265263 + Width * 1274126177);
                var structure = _layout.Structures[x, y];
                var location = structure is TileStructure.CaveFloor or TileStructure.CaveEntrance
                    ? LocationFactory.MakeCaveTile(weather, structure == TileStructure.CaveEntrance)
                    : LocationFactory.MakeTerrainLocation(terrain, weather, positionSeed);
                location.Structure = structure;
                location.CaveId = _layout.CaveIds[x, y];

                // Add river water access to adjacent tiles (not water tiles - they have their own water)
                var pos = new GridPosition(x, y);
                if (_riverAdjacentPositions.Contains(pos) && terrain != TerrainType.Water)
                {
                    var riverAccess = new WaterFeature("river", "River")
                        .WithDescription("A river flows past here.")
                        .AsThinIce()  // Rivers don't freeze solid
                        .WithFishAbundance(0.5);
                    location.Features.Add(riverAccess);
                }

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
    /// Rerolls sparse distributions using a minimum scaled to usable land.
    /// </summary>
    private void GenerateLayeredTerrain()
    {
        int minTilesPerTerrain = Math.Max(1, (int)Math.Round(40 * _terrainDensity));
        const int maxAttempts = 20;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int seed = _rng.Next();

            // Layer 1: Base terrain - Forest/Plain split using octave noise
            // Two-pass approach: collect noise values, find median, then apply threshold
            var noiseGrid = new double[Width, Height];
            var noiseValues = new List<double>();

            // First pass: generate noise values
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    double noise = OctaveNoise(x, y, seed, scale: 16);
                    noiseGrid[x, y] = noise;
                    if (_paintable.Contains(new GridPosition(x, y)))
                        noiseValues.Add(noise);
                }
            }

            // Find median to guarantee 50/50 split
            noiseValues.Sort();
            double median = noiseValues[noiseValues.Count / 2];

            // Second pass: apply threshold using median
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (_paintable.Contains(new GridPosition(x, y)))
                        _terrain[x, y] = noiseGrid[x, y] > median ? TerrainType.Forest : TerrainType.Plain;
                }
            }

            // Layer 2: Rock - scattered single tiles
            int rockCount = Math.Max(1, (int)Math.Round(_rng.Next(224, 372) * _terrainDensity));
            for (int i = 0; i < rockCount; i++)
            {
                var (x, y) = RandomPosition();
                if (_terrain[x, y] == TerrainType.Forest || _terrain[x, y] == TerrainType.Plain)
                {
                    _terrain[x, y] = TerrainType.Rock;
                }
            }

            // Layer 3: Clearings - clusters placed in forest
            int clearingClusters = Math.Max(1, (int)Math.Round(_rng.Next(108, 192) * _terrainDensity));
            PlaceClusters(clearingClusters, TerrainType.Clearing, TerrainType.Forest, MediumShapes);

            // Layer 4: Hills - clusters placed in plains
            int hillClusters = Math.Max(1, (int)Math.Round(_rng.Next(92, 156) * _terrainDensity));
            PlaceClusters(hillClusters, TerrainType.Hills, TerrainType.Plain, MediumShapes);

            // Layer 5: Water - small clusters scattered
            int waterFeatures = Math.Max(1, (int)Math.Round(_rng.Next(92, 156) * _terrainDensity));
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

        // Count the land painted by the biome pass.
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (!_paintable.Contains(new GridPosition(x, y))) continue;
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
            var (x, y) = RandomPosition();

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
                if (nx < 0 || nx >= Width || ny < 0 || ny >= Height || !_paintable.Contains(new GridPosition(nx, ny)))
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
            for (int y = 0; y < Height; y++)
            {
                if (!_paintable.Contains(new GridPosition(x, y))) continue;
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
                        _terrain[nx, ny] is TerrainType.Water or TerrainType.DeepWater)
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
    private (int x, int y) RandomPosition()
    {
        var p = _land[_rng.Next(_land.Count)];
        return (p.X, p.Y);
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
    /// Place the camp in the western starting basin.
    /// </summary>
    private (GridPosition CampPos, Location Camp) PlaceCamp(GameMap map, Weather weather)
    {
        var campPos = _layout.Camp;

        // Create camp location
        var camp = CreateCampLocation(weather);

        // Keep water access when camp replaces a riverbank tile.
        camp.Features.AddRange(map.GetLocationAt(campPos)!.Features.OfType<WaterFeature>());
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

        camp.Features.Add(FeatureFactory.CreateMixedForestForage(density: 0.38));
        camp.Features.Add(new ShelterFeature(
            name: "Overhang",
            type: ShelterType.NaturalShelter,
            tempInsulation: 0.55,
            overheadCoverage: 0.3,
            windCoverage: 0.4,
            insulationCap: 0.70,
            overheadCap: 0.50,
            windCap: 0.55
        ));
        camp.Features.Add(BeddingFeature.CreateCampBedding());
        camp.MarkExplored();

        return camp;
    }

    /// <summary>The final eastern corridor retains the six-stage mountain crossing.</summary>
    private void PlacePassLocations(GameMap map, Weather weather)
    {
        Func<Weather, Location>[] factories =
        [
            LocationFactory.MakePassApproach, LocationFactory.MakeLowerPass,
            LocationFactory.MakePassProper, LocationFactory.MakeUpperDescent,
            LocationFactory.MakeLowerDescent, LocationFactory.MakeFarSide
        ];
        for (int j = 0; j < factories.Length; j++)
        {
            var p = _layout.Pass[j * (_layout.Pass.Count - 1) / (factories.Length - 1)];
            var location = factories[j](weather);
            location.Terrain = TerrainType.Rock;
            map.SetLocation(p.X, p.Y, location);
        }
    }

    /// <summary>
    /// Place named locations across the map using terrain-aware selection.
    /// Locations are matched to their preferred terrain types.
    /// Elite locations spawn farther east, away from the starting camp.
    /// </summary>
    private void PlaceNamedLocations(GameMap map, Weather weather, GridPosition campPos)
    {
        var placedPositions = new List<GridPosition> { campPos };
        int attempts = 0;
        int maxAttempts = TargetNamedLocations * 10;

        double minEliteDistance = Width * 0.65;  // ~21.5 tiles

        while (placedPositions.Count <= TargetNamedLocations && attempts < maxAttempts)
        {
            attempts++;

            // Pick a random position
            var candidate = _land[_rng.Next(_land.Count)];
            int x = candidate.X;
            int y = candidate.Y;

            var pos = new GridPosition(x, y);
            var location = map.GetLocationAt(pos);

            if (location == null || !location.IsPassable || !location.IsTerrainOnly)
                continue;

            // Check minimum spacing from other locations
            bool tooClose = placedPositions.Any(p => p.ManhattanDistance(pos) < MinLocationSpacing);
            if (tooClose)
                continue;

            // Calculate distance from camp and determine if elite locations allowed
            double distanceFromCamp = pos.DistanceTo(campPos);
            bool allowElite = distanceFromCamp >= minEliteDistance;

            // Get the terrain at this position and generate a matching location
            var terrain = _terrain[x, y];
            var namedLocation = GenerateLocationForTerrain(weather, terrain, allowElite);

            if (namedLocation == null)
                continue; // No suitable location for this terrain

            // Set the location's terrain type to match
            namedLocation.Terrain = terrain;

            // Generate hidden features using position seed (same approach as terrain locations)
            int positionSeed = unchecked(x * 374761393 + y * 668265263 + Width * 1274126177);
            var discoveryGenerator = new DiscoveryGenerator(positionSeed + LocationFactory.DiscoverySeedOffset);
            namedLocation.HiddenFeatures.AddRange(discoveryGenerator.GenerateFor(terrain));

            // Named sites inherit local river/lake access from their terrain location.
            if (!namedLocation.Features.OfType<WaterFeature>().Any())
                namedLocation.Features.AddRange(location.Features.OfType<WaterFeature>());
            map.SetLocation(x, y, namedLocation);
            placedPositions.Add(pos);
        }
    }

    /// <summary>
    /// Generate a location that matches the given terrain type.
    /// Uses weighted selection filtered to locations that prefer this terrain.
    /// If allowElite is false, filters out elite locations.
    /// </summary>
    private Location? GenerateLocationForTerrain(Weather weather, TerrainType terrain, bool allowElite)
    {
        // Filter to locations that prefer this terrain
        var validLocations = LocationWeights
            .Where(l => l.PreferredTerrain != null && l.PreferredTerrain.Contains(terrain))
            .ToList();

        // If not allowing elite, filter them out
        if (!allowElite)
        {
            validLocations = validLocations
                .Where(l => !EliteLocationFactories.Contains(l.Factory))
                .ToList();
        }

        if (validLocations.Count == 0)
            return null;

        double totalWeight = validLocations.Sum(w => w.Weight);
        double roll = _rng.NextDouble() * totalWeight;

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
