namespace Survival;

public enum Terrain
{
    Forest, Clearing, Plain, Hills, Water, Marsh, Rock,
    Mountain, DeepWater, // impassable
}

/// Underground tiles. A cave floor connects only to its own floor or a mouth.
public enum Cave { None, Floor, Entrance }

/// Something between two adjacent tiles. Generation lays only Ravine and Cliff, both impassable;
/// the rest are authored or player-built and arrive with phase 4 locations and trail work.
public enum EdgeType { River, Cliff, Climb, GameTrail, TrailMarker, CutTrail, Ravine }

public enum Direction { North, East, South, West }

/// One named stage of the mountain crossing. Hazard, wind and cold from the old LocationFactory.
public sealed record PassStage(string Name, double HazardLevel, double WindFactor, double TemperatureDeltaF, bool IsCrossingExit = false);

/// What a tile is now, where that differs from generation. Ground and tracks add fields here.
public sealed class TileState;

public static class TerrainProps
{
    public static bool IsPassable(this Terrain t) => t is not (Terrain.Mountain or Terrain.DeepWater);

    /// Minutes to cross one 100 m tile of flat ground at a walk, before hazard and modifier.
    public static int BaseTraversalMinutes(this Terrain t) => t switch
    {
        Terrain.Rock or Terrain.Hills or Terrain.Marsh => 2,
        Terrain.Mountain or Terrain.DeepWater => int.MaxValue,
        _ => 1,
    };

    /// 0-1: chance of a slip or fall, and how much the ground slows you.
    public static double BaseHazardLevel(this Terrain t) => t switch
    {
        Terrain.Clearing => 0.05,
        Terrain.Forest => 0.1,
        Terrain.Hills => 0.25,
        Terrain.Rock or Terrain.Marsh => 0.3,
        Terrain.Water => 0.4,
        _ => 0,
    };

    /// 0-2: how much of the wind reaches you here.
    public static double BaseWindFactor(this Terrain t) => t switch
    {
        Terrain.Forest => 0.3,
        Terrain.Clearing => 0.7,
        Terrain.Marsh => 0.8,
        Terrain.Plain => 1.2,
        Terrain.Water => 1.3,
        Terrain.Hills => 1.4,
        Terrain.Mountain => 1.8,
        _ => 1.0,
    };

    /// Minutes an edge adds to a crossing (negative saves time).
    public static int TraversalMinutes(this EdgeType e) => e switch
    {
        EdgeType.River => 4,
        EdgeType.Climb => 6,
        EdgeType.GameTrail or EdgeType.TrailMarker => -1,
        EdgeType.CutTrail => -2,
        _ => 0,
    };

    /// Whether an edge stops a crossing outright. ponytail: no seasons yet (old River could flood in spring).
    public static bool IsBlocking(this EdgeType e) => e is EdgeType.Ravine or EdgeType.Cliff;
}

/// Terrain as generated from a seed. Never saved: Map.Of rebuilds it from World.Seed.
public sealed class GeneratedMap(int width, int height, Terrain[,] terrain, Cave[,] caves, int?[,] caveIds,
    Dictionary<(TileId, TileId), EdgeType> edges, HashSet<TileId> river, TileId camp, Dictionary<TileId, int> passStages)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public TileId Camp { get; } = camp;
    /// The far side of the pass. Reaching it ends the run.
    public TileId Exit { get; } = passStages.Single(s => Map.PassStages[s.Value].IsCrossingExit).Key;
    public IReadOnlyDictionary<TileId, int> PassStageIndex => passStages;
    public IReadOnlyDictionary<(TileId, TileId), EdgeType> Edges => edges;

    public bool InBounds(TileId t) => MapGen.In(t, Width, Height);
    public Terrain TerrainAt(TileId t) => terrain[t.X, t.Y];
    public Cave CaveAt(TileId t) => caves[t.X, t.Y];
    public int? CaveIdAt(TileId t) => caveIds[t.X, t.Y];
    /// Beside a river: the old game gave these tiles a thin-ice WaterFeature, which named sites
    /// (the pass stages) did not inherit.
    public bool BesideRiver(TileId t) => river.Contains(t) && !passStages.ContainsKey(t);
    public PassStage? StageAt(TileId t) => passStages.TryGetValue(t, out int i) ? Map.PassStages[i] : null;
    public EdgeType? EdgeBetween(TileId a, TileId b) => edges.TryGetValue(MapGen.EdgeKey(a, b), out var e) ? e : null;
}

/// The world's terrain and how tiles connect. Terrain is a pure function of the seed (plan decision 1).
// ponytail: no tile visibility / fog of war yet. That is the player's knowledge (plan decision 7), not
// map truth; port old GameMap.UpdateVisibility + Sight rays into the agent-knowledge step, not here.
public static class Map
{
    public const int DefaultWidth = 224, DefaultHeight = 82; // the old game's valley world; 100 m tiles

