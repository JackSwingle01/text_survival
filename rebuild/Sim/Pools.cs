using System.Text.Json.Serialization;

namespace Survival;

/// Something on a tile that work draws from (plan Keep/Port/Cut: old ForageFeature, HarvestableFeature,
/// WoodedAreaFeature, SalvageFeature and EnvironmentalDetail, merged). Generated from (seed, tile),
/// never saved: only what has been drawn from is a delta, in TileState.Pools.
public abstract record PoolDef(string Name);

/// Ambient searching (old ForageFeature): finds per hour = density x abundance, density falling
/// as the tile is worked and regrowing over four weeks.
public sealed record ForagePool(string Name, double Density, IReadOnlyList<ForageItem> Items) : PoolDef(Name);
public sealed record ForageItem(Resource Resource, double Abundance, double MinKg, double MaxKg);

/// Standing timber (old WoodedAreaFeature). Null wood is mixed forest; null trees is unlimited.
public sealed record TreePool(string Name, Resource? Wood, double MinutesToFell, int? Trees = null, double RespawnHoursPerTree = 168) : PoolDef(Name);

/// Old HarvestableFeature, SalvageFeature and EnvironmentalDetail: units you take one cycle at a
/// time. Each cycle of MinutesPerUnit yields one unit of every item still there; salvage and
/// details are one cycle holding everything. Hidden until searched for.
public sealed record StockPool(
    string Name, StockKind Kind, int MinutesPerUnit, IReadOnlyList<StockItem> Items, double RevealAtHours,
    double PlacementHeightM = 0.6, IReadOnlyList<Gear>? Gear = null, ToolType? RequiresTool = null) : PoolDef(Name);
/// RespawnHoursPerUnit 0 never regrows.
public sealed record StockItem(Resource Resource, int Units, double KgPerUnit, double RespawnHoursPerUnit = 0);

/// A natural shelter (old DiscoveryGenerator rock overhang, ice cave, cave sleeping recess) hidden
/// until searched for. Nothing to gather: finding it puts the shelter on the tile (Pools.Search).
public sealed record ShelterFind(ShelterDef Shelter, double RevealAtHours, double PlacementHeightM = 2.0) : PoolDef(Shelter.Name);

/// How a stock is worked: harvest (no impairment check), salvage (slowed by bad legs), detail (a light look).
public enum StockKind { Harvest, Salvage, Detail }

/// What drawing from a pool has changed. Only pools something has worked are stored.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ForageState), "forage")]
[JsonDerivedType(typeof(TreeState), "trees")]
[JsonDerivedType(typeof(StockState), "stock")]
public abstract class PoolState;

public sealed class ForageState : PoolState
{
    public double HoursForaged { get; set; }
    public long LastForagedMinute { get; set; }
    /// Herd grazing per resource (phase 3b): level 0-1 as of Minute, regrowing linearly after.
    public Dictionary<Resource, Grazing> Grazed { get; init; } = [];
}
public readonly record struct Grazing(double Level, long Minute);

public sealed class TreeState : PoolState
{
    public double MinutesWorked { get; set; }
    public int Felled { get; set; }
    /// When the stand was last cut to nothing (limited stands only).
    public long ClearedMinute { get; set; }
}

public sealed class StockState : PoolState
{
    /// Units left per item as of LastTakenMinute, and when each was last taken (regrowth counts from it).
    public required int[] Units { get; init; }
    public required long[] LastTakenMinute { get; init; }
    public double MinutesWorked { get; set; }
    public bool GearTaken { get; set; }
}

public static class Pools
{
    /// Old ForageFeature.respawnRateHours: four weeks to regrow fully.
    public const double ForageRespawnHours = 672;
    const double MaxRevealMultiplier = 10.0;

    // ---- generation: a pure function of (seed, tile) ----

    /// The pools generation put on this tile, in index order. Cached; never saved.
    public static IReadOnlyList<PoolDef> At(World world, TileId tile)
    {
        world.PoolCache ??= [];
        if (!world.PoolCache.TryGetValue(tile, out var pools))
            world.PoolCache[tile] = pools = Generate(world.Seed, Map.Of(world), tile);
        return pools;
    }

    public static PoolDef Def(World world, PoolId id)
    {
        var pools = At(world, id.Tile);
        return id.Index >= 0 && id.Index < pools.Count ? pools[id.Index] : throw new KeyNotFoundException($"No pool {id}");
    }

    internal static IReadOnlyList<PoolDef> Generate(int seed, GeneratedMap m, TileId tile)
    {
        var pools = new List<PoolDef>();
        if (!m.InBounds(tile) || !m.TerrainAt(tile).IsPassable()) return pools;
        var rng = Rng.ForSystem(seed, $"pools {TileIdConverter.Format(tile)}");
        var terrain = m.TerrainAt(tile);

        // Old GridWorldGenerator.CreateCampLocation: thin mixed forage, no timber, nothing hidden.
        if (tile == m.Camp)
        {
            pools.Add(new ForagePool("forage", 0.38, MixedForest));
            return pools;
        }
        // A named place carries its own content; open-land sites also keep the terrain's hidden finds
        // (old PlaceNamedLocations ran the terrain's DiscoveryGenerator for them) and cave sites the
        // passage's (old PlaceCaveLocations copied them across, skipping what the site already had).
        if (m.SiteAt(tile) is LocationDef site)
        {
            pools.AddRange(SitePools(Map.SiteFeatures(seed, m, tile), rng));
            if (site.Placement == Placement.Named) pools.AddRange(Discoveries(terrain, rng));
            else if (site.Placement == Placement.Cave)
                pools.AddRange(CaveDiscoveries(rng).Where(d => pools.All(p => p.Name != d.Name) && site.All.OfType<SiteShelter>().All(s => s.Name != d.Name)));
            return pools;
        }
        // Old CaveLocationFactory.MakePassage: stone and bone underfoot, wind-blown sticks at the mouth.
        if (m.CaveAt(tile) != Cave.None)
        {
            pools.Add(new ForagePool("forage", 0.52, m.CaveAt(tile) == Cave.Entrance ? CaveMouth : CaveFloor));
            pools.AddRange(CaveDiscoveries(rng));
            return pools;
        }
        pools.Add(TerrainForage(terrain, rng));
        if (terrain == Terrain.Forest) pools.Add(new TreePool("Forest Trees", null, 100));
        pools.AddRange(Details(terrain, rng));
        pools.AddRange(Discoveries(terrain, rng));
        return pools;
    }

