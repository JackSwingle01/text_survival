namespace Survival;

public enum Terrain
{
    Forest, Clearing, Plain, Hills, Water, Marsh, Rock,
    Mountain, DeepWater, // impassable
}

/// A cave floor connects only to its own floor or a mouth.
public enum Cave { None, Floor, Entrance }

/// Generation lays only Ravine and Cliff, both impassable; the rest are authored or player-built.
public enum EdgeType { River, Cliff, Climb, GameTrail, TrailMarker, CutTrail, Ravine }

public enum Direction { North, East, South, West }

/// What a tile is now, where that differs from generation.
public sealed class TileState
{
    /// Footprints, one sign per kind of maker. Owned by Tracks; null when the ground is clean.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<TrackMaker, Track>? Tracks { get; set; }
    /// Snow, water and ice on the ground, once something has written to it (Ground.Write). Owned by Ground; null while it is its class's.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public GroundState? Ground { get; set; }
    /// Pools something has drawn from, by index into Pools.At. Owned by Pools.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, PoolState>? Pools { get; set; }
    /// Things left here: camp storage and whatever a full pack dropped. Unlimited. Owned by Items.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Inventory? Stash { get; set; }
    /// Who made the stash: whoever first put something down here. Goes with the stash. Owned by Items.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Cause? StashCause { get; set; }
    /// The stash's own id (World.NextStashId): a new one each time a stash is begun here. Goes with the stash. Owned by Items.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? StashId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<Carcass>? Carcasses { get; set; }
    /// Nothing here differs from generation, so the tile can leave World.Tiles.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Tracks == null && Ground == null && Pools == null && Stash == null && StashCause == null && StashId == null && Carcasses == null;
}

/// Trees is stated on every row, null where none stand, so a forest cannot lose its timber to a typo.
public sealed record TerrainRow(bool Passable, double Hazard, double Wind, double Cover, double Visibility, string Substrate,
    TreeRow? Trees, int? TraversalMinutes = null, ForageRef? Forage = null);
public sealed record CaveRow(double Hazard, double Wind, double Cover, double Visibility, ForageRef Forage);
public sealed record ForageRef(string Table, double Density);
public sealed record TreeRow(string Name, double MinutesToFell);

/// What a felled tree gives (Content/terrain.json felling), wherever it stood.
/// A mixed stand's tree is one wood, drawn by MixedWood's shares in Resource order (Pine, Birch, Oak) -
/// the loader reads keyed tables in enum order, whatever order the file lists them in.
public sealed record Felling(Pieces Logs, IReadOnlyDictionary<Resource, double> LogKg, double LogKgSpreadPct,
    IReadOnlyList<(Resource Wood, double Share)> MixedWood, Pieces Kindling, Pieces Tinder)
{
    public Resource MixedWoodAt(double roll)
    {
        double below = 0;
        for (int i = 0; i < MixedWood.Count - 1; i++)
            if (roll < (below += MixedWood[i].Share)) return MixedWood[i].Wood;
        return MixedWood[^1].Wood;
    }
}

/// Count and Kg are [min, max] ranges, Count inclusive.
public sealed record Pieces(IReadOnlyList<int> Count, IReadOnlyList<double>? Kg = null)
{
    public double MiddleCount() => (Count[0] + Count[1]) / 2.0;
    public double MiddleKg() => Kg is [var lo, var hi] ? (lo + hi) / 2 : throw new InvalidOperationException("these pieces have no weight");
}
/// Negative TraversalMinutes saves time.
public sealed record EdgeRow(int TraversalMinutes, bool Blocking);
/// The days of the year (Weather.DayOfYear) lakes and rivers freeze over and break up (Content/terrain.json).
public sealed record WaterIce(int FreezeDay, int ThawDay);

public static class TerrainProps
{
    // By enum value: MapGen asks these of every tile, many times over.
    static readonly TerrainRow[] Rows = [.. Enum.GetValues<Terrain>().Select(t => Content.Tables.Terrains[t])];
    static readonly EdgeRow[] Edges = [.. Enum.GetValues<EdgeType>().Select(e => Content.Tables.Edges[e])];

