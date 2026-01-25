using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Environments.Factories;

/// <summary>
/// Generates a world map with terrain and placed locations.
/// Uses a layered generation algorithm for natural terrain distribution.
/// </summary>
public class GridWorldGenerator
{
    public int Width { get; set; } = 48;
    public int Height { get; set; } = 48;
    public int TargetNamedLocations { get; set; } = 150;
    public int MinLocationSpacing { get; set; } = 5;  // Minimum tiles between named locations
    public int MountainRows { get; set; } = 9;

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

        // Step 3: Generate rivers flowing north to south
        GenerateRivers(map);

        // Step 4: Create terrain-only locations for all positions
        InitializeTerrainLocations(map, weather);

        // Step 5: Place camp near center (replaces terrain location)
        var (campPos, camp) = PlaceCamp(map, weather);

        // Step 6: Place named locations across the map (replaces terrain locations)
        PlaceNamedLocations(map, weather, campPos);

        // Step 7: Set initial position and visibility around camp
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
                var positionSeed = HashCode.Combine(x, y, Width);
                var location = LocationFactory.MakeTerrainLocation(terrain, weather, positionSeed);

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
            // Two-pass approach: collect noise values, find median, then apply threshold
            var noiseGrid = new double[Width, Height];
            var noiseValues = new List<double>();