    /// Old FeatureFactory.CreateTerrainForage: a terrain's table at a density drawn around its base.
    static ForagePool TerrainForage(Terrain terrain, Rng rng)
    {
        var (baseDensity, items) = terrain switch
        {
            Terrain.Forest => (1.0, MixedForest),
            Terrain.Clearing => (0.72, MixedForest),
            Terrain.Marsh => (0.52, Wetland),
            Terrain.Water => (0.52, FrozenCreek),
            Terrain.Plain => (0.52, Open),
            Terrain.Hills => (0.40, Rocky),
            Terrain.Rock => (0.52, Rocky),
            _ => (0.32, Barren),
        };
        // old game: drew this from the process-wide RNG, so the same seed gave different tiles (bug, fixed: seeded by tile).
        return new ForagePool("forage", Math.Clamp(Normal(rng, baseDensity, 0.32), 0.1, 2.0), items);
    }

    /// A named site's features as pools, in order (Map.SiteFeatures has rolled their chances). Water that runs open is a
    /// source to draw from (old WaterFeature.AsOpenWater, as Hidden Spring); frozen water is hazard
    /// (Map.TravelHazardLevel). Shelters, small game, caches and events are not pools.
    // A site's shelter is placed with the world (Map.PlaceSiteShelters). Its small game waits for
    // hunting by density, and its event triggers (FirstVisitEvent, SiteEvent) for moments: they would
    // fire on arrival and in Search below, when the hidden one is found.
    static IEnumerable<PoolDef> SitePools(IReadOnlyList<SiteFeature> features, Rng rng)
    {
        foreach (var f in features)
        {
            switch (f)
            {
                case SiteForage fo: yield return new ForagePool("forage", fo.Density, fo.Items); break;
                case SiteTrees t: yield return new TreePool(t.Name, t.Wood, t.MinutesToFell, t.Trees, t.RespawnHoursPerTree); break;
                case SiteStock { Work: StockKind.Salvage } s:
                    var (items, gear) = RollLoot(Content.Loot[s.Loot!], rng);
                    yield return new StockPool(s.Name, StockKind.Salvage, s.Minutes, items, f.RevealAtHours, PlacementHeightM: 0.3, Gear: gear.Count > 0 ? gear : null);
                    break;
                case SiteStock s:
                    yield return new StockPool(s.Name, s.Work, s.Minutes,
                        [.. s.Items!.Select(i => new StockItem(i.Resource, i.UnitsMax is int max ? Between(rng, i.Units, max + 1) : i.Units, i.KgPerUnit, i.RespawnHoursPerUnit))],
                        f.RevealAtHours, RequiresTool: s.RequiresTool);
                    break;
                case SiteWater { Frozen: false } w:
                    yield return new StockPool(w.Name, StockKind.Harvest, 1, [new StockItem(Resource.Water, 100, 1.0, 0.1)], f.RevealAtHours);
                    break;
            }
        }
    }

    /// One loot table rolled (Content/loot.json): the resources as one salvage item each, and the gear.
    internal static (List<StockItem> Items, List<Gear> Gear) RollLoot(IReadOnlyList<LootRoll> table, Rng rng)
    {
        var kg = new SortedDictionary<Resource, double>();
        var gear = new List<Gear>();
        void Take(LootRoll r)
        {
            if (r.Chance < 1 && rng.NextDouble() >= r.Chance) return;
            if (r.Resource is Resource res)
            {
                int pieces = r.Pieces is [var lo, var hi] ? Between(rng, lo, hi + 1) : 1;
                kg[res] = kg.GetValueOrDefault(res) + Pieces(rng, pieces, r.Kg![0], r.Kg[1]);
            }
            else if (r.Gear != null)
            {
                var g = Items.Make(r.Gear[rng.NextInt(r.Gear.Count)]);
                // old game: rolled a share of an unlimited (-1) maximum, which came out 0, broken; unlimited stays whole (bug, fixed).
                if (r.DurabilityPct is [var d0, var d1] && g.MaxDurability > 0)
                    g.Durability = Math.Max(1, Between(rng, (int)(g.MaxDurability * d0), (int)(g.MaxDurability * d1) + 1));
                gear.Add(g);
            }
            else
            {
                int count = Between(rng, r.Pick![0], r.Pick[1] + 1);
                var of = r.Of!.ToList();
                for (int i = of.Count - 1; i > 0; i--) { int j = rng.NextInt(i + 1); (of[i], of[j]) = (of[j], of[i]); }
                foreach (var pick in of.Take(count)) Take(pick);
            }
        }
        foreach (var r in table) Take(r);
        return ([.. kg.Select(x => All(x.Key, x.Value))], gear);
    }

    static double Normal(Rng rng, double mean, double sd)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return mean + sd * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    static ForageItem F(Resource r, double abundance, double minKg, double maxKg) => new(r, abundance, minKg, maxKg);

