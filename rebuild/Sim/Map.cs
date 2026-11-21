namespace Survival;

public enum Terrain
{
    Forest, Clearing, Plain, Hills, Water, Marsh, Rock,
    Mountain, DeepWater, // impassable
}

/// Underground tiles. A cave floor connects only to its own floor or a mouth.
public enum Cave { None, Floor, Entrance }

/// Something between two adjacent tiles. Generation lays only Ravine and Cliff, both impassable;
/// the rest are authored or player-built; nothing lays them yet (no trail cutting or marking).
public enum EdgeType { River, Cliff, Climb, GameTrail, TrailMarker, CutTrail, Ravine }

public enum Direction { North, East, South, West }

/// What a tile is now, where that differs from generation. Ground and tracks add fields here.
public sealed class TileState
{
    /// Footprints, one sign per kind of maker. Owned by Tracks; null when the ground is clean.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<TrackMaker, Track>? Tracks { get; set; }
    /// Snow, water and ice on the ground. Owned by Ground; null until the tile is first observed.
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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<Carcass>? Carcasses { get; set; }
    /// Nothing here differs from generation, so the tile can leave World.Tiles. Each owner sets its
    /// field back to null when it has nothing left to keep.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Tracks == null && Ground == null && Pools == null && Stash == null && StashCause == null && Carcasses == null;
}

/// One terrain as generated (Content/terrain.json). A wall has no traversal, forage or trees.
/// Trees is stated on every row, null where none stand, so a forest cannot lose its timber to a typo.
public sealed record TerrainRow(bool Passable, double Hazard, double Wind, double Cover, double Visibility, string Substrate,
    TreeRow? Trees, int? TraversalMinutes = null, ForageRef? Forage = null);
/// A cave tile as generated (Content/terrain.json caves); its substrate is bare rock.
public sealed record CaveRow(double Hazard, double Wind, double Cover, double Visibility, ForageRef Forage);
/// Which forage table (Content/forage.json) a place has, and the density drawn around.
public sealed record ForageRef(string Table, double Density);
/// Standing timber: mixed forest, unlimited.
public sealed record TreeRow(string Name, double MinutesToFell);

/// What a felled tree gives (Content/terrain.json felling), wherever it stood: a count of logs of
/// its wood, each about LogKg (within LogKgSpreadPct either way), some sticks and pinches of tinder.
/// A mixed stand's tree is one wood, drawn by MixedWood's shares in order. Pools.Chop rolls it;
/// Report reckons its middle.
public sealed record Felling(Pieces Logs, IReadOnlyDictionary<Resource, double> LogKg, double LogKgSpreadPct,
    IReadOnlyList<(Resource Wood, double Share)> MixedWood, Pieces Kindling, Pieces Tinder)
{
    /// The wood a mixed stand's tree turns out to be, from a roll in [0, 1).
    public Resource MixedWoodAt(double roll)
    {
        double below = 0;
        for (int i = 0; i < MixedWood.Count - 1; i++)
            if (roll < (below += MixedWood[i].Share)) return MixedWood[i].Wood;
        return MixedWood[^1].Wood;
    }
}

/// A count in [Count[0], Count[1]], both inclusive, and (where pieces weigh) each one's kg drawn
/// uniformly in [Kg[0], Kg[1]].
public sealed record Pieces(IReadOnlyList<int> Count, IReadOnlyList<double>? Kg = null)
{
    public double MiddleCount() => (Count[0] + Count[1]) / 2.0;
    public double MiddleKg() => Kg is [var lo, var hi] ? (lo + hi) / 2 : throw new InvalidOperationException("these pieces have no weight");
}
/// Minutes an edge adds to a crossing (negative saves time), and whether it stops one outright.
public sealed record EdgeRow(int TraversalMinutes, bool Blocking);

public static class TerrainProps
{
    // By enum value: MapGen asks these of every tile, many times over.
    static readonly TerrainRow[] Rows = [.. Enum.GetValues<Terrain>().Select(t => Content.Tables.Terrains[t])];
    static readonly EdgeRow[] Edges = [.. Enum.GetValues<EdgeType>().Select(e => Content.Tables.Edges[e])];

