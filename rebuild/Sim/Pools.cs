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

/// What a discovery or detail makes (Content/discoveries.json): a stock (Work, Minutes, Items) or a
/// natural shelter (Shelter, levels with spreads, caps).
public sealed record FindDef(string Name, StockKind? Work = null, int Minutes = 0, IReadOnlyList<FindItem>? Items = null,
    ShelterKind? Shelter = null, double? Insulation = null, double InsulationSpread = 0, double? Overhead = null, double OverheadSpread = 0,
    double? Wind = null, double WindSpread = 0, double? InsulationCap = null, double? OverheadCap = null, double? WindCap = null);

/// One item of a find, rolled in order: chance, units in [Units, UnitsMax], each unit's kg in
/// [KgPerUnit, KgPerUnitMax]. A harvest's items state RespawnHoursPerUnit (0 never regrows); no other's do. Exactly one of Resource, AnyOf (drawn after every count), Gear or Shuffle
/// (Take to TakeMax of these, shuffled first).
public sealed record FindItem(Resource? Resource = null, IReadOnlyList<Resource>? AnyOf = null, string? Gear = null,
    IReadOnlyList<FindItem>? Shuffle = null, int Take = 0, int? TakeMax = null, double Chance = 1,
    int Units = 1, int? UnitsMax = null, double KgPerUnit = 0, double? KgPerUnitMax = null, double? RespawnHoursPerUnit = null);

/// One entry of a discovery draw: a find, or a place held for a system not built yet (WaitsFor).
public sealed record DiscoveryDraw(double SpawnChance, double ExpectedHours, string? Find = null, string? WaitsFor = null);