    // Old FeatureFactory forage tables. AddMixedWood splits 40/35/25 pine/birch/oak.
    internal static readonly IReadOnlyList<ForageItem> MixedForest =
    [
        F(Resource.Pine, 1.5 * 0.4, 1.5, 3.5), F(Resource.Birch, 1.5 * 0.35, 1.5, 3.5), F(Resource.Oak, 1.5 * 0.25, 1.5, 3.5),
        F(Resource.Kindling, 3.0, 0.2, 0.6), F(Resource.Tinder, 2.0, 0.02, 0.08), F(Resource.Berries, 0.6, 0.2, 0.6),
        F(Resource.Nuts, 0.08, 0.03, 0.08), F(Resource.Roots, 0.15, 0.15, 0.3), F(Resource.PlantFiber, 0.5, 0.05, 0.15),
        F(Resource.Stone, 0.3, 0.2, 0.4), F(Resource.BirchPolypore, 0.15, 0.05, 0.15), F(Resource.Chaga, 0.1, 0.05, 0.2),
        F(Resource.Amadou, 0.12, 0.02, 0.08), F(Resource.PineNeedles, 0.25, 0.02, 0.1), F(Resource.PineResin, 0.1, 0.02, 0.1),
        F(Resource.Usnea, 0.15, 0.02, 0.08),
    ];
    static readonly IReadOnlyList<ForageItem> Wetland =
    [
        F(Resource.Kindling, 2.0, 0.2, 0.5), F(Resource.Birch, 1.0, 1.0, 2.0), F(Resource.Stone, 0.6, 0.2, 0.5),
        F(Resource.Roots, 0.4, 0.15, 0.3), F(Resource.WillowBark, 0.25, 0.02, 0.1), F(Resource.SphagnumMoss, 0.15, 0.1, 0.3),
    ];
    static readonly IReadOnlyList<ForageItem> Open =
    [
        F(Resource.Tinder, 0.7, 0.03, 0.1), F(Resource.Kindling, 0.2, 0.1, 0.3), F(Resource.Berries, 0.15, 0.12, 0.4),
        F(Resource.Roots, 0.3, 0.1, 0.25), F(Resource.PlantFiber, 0.6, 0.05, 0.12),
    ];
    static readonly IReadOnlyList<ForageItem> Rocky =
        [F(Resource.Stone, 2, 0.25, 0.7), F(Resource.Tinder, 0.2, 0.01, 0.04), F(Resource.Flint, 0.2, 0.1, 0.25)];
    static readonly IReadOnlyList<ForageItem> FrozenCreek = [F(Resource.Kindling, 1.5, 0.15, 0.4), F(Resource.Stone, 0.3, 0.2, 0.4)];
    // Old CaveLocationFactory.CreateForage; a mouth adds sticks and tinder blown in.
    static readonly IReadOnlyList<ForageItem> CaveFloor =
        [F(Resource.Stone, 1.0, 0.15, 0.45), F(Resource.Shale, 0.4, 0.15, 0.35), F(Resource.Flint, 0.15, 0.1, 0.25), F(Resource.Bone, 0.15, 0.1, 0.3), F(Resource.Pyrite, 0.03, 0.02, 0.08)];
    static readonly IReadOnlyList<ForageItem> CaveMouth = [.. CaveFloor, F(Resource.Kindling, 0.3, 0.1, 0.25), F(Resource.Tinder, 0.4, 0.01, 0.04)];
    static readonly IReadOnlyList<ForageItem> Barren = [F(Resource.Stone, 0.4, 0.1, 0.3), F(Resource.Tinder, 0.2, 0.01, 0.04)];

    /// Old GenerateRevealThreshold: exponential around the expected hours, capped at ten times.
    static double RevealAt(Rng rng, double expectedHours)
    {
        double u = rng.NextDouble();
        if (u == 0) u = double.Epsilon;
        return Math.Min(-Math.Log(u) * expectedHours, expectedHours * MaxRevealMultiplier);
    }

    static int Between(Rng rng, int min, int maxExclusive) => min + rng.NextInt(maxExclusive - min);
    static double Uniform(Rng rng, double min, double max) => min + rng.NextDouble() * (max - min);

    /// Several pieces of one resource, summed: stock is by weight.
    static double Pieces(Rng rng, int count, double min, double max)
    {
        double kg = 0;
        for (int i = 0; i < count; i++) kg += Uniform(rng, min, max);
        return kg;
    }

    // ---- environmental details (old EnvironmentalDetailFactory): quick finds, 15-30 minutes ----