    /// West to east along the pass row: six stages, the last one the exit.
    public static readonly PassStage[] PassStages =
    [
        new("Pass Approach", 0.6, 1.3, -5),
        new("Lower Pass", 0.8, 1.6, -10),
        new("The Pass Proper", 1.0, 2.0, -15),
        new("Upper Descent", 0.7, 1.5, -10),
        new("Lower Descent", 0.5, 1.0, -5),
        new("Far Side", 0.3, 0.6, 0, IsCrossingExit: true),
    ];

    public static GeneratedMap Generate(int seed, int width = DefaultWidth, int height = DefaultHeight) =>
        MapGen.Generate(seed, width, height);

    /// The world's generated map, built from its seed on first use.
    public static GeneratedMap Of(World world) => world.GeneratedMap ??= Generate(world.Seed);

    public static TileId Step(TileId from, Direction dir) => dir switch
    {
        Direction.North => new(from.X, from.Y - 1),
        Direction.East => new(from.X + 1, from.Y),
        Direction.South => new(from.X, from.Y + 1),
        Direction.West => new(from.X - 1, from.Y),
        _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null),
    };

    /// Why a move between adjacent tiles is impossible, or null if it can be made.
    public static string? Blocked(GeneratedMap m, TileId from, TileId to)
    {
        if (MapGen.Manhattan(from, to) != 1) throw new ArgumentException($"{from} and {to} are not adjacent");
        if (!m.InBounds(to)) return "the edge of the world";
        if (!m.TerrainAt(to).IsPassable()) return m.TerrainAt(to) == Terrain.Mountain ? "mountains block the way" : "deep water blocks the way";
        // Underground floor connects only to its own floor or an explicit mouth.
        bool caveWall(TileId a, TileId b) => m.CaveAt(a) == Cave.Floor && (m.CaveIdAt(a) != m.CaveIdAt(b) || m.CaveAt(b) == Cave.None);
        if (caveWall(from, to) || caveWall(to, from)) return "solid rock blocks the way";
        return m.EdgeBetween(from, to) switch
        {
            EdgeType.Ravine => "a deep ravine blocks the way",
            EdgeType.Cliff => "a sheer cliff blocks the way",
            _ => null,
        };
    }

    // ---- per-tile ground, as the old terrain Location carried it ----

    /// Base injury risk and slowness of the ground's shape: terrain, cave, stage or camp, plus thin river ice.
    public static double TerrainHazardLevel(GeneratedMap m, TileId t) =>
        m.StageAt(t)?.HazardLevel ?? (t == m.Camp ? 0 : m.CaveAt(t) switch
        {
            Cave.Entrance => 0.2,
            Cave.Floor => 0.3,
            _ => m.TerrainAt(t).BaseHazardLevel(),
        });

    /// Old Location.GetTravelHazardLevel: the ground plus frozen water. River ice is thin (0.3):
    /// 0.15 for any ice, 0.2 more for thin. ponytail: static ice; Ground (phase 2c) owns ice that grows and thaws.
    public static double TravelHazardLevel(GeneratedMap m, TileId t) =>
        Math.Min(1.0, TerrainHazardLevel(m, t) + (m.BesideRiver(t) ? 0.15 + 0.20 : 0));

    /// Random 0.8-1.2 per terrain tile, from the old position seed; named places and caves are 1.
    public static double TraversalModifier(GeneratedMap m, TileId t)
    {
        if (m.StageAt(t) != null || t == m.Camp || m.CaveAt(t) != Cave.None) return 1.0;
        int positionSeed = unchecked(t.X * 374761393 + t.Y * 668265263 + m.Width * 1274126177);
        return 0.8 + new LegacyRng(positionSeed).NextDouble() * 0.4;
    }

    /// Old Location.BaseTraversalMinutes: terrain minutes plus a hazard bonus, scaled by the modifier.
    public static int BaseTraversalMinutes(GeneratedMap m, TileId t)
    {
        int terrainBase = m.TerrainAt(t).BaseTraversalMinutes();
        int hazardBonus = (int)Math.Floor(TerrainHazardLevel(m, t) * 5);
        return Math.Max(1, (int)Math.Round((terrainBase + hazardBonus) * TraversalModifier(m, t)));
    }

    /// How much of the wind reaches this tile. For Weather to scale wind by; travel does not read it yet.
    public static double WindFactor(GeneratedMap m, TileId t) =>
        m.StageAt(t)?.WindFactor ?? (t == m.Camp ? 0.4 : m.CaveAt(t) switch
        {
            Cave.Entrance => 0.3,
            Cave.Floor => 0,
            _ => m.TerrainAt(t).BaseWindFactor(),
        });

    /// How much colder this tile is than the regional air (the pass stages). For Weather to apply.
    public static double TemperatureDeltaF(GeneratedMap m, TileId t) => m.StageAt(t)?.TemperatureDeltaF ?? 0;

    public static string Describe(GeneratedMap m, TileId t) =>
        m.StageAt(t)?.Name ?? (t == m.Camp ? "Forest Camp" : m.CaveAt(t) switch
        {
            Cave.Entrance => "Cave Entrance",
            Cave.Floor => "Cave Passage",
            _ => m.TerrainAt(t).ToString(),
        }) + (m.BesideRiver(t) ? " (river)" : "");
}