/// The discovery tables (Content/discoveries.json).
public sealed record DiscoveryTables(IReadOnlyDictionary<string, FindDef> Finds, IReadOnlyDictionary<string, FindDef> Details,
    IReadOnlyDictionary<Terrain, IReadOnlyList<DiscoveryDraw>> Terrains, IReadOnlyList<DiscoveryDraw> Cave,
    IReadOnlyDictionary<Terrain, IReadOnlyList<string>> DetailDraws, IReadOnlyList<string> ExamineOnly);

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
            var forage = m.CaveAt(tile).Row().Forage;
            pools.Add(new ForagePool("forage", forage.Density, ForageTables[forage.Table]));
            pools.AddRange(CaveDiscoveries(rng));
            return pools;
        }
        pools.Add(TerrainForage(terrain, rng));
        if (terrain.Row().Trees is TreeRow trees) pools.Add(new TreePool(trees.Name, null, trees.MinutesToFell));
        pools.AddRange(Details(terrain, rng));
        pools.AddRange(Discoveries(terrain, rng));
        return pools;
    }

    /// Old FeatureFactory.CreateTerrainForage: a terrain's table at a density drawn around its base.
    static ForagePool TerrainForage(Terrain terrain, Rng rng)
    {
        var forage = terrain.Row().Forage ?? throw new InvalidOperationException($"{terrain} has no forage");
        // old game: drew this from the process-wide RNG, so the same seed gave different tiles (bug, fixed: seeded by tile).
        return new ForagePool("forage", Math.Clamp(Normal(rng, forage.Density, 0.32), 0.1, 2.0), ForageTables[forage.Table]);
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

    /// The forage tables, by name (Content/forage.json; old FeatureFactory forage tables).
    static readonly IReadOnlyDictionary<string, IReadOnlyList<ForageItem>> ForageTables = Content.Tables.Forage;
    /// The camp's thin mixed forage, and the deep woods'.
    internal static readonly IReadOnlyList<ForageItem> MixedForest = ForageTables["MixedForest"];

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

    // ---- finds: discoveries and details (Content/discoveries.json) ----

    static readonly DiscoveryTables Finds = Content.Tables.Discoveries;

    /// Old EnvironmentalDetailFactory: quick finds, 15-30 minutes. The details with nothing to take
    /// (tracks, scat, burrows...) only ever told a story; they are for moments (not built yet), so here
    /// they still take their turn in the draw but leave no pool.
    static IEnumerable<PoolDef> Details(Terrain terrain, Rng rng)
    {
        if (rng.NextDouble() < 0.65) yield break;
        var pool = Finds.DetailDraws.GetValueOrDefault(terrain) ?? [];
        if (pool.Count == 0) yield break;
        int count = rng.NextDouble() < 0.5 ? 1 : 2;
        var shuffled = pool.ToList();
        for (int i = shuffled.Count - 1; i > 0; i--) { int j = rng.NextInt(i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
        foreach (var name in shuffled.Take(count))
        {
            double reveal = RevealAt(rng, 0.25 + rng.NextDouble() * 0.25);
            if (Finds.Details.TryGetValue(name, out var detail)) yield return Make(detail, rng, reveal);
        }
    }

    static IEnumerable<PoolDef> CaveDiscoveries(Rng rng) => Roll(Finds.Cave, rng);

    static IEnumerable<PoolDef> Discoveries(Terrain terrain, Rng rng) => Roll(Finds.Terrains.GetValueOrDefault(terrain) ?? [], rng);

    /// Old DiscoveryGenerator: each draw on its spawn chance, found after an exponential share of its
    /// expected hours. A draw that waits for a system not built yet still rolls, so the draw is the old one.
    static IEnumerable<PoolDef> Roll(IReadOnlyList<DiscoveryDraw> table, Rng rng)
    {
        foreach (var t in table)
        {
            if (rng.NextDouble() >= t.SpawnChance) continue;
            var made = t.Find is string id ? Make(Finds.Finds[id], rng, 0) : null;
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

    /// How high a find stands, for how far snow buries it: a bush, a salvage pile, a detail on the ground.
    static double PlacementHeightM(StockKind kind) => kind switch { StockKind.Salvage => 0.3, StockKind.Detail => 0.25, _ => 0.6 };

    /// One find made from its definition, its items rolled in order (Content/discoveries.json says how).
    static PoolDef Make(FindDef d, Rng rng, double revealAtHours)
    {
        if (d.Shelter is ShelterKind kind) return new ShelterFind(ShelterOf(d, kind, rng), revealAtHours);
        var work = d.Work ?? throw new InvalidOperationException($"{d.Name} is neither stock nor shelter");
        var items = new List<StockItem>();
        var gear = new List<Gear>();
        var choices = new List<(int Index, IReadOnlyList<Resource> AnyOf)>();
        void Take(FindItem i)
        {
            if (i.Chance < 1 && rng.NextDouble() >= i.Chance) return;
            if (i.Gear != null) { gear.Add(Items.Make(i.Gear)); return; }
            if (i.Shuffle != null)
            {
                var of = i.Shuffle.ToList();
                for (int k = of.Count - 1; k > 0; k--) { int j = rng.NextInt(k + 1); (of[k], of[j]) = (of[j], of[k]); }
                int take = i.TakeMax is int most ? Between(rng, i.Take, most + 1) : i.Take;
                foreach (var x in of.Take(take)) Take(x);
                return;
            }
            int units = i.UnitsMax is int max ? Between(rng, i.Units, max + 1) : i.Units;
            if (i.AnyOf != null) choices.Add((items.Count, i.AnyOf));
            var resource = i.Resource ?? i.AnyOf![0]; // drawn below, after every count
            // Old salvage and details: everything comes at once, after the minutes.
            items.Add(work == StockKind.Harvest ? new StockItem(resource, units, i.KgPerUnit, i.RespawnHoursPerUnit!.Value)
                : All(resource, i.KgPerUnitMax is double hi ? Pieces(rng, units, i.KgPerUnit, hi) : units * i.KgPerUnit));
        }
        foreach (var i in d.Items ?? []) Take(i);
        foreach (var (index, anyOf) in choices) items[index] = items[index] with { Resource = anyOf[rng.NextInt(anyOf.Count)] };
        return new StockPool(d.Name, work, d.Minutes, items, revealAtHours, PlacementHeightM(work), gear.Count > 0 ? gear : null);
    }

    /// A natural shelter's levels: a level with a spread is rolled up to it. A cap left out is the kind's,
    /// and a find's caps are at least its own levels (the old overhang rolls ran past the kind's caps),
    /// so work on it never clamps a good one down.
    static ShelterDef ShelterOf(FindDef d, ShelterKind kind, Rng rng)
    {
        var k = Shelters.Kinds[kind];
        double? Level(double? level, double spread) => level is double l && spread > 0 ? l + rng.NextDouble() * spread : level;
        static double? Cap(double? level, double? cap, double kindCap) => level is double l ? Math.Max(cap ?? kindCap, l) : cap;
        double? insulation = Level(d.Insulation, d.InsulationSpread), overhead = Level(d.Overhead, d.OverheadSpread), wind = Level(d.Wind, d.WindSpread);
        return new ShelterDef(d.Name, kind, insulation, overhead, wind,
            Cap(insulation, d.InsulationCap, k.InsulationCap), Cap(overhead, d.OverheadCap, k.OverheadCap), Cap(wind, d.WindCap, k.WindCap));
    }

    static StockItem All(Resource r, double kg) => new(r, 1, kg);

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

    /// What every felled tree gives (Content/terrain.json felling).
    public static readonly Felling Felled = Content.Tables.Felling;

    /// Adds chopping minutes; returns the tree's yield if it came down (old FellTree).
    public static Inventory? Chop(World world, PoolId id, double minutes, Rng rng)
    {
        var def = (TreePool)Def(world, id);
        var s = StateFor(world, id, () => new TreeState());
        s.MinutesWorked += minutes;
        if (s.MinutesWorked < def.MinutesToFell - 1e-9) return null;

        var f = Felled;
        var log = def.Wood ?? f.MixedWoodAt(rng.NextDouble());
        double logKg = f.LogKg[log], spread = f.LogKgSpreadPct;
        var y = new Inventory();
        int logs = Between(rng, f.Logs.Count[0], f.Logs.Count[1] + 1);
        for (int i = 0; i < logs; i++) Items.Add(y, log, logKg * ((1 - spread) + rng.NextDouble() * (2 * spread)));
        foreach (var (resource, pieces) in new[] { (Resource.Kindling, f.Kindling), (Resource.Tinder, f.Tinder) })
        {
            int n = Between(rng, pieces.Count[0], pieces.Count[1] + 1);
            for (int i = 0; i < n; i++) Items.Add(y, resource, pieces.Kg![0] + rng.NextDouble() * (pieces.Kg[1] - pieces.Kg[0]));
        }

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