    /// The details with nothing to take (tracks, scat, burrows...) only ever told a story; they
    /// are for moments (not built yet), so here they still take their turn in the draw but leave no pool.
    static IEnumerable<PoolDef> Details(Terrain terrain, Rng rng)
    {
        if (rng.NextDouble() < 0.65) yield break;
        string[] pool = terrain switch
        {
            Terrain.Forest => ["fallen_log", "hollow_tree", "bent_branches", "old_nest", "scattered_bones", "forest_puddle"],
            Terrain.Clearing => ["fallen_log", "old_nest", "dry_grass", "bent_branches", "forest_puddle"],
            Terrain.Plain => ["frozen_puddle", "dry_grass", "windswept_snow", "animal_burrow"],
            Terrain.Hills => ["stone_pile", "bent_branches", "lichen_rocks", "animal_burrow"],
            Terrain.Rock => ["stone_pile", "stone_pile", "lichen_rocks"],
            Terrain.Marsh => ["frozen_puddle", "dead_reeds", "bent_branches"],
            Terrain.Water => ["frozen_puddle", "dead_reeds", "ice_formation"],
            _ => [],
        };
        if (pool.Length == 0) yield break;
        int count = rng.NextDouble() < 0.5 ? 1 : 2;
        var shuffled = pool.ToList();
        for (int i = shuffled.Count - 1; i > 0; i--) { int j = rng.NextInt(i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
        foreach (var name in shuffled.Take(count))
        {
            double reveal = RevealAt(rng, 0.25 + rng.NextDouble() * 0.25);
            StockPool D(string display, int minutes, params StockItem[] items) =>
                new(display, StockKind.Detail, minutes, items, reveal, PlacementHeightM: 0.25);
            var detail = name switch
            {
                "fallen_log" => D("Fallen Log", 3, new StockItem(Resource.Kindling, 1, Pieces(rng, Between(rng, 2, 5), 0.2, 0.4))),
                "hollow_tree" => D("Hollow Tree", 4, new StockItem(Resource.Tinder, 1, Pieces(rng, Between(rng, 1, 3), 0.02, 0.05))),
                "stone_pile" => D("Stone Pile", 5, new StockItem(Resource.Stone, 1, Pieces(rng, Between(rng, 1, 4), 0.3, 0.6))),
                "scattered_bones" => D("Scattered Bones", 3, new StockItem(Resource.Bone, 1, Pieces(rng, Between(rng, 1, 3), 0.1, 0.3))),
                "dry_grass" => D("Dry Grass Tussock", 2, new StockItem(Resource.PlantFiber, 1, Pieces(rng, Between(rng, 1, 3), 0.03, 0.07))),
                "dead_reeds" => D("Dead Reeds", 3, new StockItem(Resource.PlantFiber, 1, Pieces(rng, Between(rng, 2, 4), 0.02, 0.05))),
                "frozen_puddle" => D("Frozen Puddle", 5, new StockItem(Resource.Water, 1, Uniform(rng, 0.2, 0.5))),
                "forest_puddle" => D("Forest Puddle", 3, new StockItem(Resource.Water, 1, Uniform(rng, 0.3, 0.7))),
                "old_nest" => OldNest(rng, D),
                _ => null, // examine-only: nothing to take
            };
            if (detail != null) yield return detail;
        }
    }

    static StockPool OldNest(Rng rng, Func<string, int, StockItem[], StockPool> d)
    {
        var items = new List<StockItem>();
        if (rng.NextDouble() < 0.6) items.Add(new StockItem(Resource.Tinder, 1, Uniform(rng, 0.01, 0.03)));
        items.Add(new StockItem(Resource.PlantFiber, 1, Uniform(rng, 0.02, 0.05)));
        return d("Old Nest", 2, [.. items]);
    }

    // ---- discoveries (old DiscoveryGenerator terrain pools) ----

    /// One template: spawn chance, expected hours to find, and the pool it makes.
    sealed record Template(double SpawnChance, double ExpectedHours, Func<Rng, double, PoolDef>? Make);

    /// Old pools in their order. Templates with a null Make are small game and event triggers, which
    /// arrive with their systems; they still roll so the draw is the old one.
    static IReadOnlyList<Template> DiscoveryTable(Terrain terrain) => terrain switch
    {
        Terrain.Forest =>
        [
            new(0.08, 1.0, BerryBush), new(0.06, 1.5, MixedDeadfall), new(0.04, 2.0, Cattails), new(0.06, 1.5, null),
            new(0.03, 2.5, MedicinePatch), new(0.04, 1.5, ResinPocket), new(0.03, 2.0, AbandonedDen), new(0.03, 2.0, CharDeposit),
            new(0.02, 2.0, AntlerShed), new(0.02, 2.5, OldButcheringSite), new(0.01, 3.0, TallowPot), new(0.02, 2.0, DryWoodStack),
            new(0.02, 2.5, CordageBundle), new(0.02, 2.0, TinderCache), new(0.01, 3.5, MedicineStash), new(0.02, 3.0, MassiveDeadfall),
            new(0.01, 4.0, RockOverhang), new(0.02, 3.5, AbandonedCamp), new(0.02, 4.0, TrapperStash), new(0.02, 3.0, HuntersBlind),
            new(0.01, 4.0, null), new(0.02, 3.0, null), new(0.02, 3.0, null), new(0.02, 2.0, null), new(0.03, 2.0, null),
        ],
        Terrain.Clearing =>
        [
            new(0.09, 0.8, BerryBush), new(0.05, 1.2, MixedDeadfall), new(0.05, 1.2, null), new(0.03, 2.5, MedicinePatch),
            new(0.03, 2.0, CharDeposit), new(0.01, 2.5, OldButcheringSite), new(0.02, 2.0, DryWoodStack),
            new(0.02, 3.5, AbandonedCamp), new(0.02, 3.0, HuntersBlind),
        ],
        Terrain.Plain =>
        [
            new(0.04, 2.0, BonePile), new(0.03, 2.5, MeltwaterPool), new(0.05, 1.5, null), new(0.03, 2.0, CharDeposit),
            new(0.02, 2.0, null), new(0.02, 2.5, OldButcheringSite), new(0.02, 3.0, HuntersBlind), new(0.02, 5.0, null), new(0.02, 3.0, null),
        ],
        Terrain.Hills =>
        [
            new(0.05, 2.5, FlintOutcrop), new(0.04, 2.0, null), new(0.03, 2.0, AbandonedDen), new(0.02, 2.0, null),
            new(0.03, 1.5, AntlerShed), new(0.02, 2.0, KnappingScatter), new(0.01, 3.5, MedicineStash), new(0.03, 3.0, RockOverhang),
            new(0.02, 3.5, IceCave), new(0.02, 4.0, PyriteSeam), new(0.02, 3.5, HiddenSpring), new(0.03, 4.0, null),
            new(0.02, 5.0, null), new(0.02, 3.5, null),
        ],
        Terrain.Rock =>
        [
            new(0.06, 2.0, FlintOutcrop), new(0.03, 2.0, BonePile), new(0.03, 2.0, null), new(0.03, 2.0, AbandonedDen),
            new(0.03, 2.0, KnappingScatter), new(0.02, 2.5, TallowPot), new(0.02, 2.0, TinderCache), new(0.03, 2.5, RockOverhang),
            new(0.02, 3.5, IceCave), new(0.02, 3.5, HiddenSpring),
        ],
        Terrain.Marsh =>
        [
            new(0.08, 1.0, Cattails), new(0.06, 1.5, MarshWater), new(0.06, 1.0, null), new(0.03, 2.5, MedicinePatch),
            new(0.02, 2.0, CordageBundle), new(0.01, 3.5, MedicineStash), new(0.02, 4.0, TrapperStash), new(0.02, 2.5, null),
        ],
        Terrain.Water => [new(0.08, 1.0, IceSource), new(0.04, 2.0, null), new(0.02, 4.0, HiddenSpring), new(0.02, 3.0, null)],
        _ => [],
    };

    /// Old DiscoveryGenerator.CavePool.
    static readonly IReadOnlyList<Template> CaveTable =
    [
        new(0.12, 0.8, FlintOutcrop), new(0.06, 1.2, PyriteSeam), new(0.16, 0.5, BonePile), new(0.06, 0.6, CharDeposit),
        new(0.05, 0.8, KnappingScatter), new(0.06, 0.7, CaveSeep), new(0.05, 1.0, SleepingRecess), new(0.12, 0.4, LooseShale),
    ];

    // Old DiscoveryFeatureFactory natural shelters and CaveLocationFactory.CreateSleepingRecess. The old
    // game drew their levels from the process-wide RNG; here they come from the tile's stream.
    // The old rolls run past the RockOverhang kind's caps (0.40), so a find's caps are at least its
    // own levels: work on it never clamps a good overhang down to the kind's.
    static ShelterFind RockOverhang(Rng r, double _)
    {
        double insulation = 0.3 + r.NextDouble() * 0.3, wind = 0.4 + r.NextDouble() * 0.3;
        var kind = Shelters.Kinds[ShelterKind.RockOverhang];
        return new(new ShelterDef("Rock Overhang", ShelterKind.RockOverhang, insulation, 0.8, wind,
            Math.Max(kind.InsulationCap, insulation), Math.Max(kind.OverheadCap, 0.8), Math.Max(kind.WindCap, wind)), 0);
    }

    static ShelterFind IceCave(Rng r, double _)
    {
        double insulation = 0.25 + r.NextDouble() * 0.15, wind = 0.7 + r.NextDouble() * 0.2;
        return new(new ShelterDef("Ice Cave", ShelterKind.Cave, insulation, 0.95, wind, 0.45, 0.98, 0.95), 0);
    }

    static ShelterFind SleepingRecess(Rng r, double _) => new(new ShelterDef("Dry Sleeping Recess", ShelterKind.Cave), 0);

    static IEnumerable<PoolDef> CaveDiscoveries(Rng rng) => Roll(CaveTable, rng);

    static IEnumerable<PoolDef> Discoveries(Terrain terrain, Rng rng) => Roll(DiscoveryTable(terrain), rng);

    static IEnumerable<PoolDef> Roll(IReadOnlyList<Template> table, Rng rng)
    {
        foreach (var t in table)
        {
            if (rng.NextDouble() >= t.SpawnChance) continue;
            var made = t.Make?.Invoke(rng, t.ExpectedHours);
            double reveal = RevealAt(rng, t.ExpectedHours);
            switch (made)
            {
                case StockPool stock: yield return stock with { RevealAtHours = reveal }; break;
                case ShelterFind find: yield return find with { RevealAtHours = reveal }; break;
                case null: break;
                default: throw new InvalidOperationException($"No reveal for a {made.GetType().Name}");
            }
        }
    }

    static StockPool H(string name, int minutes, params StockItem[] items) => new(name, StockKind.Harvest, minutes, items, 0);
    static StockPool S(string name, int minutes, IReadOnlyList<StockItem> items, IReadOnlyList<Gear>? gear = null) =>
        new(name, StockKind.Salvage, minutes, items, 0, PlacementHeightM: 0.3, Gear: gear);

    // Old FeatureFactory and DiscoveryFeatureFactory harvestables.
    static StockPool BerryBush(Rng r, double _) => H("Wild Berry Bush", 5, new StockItem(Resource.Berries, 5, 0.4, 168));
    static StockPool Cattails(Rng r, double _) => H("Cattail Stand", 10, new StockItem(Resource.PlantFiber, 10, 0.1, 168), new StockItem(Resource.Tinder, 6, 0.03, 168));
    static StockPool MixedDeadfall(Rng r, double _) =>
        H("Fallen Tree", 10, new StockItem(Resource.Pine, 4, 2.5, 720), new StockItem(Resource.Kindling, 8, 0.3, 168), new StockItem(Resource.Tinder, 3, 0.05, 168));
    static StockPool MassiveDeadfall(Rng r, double _) =>
        H("Massive Fallen Pine", 20, new StockItem(Resource.Pine, 8, 2.5), new StockItem(Resource.Kindling, 15, 0.3), new StockItem(Resource.Tinder, 5, 0.05));
    static StockPool MeltwaterPool(Rng r, double _) => H("Glacial Pool", 3, new StockItem(Resource.Water, 20, 1.0, 24));
    static StockPool MarshWater(Rng r, double _) => H("Open Water", 3, new StockItem(Resource.Water, 30, 1.0, 6));
    static StockPool IceSource(Rng r, double _) => H("Creek Ice", 5, new StockItem(Resource.Water, 20, 1.0, 48));
    /// Old WaterFeature.AsOpenWater: water that never freezes. A source as generous as a river.
    static StockPool HiddenSpring(Rng r, double _) => H("Hidden Spring", 1, new StockItem(Resource.Water, 100, 1.0, 0.1));
    /// Old CaveLocationFactory.CreateSeep: open water, fishless, from deep in the rock.
    static StockPool CaveSeep(Rng r, double _) => H("Cave Seep", 1, new StockItem(Resource.Water, 100, 1.0, 0.1));
    static StockPool LooseShale(Rng r, double _) => H("Loose Shale", 5, new StockItem(Resource.Shale, 4, 0.25));
    static StockPool FlintOutcrop(Rng r, double _) => H("Flint Outcrop", 5, new StockItem(Resource.Flint, 3 + r.NextInt(5), 0.15));
    static StockPool PyriteSeam(Rng r, double _) => H("Iron Pyrite Seam", 8, new StockItem(Resource.Pyrite, 2 + r.NextInt(4), 0.08));
    static StockPool BonePile(Rng r, double _) => H("Old Bones", 3, new StockItem(Resource.Bone, 2 + r.NextInt(4), 0.2));
    static StockPool ResinPocket(Rng r, double _) => H("Resin-Weeping Pine", 5, new StockItem(Resource.PineResin, 2 + r.NextInt(4), 0.05, 168));
    static StockPool CharDeposit(Rng r, double _) => H("Old Fire Scar", 5, new StockItem(Resource.Charcoal, 3 + r.NextInt(5), 0.08));
    static StockPool AntlerShed(Rng r, double _) => H("Shed Antlers", 5, new StockItem(Resource.Bone, 2 + r.NextInt(3), 0.4));

    static StockPool MedicinePatch(Rng r, double _)
    {
        var items = new List<StockItem> { new StockItem(Resource.BirchPolypore, 3 + r.NextInt(3), 0.1, 336) };
        if (r.NextDouble() < 0.5) items.Add(new StockItem(Resource.WillowBark, 2 + r.NextInt(2), 0.08, 336));
        if (r.NextDouble() < 0.4) items.Add(new StockItem(Resource.Usnea, 2 + r.NextInt(2), 0.05, 336));
        return H("Herb Patch", 8, [.. items]);
    }

    static StockPool AbandonedDen(Rng r, double _)
    {
        var items = new List<StockItem> { new StockItem(Resource.Bone, 2 + r.NextInt(3), 0.2) };
        if (r.NextDouble() < 0.4) items.Add(new StockItem(Resource.Hide, 1, 0.15));
        return H("Old Den", 10, [.. items]);
    }

    static StockPool OldButcheringSite(Rng r, double _)
    {
        var items = new List<StockItem> { new StockItem(Resource.Bone, 2 + r.NextInt(2), 0.15) };
        if (r.NextDouble() < 0.7) items.Add(new StockItem(Resource.Sinew, 1 + r.NextInt(2), 0.05));
        if (r.NextDouble() < 0.4) items.Add(new StockItem(Resource.Hide, 1, 0.2));
        return H("Old Kill Site", 8, [.. items]);
    }

    static StockPool KnappingScatter(Rng r, double _)
    {
        var items = new List<StockItem> { new StockItem(Resource.Flint, 2 + r.NextInt(4), 0.12) };
        if (r.NextDouble() < 0.3) items.Add(new StockItem(Resource.Shale, 1, 0.2));
        return H("Knapping Scatter", 4, [.. items]);
    }

    static StockPool DryWoodStack(Rng r, double _)
    {
        int logs = 3 + r.NextInt(4), sticks = 4 + r.NextInt(5);
        var wood = r.NextInt(3) switch { 0 => Resource.Pine, 1 => Resource.Birch, _ => Resource.Oak };
        return H("Dry Wood Stack", 4, new(wood, logs, 1.5), new StockItem(Resource.Kindling, sticks, 0.15));
    }

    // Old salvage: everything comes at once, after the minutes.
    static StockItem All(Resource r, double kg) => new(r, 1, kg);

    static StockPool TallowPot(Rng r, double _) => S("Tallow Cache", 5, [All(Resource.Tallow, (1 + r.NextInt(3)) * 0.25)]);

    static StockPool CordageBundle(Rng r, double _)
    {
        var items = new List<StockItem> { All(Resource.PlantFiber, (3 + r.NextInt(3)) * 0.08) };
        if (r.NextDouble() < 0.7) items.Add(All(Resource.Rope, (1 + r.NextInt(2)) * 0.15));
        return S("Cordage Bundle", 5, items);
    }

    static StockPool TinderCache(Rng r, double _)
    {
        var items = new List<StockItem> { All(Resource.BirchBark, (3 + r.NextInt(3)) * 0.03), All(Resource.PineResin, (2 + r.NextInt(2)) * 0.05) };
        if (r.NextDouble() < 0.3) items.Add(All(Resource.Amadou, (1 + r.NextInt(2)) * 0.02));
        return S("Tinder Cache", 5, items);
    }

    static StockPool MedicineStash(Rng r, double _)
    {
        var items = new List<StockItem> { All(Resource.BirchPolypore, (2 + r.NextInt(2)) * 0.08) };
        var medicines = new List<(Resource, double)>
        {
            (Resource.WillowBark, 0.06), (Resource.Chaga, 0.1), (Resource.Usnea, 0.04),
            (Resource.SphagnumMoss, 0.05), (Resource.RoseHip, 0.03), (Resource.JuniperBerry, 0.03),
        };
        for (int i = medicines.Count - 1; i > 0; i--) { int j = r.NextInt(i + 1); (medicines[i], medicines[j]) = (medicines[j], medicines[i]); }
        int count = 2 + r.NextInt(2);
        foreach (var (res, kg) in medicines.Take(count)) items.Add(All(res, (2 + r.NextInt(2)) * kg));
        return S("Medicine Stash", 10, items);
    }

    static StockPool AbandonedCamp(Rng r, double _) =>
        S("Abandoned Camp", 45, [All(Resource.Kindling, 0.6), All(Resource.Tinder, 0.05), All(Resource.PlantFiber, 0.1)],
            r.NextDouble() < 0.5 ? [Items.Make("worn_knife")] : null);

    static StockPool TrapperStash(Rng r, double _)
    {
        var items = new List<StockItem>
        {
            All(Resource.PlantFiber, Uniform(r, 0.15, 0.3)), All(Resource.Sinew, Uniform(r, 0.12, 0.25)), All(Resource.Kindling, Uniform(r, 0.5, 0.8)),
        };
        if (r.NextDouble() < 0.5) items.Add(All(Resource.Hide, Uniform(r, 0.3, 0.5)));
        if (r.NextDouble() < 0.3) items.Add(All(Resource.Rope, Uniform(r, 0.1, 0.2)));
        return S("Trapper's Stash", 25, items);
    }

    /// old game: the abandoned spear rolled its durability as 30-70% of an unlimited maximum (-1),
    /// which always came out 0, so every one was found broken (bug, fixed: found whole).
    static StockPool HuntersBlind(Rng r, double _)
    {
        var items = new List<StockItem> { All(Resource.Kindling, Uniform(r, 0.6, 1.0)) };
        List<Gear>? gear = r.NextDouble() < 0.5 ? [Items.Make("wooden_spear")] : null;
        if (r.NextDouble() < 0.5) items.Add(All(Resource.Bone, Uniform(r, 0.3, 0.5)));
        if (r.NextDouble() < 0.4) items.Add(All(Resource.Tinder, Uniform(r, 0.15, 0.3)));
        return S("Hunter's Blind", 20, items, gear);
    }

    // ---- state: deltas, derived forward lazily ----

    static TileState TileOf(World world, TileId tile)
    {
        if (!world.Tiles.TryGetValue(tile, out var state)) world.Tiles[tile] = state = new TileState();
        return state;
    }

    public static PoolState? StateOf(World world, PoolId id) =>
        world.Tiles.TryGetValue(id.Tile, out var t) && t.Pools?.TryGetValue(id.Index, out var s) == true ? s : null;

    static T StateFor<T>(World world, PoolId id, Func<T> create) where T : PoolState
    {
        var tile = TileOf(world, id.Tile);
        tile.Pools ??= [];
        if (!tile.Pools.TryGetValue(id.Index, out var s)) tile.Pools[id.Index] = s = create();
        return (T)s;
    }

    // forage

    /// Old ForageFeature.ResourceDensity: worked ground thins as 1/(1 + 2h) and regrows linearly
    /// toward its base over four weeks from the last search.
    public static double ForageDensity(World world, PoolId id)
    {
        var def = (ForagePool)Def(world, id);
        if (StateOf(world, id) is not ForageState s || s.HoursForaged <= 0) return def.Density;
        double depleted = def.Density / (1.0 + s.HoursForaged * 2);
        double hoursSince = (world.Minute - s.LastForagedMinute) / 60.0;
        return Math.Min(def.Density, depleted + hoursSince / ForageRespawnHours * (def.Density - depleted));
    }

    /// Grazing on one resource, 0-1, regrowing at the forage rate (old ForageFeature.Update).
    public static double GrazedLevel(World world, PoolId id, Resource r) =>
        StateOf(world, id) is ForageState s && s.Grazed.TryGetValue(r, out var g)
            ? Math.Max(0, g.Level - (world.Minute - g.Minute) / 60.0 / ForageRespawnHours) : 0;

    /// Sets a resource's grazing level now (herds, phase 3b; old ForageFeature.Graze does the arithmetic).
    public static void SetGrazed(World world, PoolId id, Resource r, double level) =>
        StateFor(world, id, () => new ForageState()).Grazed[r] = new Grazing(Math.Clamp(level, 0, 1), world.Minute);

    /// Old luck tiers, rolled once per forage call.
    static double LuckFactor(Rng rng) => rng.NextDouble() switch
    {
        < 0.15 => 0.5,
        < 0.75 => 1.0,
        < 0.95 => 1.5,
        < 0.99 => 2.0,
        _ => 3.0,
    };

    /// Old ForageFeature.Forage(hours): whole finds from the floor of the expected count, one more on
    /// the remainder's odds. Depletes only when something turned up. Returns the finds piece by piece.
    public static List<(Resource Resource, double Kg)> Forage(World world, PoolId id, double hours, Rng rng)
    {
        var def = (ForagePool)Def(world, id);
        double luck = LuckFactor(rng);
        double density = ForageDensity(world, id);
        var found = new List<(Resource, double)>();
        foreach (var item in def.Items)
        {
            double chance = density * item.Abundance * (1 - GrazedLevel(world, id, item.Resource)) * luck * hours;
            int whole = (int)Math.Floor(chance);
            for (int i = 0; i < whole; i++) found.Add((item.Resource, Uniform(rng, item.MinKg, item.MaxKg)));
            double remainder = chance - whole;
            if (remainder > 0 && rng.NextDouble() < remainder) found.Add((item.Resource, Uniform(rng, item.MinKg, item.MaxKg)));
        }
        if (found.Count > 0)
        {
            var s = StateFor(world, id, () => new ForageState());
            // Recover first: regrowth since the last search is earned, then this search thins it again.
            s.HoursForaged = Recovered(world, id, def, s) + hours;
            s.LastForagedMinute = world.Minute;
        }
        return found;
    }

    /// The hours-foraged figure that gives today's density, so a search after regrowth starts from
    /// the regrown ground. old game: kept the raw total and added a respawn bonus on top, which a new
    /// search wiped out at once - one minute's find re-thinned a month of regrowth (bug, fixed).
    static double Recovered(World world, PoolId id, ForagePool def, ForageState s)
    {
        double density = ForageDensity(world, id);
        return density >= def.Density ? 0 : (def.Density / density - 1) / 2;
    }

    // trees

    public static bool HasTrees(World world, PoolId id)
    {
        var def = (TreePool)Def(world, id);
        if (def.Trees is not int max) return true;
        var s = StateOf(world, id) as TreeState;
        return Standing(world, def, s, max) > 0;
    }

    /// Old WoodedAreaFeature.Update only regrew while the stand was cleared, and stopped as soon as
    /// one tree stood: a cleared stand comes back one tree at a time, a week each.
    static int Standing(World world, TreePool def, TreeState? s, int max)
    {
        int left = max - (s?.Felled ?? 0);
        if (left > 0 || s == null) return left;
        return world.Minute - s.ClearedMinute >= def.RespawnHoursPerTree * 60 ? 1 : 0;
    }

    public static double ChopProgressPct(World world, PoolId id) =>
        (StateOf(world, id) as TreeState)?.MinutesWorked / ((TreePool)Def(world, id)).MinutesToFell ?? 0;

    /// Adds chopping minutes; returns the tree's yield if it came down (old FellTree).
    public static Inventory? Chop(World world, PoolId id, double minutes, Rng rng)
    {
        var def = (TreePool)Def(world, id);
        var s = StateFor(world, id, () => new TreeState());
        s.MinutesWorked += minutes;
        if (s.MinutesWorked < def.MinutesToFell - 1e-9) return null;

        var log = def.Wood ?? rng.NextDouble() switch { < 0.4 => Resource.Pine, < 0.75 => Resource.Birch, _ => Resource.Oak };
        double logKg = log == Resource.Oak ? 4.0 : log == Resource.Pine ? 2.5 : 2.0;
        var y = new Inventory();
        int logs = Between(rng, 8, 11);
        for (int i = 0; i < logs; i++) Items.Add(y, log, logKg * (0.9 + rng.NextDouble() * 0.2));
        int sticks = Between(rng, 4, 7);
        for (int i = 0; i < sticks; i++) Items.Add(y, Resource.Kindling, 0.2 + rng.NextDouble() * 0.3);
        int tinder = Between(rng, 2, 4);
        for (int i = 0; i < tinder; i++) Items.Add(y, Resource.Tinder, 0.03 + rng.NextDouble() * 0.05);

        s.MinutesWorked = 0;
        if (def.Trees is int max)
        {
            if (Standing(world, def, s, max) <= 0) throw new InvalidOperationException($"Pool {id} has no tree to fell");
            if (s.Felled >= max) s.ClearedMinute = world.Minute; // the regrown one, felled: the clock starts again
            else if (++s.Felled >= max) s.ClearedMinute = world.Minute;
        }
        return y;
    }

    // stock

    /// Units of each item now, regrowth included (old HarvestableFeature.Update, derived).
    public static int[] UnitsNow(World world, PoolId id)
    {
        var def = (StockPool)Def(world, id);
        if (StateOf(world, id) is not StockState s) return def.Items.Select(i => i.Units).ToArray();
        var units = new int[def.Items.Count];
        for (int i = 0; i < units.Length; i++)
        {
            var item = def.Items[i];
            // old game: a respawn time of 0 meant "never" but refilled the stock every tick (bug, fixed).
            int regrown = item.RespawnHoursPerUnit > 0 ? (int)Math.Floor((world.Minute - s.LastTakenMinute[i]) / 60.0 / item.RespawnHoursPerUnit) : 0;
            units[i] = (int)Math.Min(item.Units, (long)s.Units[i] + regrown);
        }
        return units;
    }

    public static bool HasStock(World world, PoolId id) => UnitsNow(world, id).Any(u => u > 0);

    /// Minutes of work on a stock; every full cycle takes one unit of each item left (old
    /// HarvestableFeature.Harvest). Gear in a salvage comes with the first cycle.
    public static Inventory WorkStock(World world, PoolId id, double minutes)
    {
        var def = (StockPool)Def(world, id);
        var now = UnitsNow(world, id);
        var s = StateFor(world, id, () => new StockState { Units = (int[])now.Clone(), LastTakenMinute = new long[def.Items.Count] });
        // Regrowth UnitsNow counted goes into Units, and its clock moves past it, so it counts once.
        for (int i = 0; i < now.Length; i++)
        {
            if (now[i] == s.Units[i]) continue;
            s.LastTakenMinute[i] = now[i] >= def.Items[i].Units ? world.Minute
                : s.LastTakenMinute[i] + (long)Math.Round((now[i] - s.Units[i]) * def.Items[i].RespawnHoursPerUnit * 60);
            s.Units[i] = now[i];
        }
        var got = new Inventory();
        s.MinutesWorked += minutes;
        while (s.MinutesWorked >= def.MinutesPerUnit - 1e-9 && now.Any(u => u > 0))
        {
            s.MinutesWorked -= def.MinutesPerUnit;
            for (int i = 0; i < now.Length; i++)
            {
                if (now[i] <= 0) continue;
                Items.Add(got, def.Items[i].Resource, def.Items[i].KgPerUnit);
                now[i]--;
                s.LastTakenMinute[i] = world.Minute;
            }
            if (!s.GearTaken && def.Gear is { Count: > 0 } gear)
            {
                got.Gear.AddRange(gear.Select(g => new Gear { Kind = g.Kind, Durability = g.Durability, MaxDurability = g.MaxDurability }));
                s.GearTaken = true;
            }
        }
        for (int i = 0; i < now.Length; i++) s.Units[i] = now[i];
        if (!now.Any(u => u > 0)) s.MinutesWorked = 0;
        return got;
    }

    // ---- discovery: the agent's knowledge, not the tile's ----

    /// Whether this pool needs finding before it can be worked: forage, timber and a named site's
    /// stock in plain sight (RevealAtHours 0) are there to see; every other stock, and every shelter
    /// find, is hidden.
    public static bool IsHidden(PoolDef def) => def is StockPool { RevealAtHours: > 0 } or ShelterFind;

    static (double RevealAtHours, double PlacementHeightM) Hiding(PoolDef def) => def switch
    {
        StockPool s => (s.RevealAtHours, s.PlacementHeightM),
        ShelterFind f => (f.RevealAtHours, f.PlacementHeightM),
        _ => throw new InvalidOperationException($"{def.Name} is not hidden"),
    };

    public static bool Knows(World world, AgentId who, PoolId id) =>
        !IsHidden(Def(world, id)) || (world.Agent(who).Mind as PersonMind)?.Knows.Discovered.Contains(id) == true;

    /// Old Location.RevealDiscoveries: search effort reaches every hidden pool on the tile, slowed per
    /// pool by how far snow covers it; a pool is found when its effort passes its threshold.
    public static void Search(World world, Agent agent, double perceptionWeightedHours, List<WorldEvent> events)
    {
        if (agent.Mind is not PersonMind mind) return;
        var pools = At(world, agent.Tile);
        for (int i = 0; i < pools.Count; i++)
        {
            if (!IsHidden(pools[i])) continue;
            var id = new PoolId(agent.Tile, i);
            if (mind.Knows.Discovered.Contains(id)) continue;
            var (revealAt, height) = Hiding(pools[i]);
            double hours = mind.Knows.SearchHours.GetValueOrDefault(id)
                + perceptionWeightedHours * Ground.SearchFactor(world, agent.Tile, 0, height);
            if (hours >= revealAt)
            {
                mind.Knows.SearchHours.Remove(id);
                mind.Knows.Discovered.Add(id);
                events.Add(new PoolDiscovered(world.Minute, agent.Id, id, pools[i].Name));
                if (pools[i] is ShelterFind find) Shelters.Found(world, id, find, events);
            }
            else mind.Knows.SearchHours[id] = hours;
        }
    }
}