    public static TerrainRow Row(this Terrain t) => Rows[(int)t];
    public static CaveRow Row(this Cave c) =>
        Content.Tables.Caves.TryGetValue(c, out var row) ? row : throw new KeyNotFoundException($"No cave row for {c}");

    public static bool IsPassable(this Terrain t) => Rows[(int)t].Passable;

    /// Minutes to cross one 100 m tile of flat ground at a walk, before hazard and modifier.
    public static int BaseTraversalMinutes(this Terrain t) => Rows[(int)t].TraversalMinutes ?? int.MaxValue;

    /// 0-1: chance of a slip or fall, and how much the ground slows you.
    public static double BaseHazardLevel(this Terrain t) => Rows[(int)t].Hazard;

    /// 0-2: how much of the wind reaches you here.
    public static double BaseWindFactor(this Terrain t) => Rows[(int)t].Wind;

    /// Minutes an edge adds to a crossing (negative saves time).
    public static int TraversalMinutes(this EdgeType e) => Edges[(int)e].TraversalMinutes;

    /// Whether an edge stops a crossing outright. No seasons yet: the old River could flood in spring.
    public static bool IsBlocking(this EdgeType e) => Edges[(int)e].Blocking;
}

/// How the run stands. Only death and the far side of the pass end it.
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
    public LocationDef? StageAt(TileId t) => passStages.TryGetValue(t, out int i) ? Map.PassStages[i] : null;
    /// The whole corridor through the mountains, its six stages and the rock between them.
    public bool OnPass(TileId t) => _pass.Contains(t);
    public IReadOnlyCollection<TileId> Pass => _pass;
    /// The named place on this tile: a pass stage, a cave site or a site dealt onto open land.
    public LocationDef? SiteAt(TileId t) => StageAt(t) ?? _sites.GetValueOrDefault(t);
    /// Every named site off the pass, by tile.
    public IReadOnlyDictionary<TileId, LocationDef> Sites => _sites;
    public EdgeType? EdgeBetween(TileId a, TileId b) => edges.TryGetValue(MapGen.EdgeKey(a, b), out var e) ? e : null;
}

/// The world's terrain and how tiles connect. Terrain is a pure function of the seed (plan decision 1).
// No tile visibility / fog of war yet. That is the player's knowledge (plan decision 7), not
// map truth; port old GameMap.UpdateVisibility + Sight rays into the agent-knowledge step, not here.
public static class Map
{
    public const int DefaultWidth = 224, DefaultHeight = 82; // the old game's valley world; 100 m tiles

    /// A tile whose owners have all let go of it leaves World.Tiles, so the save holds only what differs.
    /// Each owner calls this when it sets its field back to null.
    public static void Tidy(World world, TileId tile)
    {
        if (world.Tiles.TryGetValue(tile, out var state) && state.IsEmpty) world.Tiles.Remove(tile);
    }

    /// West to east along the pass row: six stages (Content/locations.json), the last one the exit.
    public static readonly LocationDef[] PassStages = [.. Content.PassStages];

    /// Derived, never stored (old GameRunner looped while alive and not on the crossing exit): a
    /// living player standing on the far side has won, a dead one has lost, and nothing to keep in sync.
    public static RunOutcome Outcome(World world)
    {
        var player = world.Agent(AgentId.Player);
        if (!Body.IsAlive(player.Body)) return RunOutcome.Died;
        return player.Tile == Of(world).Exit ? RunOutcome.Crossed : RunOutcome.Surviving;
    }

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
        m.SiteAt(t)?.Hazard ?? (t == m.Camp ? 0 : m.CaveAt(t) is var c and not Cave.None ? c.Row().Hazard : m.TerrainAt(t).BaseHazardLevel());