    public static TerrainRow Row(this Terrain t) => Rows[(int)t];
    public static CaveRow Row(this Cave c) =>
        Content.Tables.Caves.TryGetValue(c, out var row) ? row : throw new KeyNotFoundException($"No cave row for {c}");

    public static bool IsPassable(this Terrain t) => Rows[(int)t].Passable;

    /// Minutes to cross one 100 m tile at a walk, before hazard and modifier.
    public static int BaseTraversalMinutes(this Terrain t) => Rows[(int)t].TraversalMinutes ?? int.MaxValue;

    /// 0-1: how bad the footing is (Footing turns it into a chance of a fall), and how much the ground slows you.
    public static double BaseHazardLevel(this Terrain t) => Rows[(int)t].Hazard;

    /// 0-2.
    public static double BaseWindFactor(this Terrain t) => Rows[(int)t].Wind;

    public static int TraversalMinutes(this EdgeType e) => Edges[(int)e].TraversalMinutes;

    public static bool IsBlocking(this EdgeType e) => Edges[(int)e].Blocking;
}

public enum RunOutcome { Surviving, Died, Crossed }

/// Terrain as generated from a seed. Never saved: Map.Of rebuilds it from World.Seed.
public sealed class GeneratedMap(int width, int height, Terrain[,] terrain, Cave[,] caves, int?[,] caveIds,
    Dictionary<(TileId, TileId), EdgeType> edges, HashSet<TileId> river, TileId camp, Dictionary<TileId, int> passStages,
    Dictionary<TileId, LocationDef>? sites = null, IReadOnlyCollection<TileId>? pass = null)
{
    readonly Dictionary<TileId, LocationDef> _sites = sites ?? [];
    readonly HashSet<TileId> _pass = [.. pass ?? passStages.Keys, .. passStages.Keys];
    public int Width { get; } = width;
    public int Height { get; } = height;
    public TileId Camp { get; } = camp;
    public TileId Exit { get; } = passStages.Single(s => Map.PassStages[s.Value].IsCrossingExit).Key;
    public IReadOnlyDictionary<TileId, int> PassStageIndex => passStages;
    public IReadOnlyDictionary<(TileId, TileId), EdgeType> Edges => edges;

    public bool InBounds(TileId t) => MapGen.In(t, Width, Height);
    public Terrain TerrainAt(TileId t) => terrain[t.X, t.Y];
    public Cave CaveAt(TileId t) => caves[t.X, t.Y];
    public int? CaveIdAt(TileId t) => caveIds[t.X, t.Y];
    /// Beside a river: these tiles carry the river's thin ice, which named sites (the pass stages) do not.
    public bool BesideRiver(TileId t) => river.Contains(t) && !passStages.ContainsKey(t);
    public LocationDef? StageAt(TileId t) => passStages.TryGetValue(t, out int i) ? Map.PassStages[i] : null;
    public bool OnPass(TileId t) => _pass.Contains(t);
    public IReadOnlyCollection<TileId> Pass => _pass;
    public LocationDef? SiteAt(TileId t) => StageAt(t) ?? _sites.GetValueOrDefault(t);
    public IReadOnlyDictionary<TileId, LocationDef> Sites => _sites;
    public EdgeType? EdgeBetween(TileId a, TileId b) => edges.TryGetValue(MapGen.EdgeKey(a, b), out var e) ? e : null;
}

/// The world's terrain and how tiles connect. Terrain is a pure function of the seed.
// Visibility is the player's memory (Sight, Memories), not map truth, so it is not here.
public static class Map
{
    public const int DefaultWidth = 224, DefaultHeight = 82; // 100 m tiles

    /// Each owner calls this when it sets its field back to null, so the save holds only what differs.
    public static void Tidy(World world, TileId tile)
    {
        if (world.Tiles.TryGetValue(tile, out var state) && state.IsEmpty) world.Tiles.Remove(tile);
    }

    /// West to east along the pass row, the last one the exit.
    public static readonly LocationDef[] PassStages = [.. Content.PassStages];

    public static RunOutcome Outcome(World world)
    {
        var player = world.Agent(AgentId.Player);
        if (!Body.IsAlive(player.Body)) return RunOutcome.Died;
        return player.Tile == Of(world).Exit ? RunOutcome.Crossed : RunOutcome.Surviving;
    }