            // First pass: generate noise values
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    double noise = OctaveNoise(x, y, seed, scale: 8);
                    noiseGrid[x, y] = noise;
                    if (y >= MountainRows)
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
                    _terrain[x, y] = noiseGrid[x, y] > median ? TerrainType.Forest : TerrainType.Plain;
                }
            }

            // Layer 2: Rock - scattered single tiles
            int rockCount = _rng.Next(56, 93);
            for (int i = 0; i < rockCount; i++)
            {
                var (x, y) = RandomPosition(avoidMountainRows: true);
                if (_terrain[x, y] == TerrainType.Forest || _terrain[x, y] == TerrainType.Plain)
                {
                    _terrain[x, y] = TerrainType.Rock;
                }
            }

            // Layer 3: Clearings - clusters placed in forest
            int clearingClusters = _rng.Next(27, 48);
            PlaceClusters(clearingClusters, TerrainType.Clearing, TerrainType.Forest, MediumShapes);

            // Layer 4: Hills - clusters placed in plains
            int hillClusters = _rng.Next(23, 39);
            PlaceClusters(hillClusters, TerrainType.Hills, TerrainType.Plain, MediumShapes);

            // Layer 5: Water - small clusters scattered
            int waterFeatures = _rng.Next(23, 39);
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
        int passWidth = 1;  // Single-tile pass through mountains

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
    /// Generate rivers flowing north to south along vertical tile edges.
    /// Rivers flow ON edges (between columns), not through tile centers.
    /// </summary>
    private void GenerateRivers(GameMap map)
    {
        _riverAdjacentPositions.Clear();

        int riverCount = _rng.NextDouble() < 0.6 ? 1 : 2;
        var usedStartX = new HashSet<int>();

        for (int r = 0; r < riverCount; r++)
        {
            var edges = GenerateRiverEdges(usedStartX);
            if (edges.Count < 10) continue; // Skip too-short rivers

            // Create river edges directly
            foreach (var (pos1, pos2) in edges)
            {
                map.AddEdge(pos1, pos2, new Grid.TileEdge(Grid.EdgeType.River));
            }

            // Collect adjacent tiles for WaterFeature
            CollectRiverAdjacentTiles(edges);
        }
    }

    /// <summary>
    /// Generate river edges flowing from north to south along vertical tile edges.
    /// Rivers flow along the boundary between two columns (edgeX and edgeX+1).
    /// Each edge is an East edge connecting horizontally adjacent tiles at the same row.
    /// </summary>
    private List<(GridPosition, GridPosition)> GenerateRiverEdges(HashSet<int> usedStartX)
    {
        var edges = new List<(GridPosition, GridPosition)>();

        // Find starting column for the vertical edge (between edgeX and edgeX+1)
        int edgeX = FindRiverStartX(usedStartX);
        if (edgeX < 0 || edgeX >= Width - 1) return edges;

        usedStartX.Add(edgeX);
        int lastDrift = 0; // Track last drift direction to prevent zigzag

        // Flow from just below mountains to bottom of map
        for (int y = MountainRows; y < Height; y++)
        {
            // Check if both tiles on either side of the edge are valid
            if (_terrain[edgeX, y] == TerrainType.Mountain ||
                _terrain[edgeX + 1, y] == TerrainType.Mountain)
                continue;

            // Add the vertical edge at this row (between the two columns)
            edges.Add((new GridPosition(edgeX, y), new GridPosition(edgeX + 1, y)));

            // Drift: shift to a different vertical edge
            if (_rng.NextDouble() < 0.4 && y < Height - 1) // Don't drift on last row
            {
                int drift = _rng.Next(2) == 0 ? -1 : 1;

                // Prevent immediate reversal (zigzag)
                if (lastDrift == 0 || drift == lastDrift)
                {
                    int newEdgeX = edgeX + drift;
                    // Ensure new edge is valid and both tiles exist at next row
                    if (newEdgeX >= 1 && newEdgeX < Width - 2 &&
                        _terrain[newEdgeX, y + 1] != TerrainType.Mountain &&
                        _terrain[newEdgeX + 1, y + 1] != TerrainType.Mountain)
                    {
                        // Add connecting South edge before changing edgeX
                        // The shared column connects old vertical edge to new one
                        int connectingColumn = drift > 0 ? newEdgeX : edgeX;
                        edges.Add((new GridPosition(connectingColumn, y),
                                   new GridPosition(connectingColumn, y + 1)));

                        edgeX = newEdgeX;
                        lastDrift = drift;
                    }
                }
            }
            else
            {
                lastDrift = 0; // Reset drift memory when going straight
            }
        }

        return edges;
    }

    /// <summary>
    /// Find a valid starting X position for a river, spaced at least 10 tiles from others.
    /// </summary>
    private int FindRiverStartX(HashSet<int> usedStartX)
    {
        const int minSpacing = 10;
        var candidates = new List<int>();

        for (int x = 5; x < Width - 5; x++)
        {
            bool tooClose = usedStartX.Any(usedX => Math.Abs(x - usedX) < minSpacing);
            if (!tooClose && _terrain[x, MountainRows] != TerrainType.Mountain)
            {
                candidates.Add(x);
            }
        }

        if (candidates.Count == 0) return -1;
        return candidates[_rng.Next(candidates.Count)];
    }

    /// <summary>
    /// Collect all tiles adjacent to the river edges for WaterFeature addition.
    /// Both tiles on either side of each edge are adjacent to the river.
    /// </summary>
    private void CollectRiverAdjacentTiles(List<(GridPosition, GridPosition)> edges)
    {
        foreach (var (pos1, pos2) in edges)
        {
            // Both tiles on either side of the edge are adjacent to the river
            _riverAdjacentPositions.Add(pos1);
            _riverAdjacentPositions.Add(pos2);
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

    /// <summary>
    /// Place named locations across the map using terrain-aware selection.
    /// Locations are matched to their preferred terrain types.
    /// Elite locations only spawn in outer 20% of map (far from camp).
    /// </summary>
    private void PlaceNamedLocations(GameMap map, Weather weather, GridPosition campPos)
    {
        var placedPositions = new List<GridPosition> { campPos };
        int attempts = 0;
        int maxAttempts = TargetNamedLocations * 10;

        // Calculate outer ring threshold (inner 80% of area = outer 20%)
        double maxRadius = Math.Min(Width, Height) / 2.0;  // 24 tiles for 48x48 map
        double minEliteDistance = maxRadius * Math.Sqrt(0.8);  // ~21.5 tiles

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
            int positionSeed = HashCode.Combine(x, y, Width);
            var discoveryGenerator = new DiscoveryGenerator(positionSeed + LocationFactory.DiscoverySeedOffset);
            namedLocation.HiddenFeatures.AddRange(discoveryGenerator.GenerateFor(terrain));

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