    /// Old Location.GetTravelHazardLevel: the ground plus frozen water. River ice is thin (0.3):
    /// 0.15 for any ice, 0.2 more for thin. A site with water of its own has that water instead of
    /// the river (old named sites inherited the river's only when they had none).
    /// Static ice: a site's water ice does not grow or thaw with Ground yet.
    public static double TravelHazardLevel(GeneratedMap m, TileId t) =>
        Math.Min(1.0, TerrainHazardLevel(m, t) + (m.SiteAt(t)?.Water is { } w ? w.HazardDelta : m.BesideRiver(t) ? 0.15 + 0.20 : 0));

    /// Random 0.8-1.2 per terrain tile, from the old position seed; a named site has its own (mostly 1),
    /// and the camp and caves are 1.
    public static double TraversalModifier(GeneratedMap m, TileId t)
    {
        if (m.SiteAt(t) is { } site) return site.Traversal;
        if (t == m.Camp || m.CaveAt(t) != Cave.None) return 1.0;
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

    /// How much of the wind reaches this tile: what a person feels (wind chill) and the ground.
    public static double WindFactor(GeneratedMap m, TileId t) =>
        m.SiteAt(t)?.Wind ?? (t == m.Camp ? 0.4 : m.CaveAt(t) is var c and not Cave.None ? c.Row().Wind : m.TerrainAt(t).BaseWindFactor());

    /// Overhead cover 0-1: canopy, rock overhang, cave roof. Old terrain BaseOverheadCover and the
    /// camp, cave and pass-stage factories. What keeps sun, rain and snow off both the ground and a person.
    public static double OverheadCover(GeneratedMap m, TileId t)
    {
        if (m.SiteAt(t) is { } site) return site.Cover;
        if (t == m.Camp) return 0.3;
        return m.CaveAt(t) is var c and not Cave.None ? c.Row().Cover : m.TerrainAt(t).Row().Cover;
    }

    /// How much colder this tile is than the regional air (the pass stages).
    public static double TemperatureDeltaF(GeneratedMap m, TileId t) => m.SiteAt(t)?.TemperatureDeltaF ?? 0;

    /// How far you can see from here, 0-2: old Location.VisibilityFactor (terrain BaseVisibility,
    /// the camp's 0.8, a cave's, or the named site's own).
    public static double Visibility(GeneratedMap m, TileId t)
    {
        if (m.SiteAt(t) is { } site) return site.Visibility;
        if (t == m.Camp) return 0.8;
        return m.CaveAt(t) is var c and not Cave.None ? c.Row().Visibility : m.TerrainAt(t).Row().Visibility;
    }

    /// No light to work by without fire or torch: underground, and the old dark sites (a bear's cave,
    /// an ice crevasse). Night is the clock's business, not the map's.
    public static bool IsDark(GeneratedMap m, TileId t) => m.SiteAt(t)?.Dark ?? m.CaveAt(t) == Cave.Floor;

    /// The site's features this tile really has: each on its chance, rolled once per tile on its own
    /// stream, so the pools and anything describing the place agree on what is there.
    public static IReadOnlyList<SiteFeature> SiteFeatures(int seed, GeneratedMap m, TileId t)
    {
        if (m.SiteAt(t) is not { } site) return [];
        if (site.All.All(f => f.Chance >= 1)) return site.All;
        var rng = Survival.Rng.ForSystem(seed, $"site {TileIdConverter.Format(t)}");
        return [.. site.All.Where(f => f.Chance >= 1 || rng.NextDouble() < f.Chance)];
    }

    /// Puts every named site's shelter on its tile (old ShelterFeature on the site's Location). Natural
    /// shelters are world state from the start, since work improves them and weather wears them.
    public static void PlaceSiteShelters(World world)
    {
        var m = Of(world);
        foreach (var (tile, site) in m.Sites)
            foreach (var s in site.All.OfType<SiteShelter>())
                Shelters.Place(world, tile, new ShelterDef(s.Name, s.Type, s.Insulation, s.Overhead, s.Wind, s.InsulationCap, s.OverheadCap, s.WindCap));
    }

    /// What a person reads arriving here: the site's old DiscoveryText, its story drawn once per tile.
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