    public static GeneratedMap Generate(int seed, int width = DefaultWidth, int height = DefaultHeight) =>
        MapGen.Generate(seed, width, height);

    public static GeneratedMap Of(World world) => world.GeneratedMap ??= Generate(world.Seed);

    public static TileId Step(TileId from, Direction dir) => dir switch
    {
        Direction.North => new(from.X, from.Y - 1),
        Direction.East => new(from.X + 1, from.Y),
        Direction.South => new(from.X, from.Y + 1),
        Direction.West => new(from.X - 1, from.Y),
        _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null),
    };

    public static string? Blocked(GeneratedMap m, TileId from, TileId to)
    {
        if (MapGen.Manhattan(from, to) != 1) throw new ArgumentException($"{from} and {to} are not adjacent");
        if (!m.InBounds(to)) return "the edge of the world";
        if (!m.TerrainAt(to).IsPassable()) return m.TerrainAt(to) == Terrain.Mountain ? "mountains block the way" : "deep water blocks the way";
        if (CaveWall(m, from, to)) return "solid rock blocks the way";
        return m.EdgeBetween(from, to) switch
        {
            EdgeType.Ravine => "a deep ravine blocks the way",
            EdgeType.Cliff => "a sheer cliff blocks the way",
            _ => null,
        };
    }

    /// On the wing water, ravines and cliffs are nothing; the mountain wall and the rock round a cave still are.
    public static string? BlockedInFlight(GeneratedMap m, TileId from, TileId to)
    {
        if (MapGen.Manhattan(from, to) != 1) throw new ArgumentException($"{from} and {to} are not adjacent");
        if (!m.InBounds(to)) return "the edge of the world";
        if (m.TerrainAt(to) == Terrain.Mountain) return "mountains block the way";
        return CaveWall(m, from, to) ? "solid rock blocks the way" : null;
    }

    static bool CaveWall(GeneratedMap m, TileId from, TileId to)
    {
        bool wall(TileId a, TileId b) => m.CaveAt(a) == Cave.Floor && (m.CaveIdAt(a) != m.CaveIdAt(b) || m.CaveAt(b) == Cave.None);
        return wall(from, to) || wall(to, from);
    }

    // ---- per-tile ground ----

    public static double TerrainHazardLevel(GeneratedMap m, TileId t) =>
        m.SiteAt(t)?.Hazard ?? (t == m.Camp ? 0 : m.CaveAt(t) is var c and not Cave.None ? c.Row().Hazard : m.TerrainAt(t).BaseHazardLevel());

    /// The ground plus frozen water. River ice is thin (0.15 for any ice, 0.2 more for thin); a site's own
    /// water replaces the river's. Thickness is fixed: only whether it is there follows the date (IsFrozen).
    public static double TravelHazardLevel(GeneratedMap m, TileId t, long minute) =>
        Math.Min(1.0, TerrainHazardLevel(m, t) + (!HoldsWater(m, t) || !IsFrozen(m, t, minute) ? 0
            : m.SiteAt(t)?.Water is { } w ? w.HazardDelta : m.BesideRiver(t) ? 0.15 + 0.20 : 0));

    public static bool HoldsWater(GeneratedMap m, TileId t) =>
        m.TerrainAt(t) is Terrain.Water or Terrain.DeepWater || m.BesideRiver(t) || m.SiteAt(t)?.Water != null;

    /// By the date alone (terrain.json waterIce), a stand-in for ice that grows and thaws with the cold.
    public static bool IsFrozen(GeneratedMap m, TileId t, long minute)
    {
        if (!HoldsWater(m, t)) throw new ArgumentException($"{t} holds no water to freeze");
        return m.SiteAt(t)?.Water is not { Frozen: false } && IsIceSeason(minute);
    }

    public static bool IsIceSeason(long minute)
    {
        var ice = Content.Tables.WaterIce;
        int day = Weather.DayOfYear(minute);
        return day >= ice.FreezeDay || day < ice.ThawDay;
    }

    /// An unfrozen lake tile, to be waded; a spring on one is open all year. Deep water is a wall in every season.
    public static bool IsOpenWater(GeneratedMap m, TileId t, long minute) =>
        m.InBounds(t) && m.TerrainAt(t) == Terrain.Water && !IsFrozen(m, t, minute);

    /// Random 0.8-1.2 per terrain tile, from the position seed; the camp and caves are 1.
    public static double TraversalModifier(GeneratedMap m, TileId t)
    {
        if (m.SiteAt(t) is { } site) return site.Traversal;
        if (t == m.Camp || m.CaveAt(t) != Cave.None) return 1.0;
        int positionSeed = unchecked(t.X * 374761393 + t.Y * 668265263 + m.Width * 1274126177);
        return 0.8 + new LegacyRng(positionSeed).NextDouble() * 0.4;
    }

    public static int BaseTraversalMinutes(GeneratedMap m, TileId t)
    {
        int terrainBase = m.TerrainAt(t).BaseTraversalMinutes();
        int hazardBonus = (int)Math.Floor(TerrainHazardLevel(m, t) * 5);
        return Math.Max(1, (int)Math.Round((terrainBase + hazardBonus) * TraversalModifier(m, t)));
    }

    public static double WindFactor(GeneratedMap m, TileId t) =>
        m.SiteAt(t)?.Wind ?? (t == m.Camp ? 0.4 : m.CaveAt(t) is var c and not Cave.None ? c.Row().Wind : m.TerrainAt(t).BaseWindFactor());

    /// Overhead cover 0-1: canopy, overhang, cave roof; keeps sun, rain and snow off ground and person alike.
    public static double OverheadCover(GeneratedMap m, TileId t)
    {
        if (m.SiteAt(t) is { } site) return site.Cover;
        if (t == m.Camp) return 0.3;
        return m.CaveAt(t) is var c and not Cave.None ? c.Row().Cover : m.TerrainAt(t).Row().Cover;
    }

    public static double TemperatureDeltaF(GeneratedMap m, TileId t) => m.SiteAt(t)?.TemperatureDeltaF ?? 0;

    /// 0-2.
    public static double Visibility(GeneratedMap m, TileId t)
    {
        if (m.SiteAt(t) is { } site) return site.Visibility;
        if (t == m.Camp) return 0.8;
        return m.CaveAt(t) is var c and not Cave.None ? c.Row().Visibility : m.TerrainAt(t).Row().Visibility;
    }

    /// Underground and the dark sites. Night is the clock's business, not the map's.
    public static bool IsDark(GeneratedMap m, TileId t) => m.SiteAt(t)?.Dark ?? m.CaveAt(t) == Cave.Floor;

    /// Rolled once per tile on its own stream, so the pools and anything describing the place agree.
    public static IReadOnlyList<SiteFeature> SiteFeatures(int seed, GeneratedMap m, TileId t)
    {
        if (m.SiteAt(t) is not { } site) return [];
        if (site.All.All(f => f.Chance >= 1)) return site.All;
        var rng = Survival.Rng.ForSystem(seed, $"site {TileIdConverter.Format(t)}");
        return [.. site.All.Where(f => f.Chance >= 1 || rng.NextDouble() < f.Chance)];
    }

    /// Natural shelters are world state from the start: work improves them and weather wears them.
    public static void PlaceSiteShelters(World world)
    {
        var m = Of(world);
        foreach (var (tile, site) in m.Sites)
            foreach (var s in site.All.OfType<SiteShelter>())
                Shelters.Place(world, tile, new ShelterDef(s.Name, s.Type, s.Insulation, s.Overhead, s.Wind, s.InsulationCap, s.OverheadCap, s.WindCap));
    }

    public static string? DiscoveryText(World world, TileId t)
    {
        var site = Of(world).SiteAt(t);
        if (site?.DiscoveryText is not { } text) return null;
        if (site.Stories is not { Count: > 0 } stories) return text;
        var rng = Survival.Rng.ForSystem(world.Seed, $"story {TileIdConverter.Format(t)}");
        return text.Replace("{story}", stories[rng.NextInt(stories.Count)]);
    }

    public static string Describe(GeneratedMap m, TileId t) =>
        m.SiteAt(t)?.Name ?? (t == m.Camp ? "Forest Camp" : m.CaveAt(t) switch
        {
            Cave.Entrance => "Cave Entrance",
            Cave.Floor => "Cave Passage",
            _ => m.TerrainAt(t).ToString(),
        }) + (m.BesideRiver(t) ? " (river)" : "");
}
