using System.Text.Json.Serialization;

namespace Survival;

/// Something on a tile that work draws from: forage, timber, harvestables, salvage and details, as
/// one concept. Generated from (seed, tile), never saved: only what has been drawn from is a delta,
/// in TileState.Pools.
public abstract record PoolDef(string Name);

/// Ambient searching: finds per hour = density x abundance.
public sealed record ForagePool(string Name, double Density, IReadOnlyList<ForageItem> Items) : PoolDef(Name);
public sealed record ForageItem(Resource Resource, double Abundance, double MinKg, double MaxKg);

/// Standing timber. Null wood is mixed forest; null trees is unlimited.
public sealed record TreePool(string Name, Resource? Wood, double MinutesToFell, int? Trees = null, double RespawnHoursPerTree = 168) : PoolDef(Name);

/// Harvestables, salvage and details. Each MinutesPerUnit cycle yields one unit of every item
/// still there; salvage and details are one cycle holding everything.
public sealed record StockPool(
    string Name, StockKind Kind, int MinutesPerUnit, IReadOnlyList<StockItem> Items, double RevealAtHours,
    double PlacementHeightM = 0.6, IReadOnlyList<Gear>? Gear = null, IReadOnlyList<string>? Found = null) : PoolDef(Name);
/// PieceKgs: a salvage's or a pile's pieces as rolled, one unit of them all; null for a harvest (a unit is a piece).
/// RequiresTool: only someone carrying it works this item (a wood find's firewood needs an axe); the rest come by hand.
public sealed record StockItem(Resource Resource, int Units, double KgPerUnit, double RespawnHoursPerUnit = 0, IReadOnlyList<double>? PieceKgs = null,
    ToolType? RequiresTool = null)
{
    public bool Equals(StockItem? other) => other is not null && Resource == other.Resource && Units == other.Units && KgPerUnit == other.KgPerUnit
        && RespawnHoursPerUnit == other.RespawnHoursPerUnit && (PieceKgs ?? []).SequenceEqual(other.PieceKgs ?? []) && RequiresTool == other.RequiresTool;

    public override int GetHashCode() => HashCode.Combine(Resource, Units, KgPerUnit, RespawnHoursPerUnit, RequiresTool);
}

/// A natural shelter, nothing to gather: finding it puts the shelter on the tile (Pools.Search).
public sealed record ShelterFind(ShelterDef Shelter, double RevealAtHours, double PlacementHeightM = 2.0, IReadOnlyList<string>? Found = null) : PoolDef(Shelter.Name);

/// A dead animal, nothing to gather from the pool itself: finding it lays the carcass on the tile (Pools.Search).
public sealed record CarcassFind(string Name, Species Animal, double HarvestedPct, double RevealAtHours, IReadOnlyList<string>? Found = null,
    double PlacementHeightM = 0.4) : PoolDef(Name);

/// Something somebody hid: finding it puts what it holds on the tile as a pile (Pools.Search).
public sealed record PileFind(string Name, IReadOnlyList<StockItem> Items, IReadOnlyList<Gear> Gear, double RevealAtHours, IReadOnlyList<string>? Found = null,
    double PlacementHeightM = 0.3) : PoolDef(Name);

/// How a stock is worked: harvest (no impairment check), salvage (slowed by bad legs), detail (a light look).
public enum StockKind { Harvest, Salvage, Detail }

/// What a discovery or detail makes (Content/discoveries.json): a stock, a natural shelter, a carcass or a pile.
/// Loot: a salvage's or a pile's contents as a loot table (loot.json), in place of Items. Found: the lines read on finding it.
/// Outcomes: what a salvage or a pile may turn out to hold, one drawn by weight, in place of Loot.
public sealed record FindDef(string Name, StockKind? Work = null, int Minutes = 0, IReadOnlyList<FindItem>? Items = null,
    ShelterKind? Shelter = null, double? Insulation = null, double InsulationSpread = 0, double? Overhead = null, double OverheadSpread = 0,
    double? Wind = null, double WindSpread = 0, double? InsulationCap = null, double? OverheadCap = null, double? WindCap = null,
    string? Loot = null, CarcassRow? Carcass = null, bool Pile = false, IReadOnlyList<string>? Found = null,
    IReadOnlyList<FindOutcome>? Outcomes = null);

/// One way a find can turn out: its loot tables rolled together (none: nothing there), each scaled by Scale (kilos and
/// how many it picks), told by Line after the find's found line.
public sealed record FindOutcome(double Weight, string Line, IReadOnlyList<string> Loot, double Scale = 1);

public sealed record CarcassRow(IReadOnlyList<AnimalWeight> Animals, IReadOnlyList<double> HarvestedPct);
public sealed record AnimalWeight(Species Animal, double Weight);

/// One item of a find, rolled in order. Only a harvest's items state RespawnHoursPerUnit (0 never regrows).
/// Exactly one of Resource, AnyOf (one draw for every AnyOf of the find, after every count), Gear or Shuffle (Take to TakeMax, shuffled first).
public sealed record FindItem(Resource? Resource = null, IReadOnlyList<Resource>? AnyOf = null, string? Gear = null,
    IReadOnlyList<FindItem>? Shuffle = null, int Take = 0, int? TakeMax = null, double Chance = 1,
    int Units = 1, int? UnitsMax = null, double KgPerUnit = 0, double? KgPerUnitMax = null, double? RespawnHoursPerUnit = null,
    ToolType? RequiresTool = null);

/// One entry of a discovery draw: a find, or a place held for a system not built yet (WaitsFor).
public sealed record DiscoveryDraw(double SpawnChance, double ExpectedHours, string? Find = null, string? WaitsFor = null);

/// A find no map has fewer than AtLeast of: what the cave draws left short is dealt onto bare cave floors, found after about ExpectedHours of searching.
public sealed record DealtFind(string Find, int AtLeast, double ExpectedHours);

public sealed record DiscoveryTables(IReadOnlyDictionary<string, FindDef> Finds, IReadOnlyDictionary<string, FindDef> Details,
    IReadOnlyDictionary<Terrain, IReadOnlyList<DiscoveryDraw>> Terrains, IReadOnlyList<DiscoveryDraw> Cave, IReadOnlyList<DealtFind> CavePerMap,
    IReadOnlyDictionary<Terrain, IReadOnlyList<string>> DetailDraws, IReadOnlyList<string> ExamineOnly);

/// What drawing from a pool has changed. Only pools something has worked are stored.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ForageState), "forage")]
[JsonDerivedType(typeof(TreeState), "trees")]
[JsonDerivedType(typeof(StockState), "stock")]
[JsonDerivedType(typeof(LaidState), "laid")]
public abstract class PoolState;

public sealed class ForageState : PoolState
{
    public double HoursForaged { get; set; }
    public long LastForagedMinute { get; set; }
    /// Which draw of clues the ground shows now (Clues.At): a search or a walk about moves it on.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ClueDraw { get; set; }
    /// Herd grazing per resource: level 0-1 as of Minute, regrowing linearly after. Sorted, as records are dropped (DropRegrown).
    public SortedList<Resource, Grazing> Grazed { get; init; } = [];
}
public readonly record struct Grazing(double Level, long Minute);

/// A stand's chopping, and the tree that lies felled on it: its trunk is the firewood still to cut, worked over as many trips as it takes.
public sealed class TreeState : PoolState
{
    public double MinutesWorked { get; set; }
    public int Felled { get; set; }
    /// When the stand was last cut to nothing (limited stands only).
    public long ClearedMinute { get; set; }
    /// The felled tree's firewood still to cut, piece by piece, and how many pieces it held (which times each cut).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<double>? Trunk { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Resource? TrunkWood { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int TrunkPieces { get; set; }
}

/// A carcass or pile find whose contents are on the tile: whoever finds it next finds what is left of them.
public sealed class LaidState : PoolState;

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
    public const double ForageRespawnHours = 672;
    const double MaxRevealMultiplier = 10.0;

    /// Generation is a pure function of (seed, tile). In index order; cached, never saved.
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
        var (pools, rng) = Drawn(seed, m, tile);
        // Last, so a dealt find moves none of the tile's other draws.
        foreach (var dealt in DealtFinds(seed, m).GetValueOrDefault(tile) ?? [])
            pools.Add(Hidden(Make(Finds.Finds[dealt.Find], rng!, 0), RevealAt(rng!, dealt.ExpectedHours))!);
        return pools;
    }

    /// The cavePerMap finds the map's own draws left it short of, each on a cave floor with no site, on a stream
    /// of its own. Worked out once per map, from every cave tile's draws.
    static IReadOnlyDictionary<TileId, IReadOnlyList<DealtFind>> DealtFinds(int seed, GeneratedMap m)
    {
        if (m.DealtFinds is { } known) return known;
        var dealt = new Dictionary<TileId, IReadOnlyList<DealtFind>>();
        if (Finds.CavePerMap.Count > 0)
        {
            var caves = new List<TileId>();
            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                    if (m.CaveAt(new(x, y)) != Cave.None) caves.Add(new(x, y));
            var drawn = caves.SelectMany(t => Drawn(seed, m, t).Pools).Select(p => p.Name).ToList();
            foreach (var find in Finds.CavePerMap)
            {
                var rng = Rng.ForSystem(seed, $"per map {find.Find}");
                var free = caves.Where(t => m.CaveAt(t) == Cave.Floor && m.SiteAt(t) == null && !dealt.ContainsKey(t)).ToList();
                for (int have = drawn.Count(n => n == Finds.Finds[find.Find].Name); have < find.AtLeast && free.Count > 0; have++)
                {
                    int at = rng.NextInt(free.Count);
                    dealt[free[at]] = [find];
                    free.RemoveAt(at);
                }
            }
        }
        return m.DealtFinds = dealt;
    }

    static (List<PoolDef> Pools, Rng? Rng) Drawn(int seed, GeneratedMap m, TileId tile)
    {
        var pools = new List<PoolDef>();
        if (!m.InBounds(tile) || !m.TerrainAt(tile).IsPassable()) return (pools, null);
        var rng = Rng.ForSystem(seed, $"pools {TileIdConverter.Format(tile)}");
        var terrain = m.TerrainAt(tile);

        if (tile == m.Camp)
        {
            pools.Add(new ForagePool("forage", 0.38, MixedForest));
            return (pools, rng);
        }
        // A named place carries its own content; open-land sites also keep the terrain's hidden finds and
        // cave sites the passage's, skipping what the site already has.
        if (m.SiteAt(tile) is LocationDef site)
        {
            pools.AddRange(SitePools(Map.SiteFeatures(seed, m, tile), rng));
            if (site.Placement == Placement.Named) pools.AddRange(Discoveries(terrain, rng));
            else if (site.Placement == Placement.Cave)
                pools.AddRange(CaveDiscoveries(rng).Where(d => pools.All(p => p.Name != d.Name) && site.All.OfType<SiteShelter>().All(s => s.Name != d.Name)));
            return (pools, rng);
        }
        if (m.CaveAt(tile) != Cave.None)
        {
            var forage = m.CaveAt(tile).Row().Forage;
            pools.Add(new ForagePool("forage", forage.Density, ForageTables[forage.Table]));
            pools.AddRange(CaveDiscoveries(rng));
            return (pools, rng);
        }
        pools.Add(TerrainForage(terrain, rng));
        if (terrain.Row().Trees is TreeRow trees) pools.Add(new TreePool(trees.Name, null, trees.MinutesToFell));
        pools.AddRange(Details(terrain, rng));
        pools.AddRange(Discoveries(terrain, rng));
        return (pools, rng);
    }

    static ForagePool TerrainForage(Terrain terrain, Rng rng)
    {
        var forage = terrain.Row().Forage ?? throw new InvalidOperationException($"{terrain} has no forage");
        return new ForagePool("forage", Math.Clamp(Normal(rng, forage.Density, 0.32), 0.1, 2.0), ForageTables[forage.Table]);
    }

    /// A named site's features as pools, in order (Map.SiteFeatures has rolled their chances). Open water is a
    /// source to draw from; frozen water is hazard (Map.TravelHazardLevel). Shelters (Map.PlaceSiteShelters),
    /// small game (hunted by density), caches and event triggers (fired on arrival and in Search) are not pools.
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
                        [.. s.Items!.Select(i => new StockItem(i.Resource, i.UnitsMax is int max ? Between(rng, i.Units, max + 1) : i.Units, i.KgPerUnit, i.RespawnHoursPerUnit,
                            RequiresTool: i.RequiresTool))],
                        f.RevealAtHours);
                    break;
                case SiteFind find:
                    yield return Make(Finds.Finds[find.Find], rng, f.RevealAtHours);
                    break;
                case SiteWater { Frozen: false } w:
                    yield return new StockPool(w.Name, StockKind.Harvest, 1, [new StockItem(Resource.Water, 100, 1.0, 0.1)], f.RevealAtHours);
                    break;
            }
        }
    }

    /// Content/loot.json rolled: one salvage item per resource, plus the gear. Scale counts the pieces; a continuous
    /// resource's kilos are scaled again, a discrete one's pieces keep their weights.
    internal static (List<StockItem> Items, List<Gear> Gear) RollLoot(IReadOnlyList<LootRoll> table, Rng rng, double scale = 1)
    {
        var kg = new SortedDictionary<Resource, List<double>>();
        var gear = new List<Gear>();
        void Take(LootRoll r)
        {
            if (r.Chance < 1 && rng.NextDouble() >= r.Chance) return;
            if (r.Resource is Resource res)
            {
                int pieces = (int)Math.Ceiling((r.Pieces is [var lo, var hi] ? Between(rng, lo, hi + 1) : 1) * scale);
                var rolled = Pieces(rng, pieces, r.Kg![0], r.Kg[1]);
                if (!kg.TryGetValue(res, out var all)) kg[res] = all = [];
                all.AddRange(Items.IsDiscrete(res) ? rolled : [rolled.Sum() * scale]);
            }
            else if (r.Gear != null)
            {
                var g = Items.Make(r.Gear[rng.NextInt(r.Gear.Count)]);
                // An unlimited (-1) maximum stays whole: a share of it would come out 0.
                if (r.DurabilityPct is [var d0, var d1] && g.MaxDurability > 0)
                    g.Durability = Math.Max(1, Between(rng, (int)(g.MaxDurability * d0), (int)(g.MaxDurability * d1) + 1));
                gear.Add(g);
            }
            else
            {
                int count = (int)Math.Ceiling(Between(rng, r.Pick![0], r.Pick[1] + 1) * scale);
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

    static readonly IReadOnlyDictionary<string, IReadOnlyList<ForageItem>> ForageTables = Content.Tables.Forage;
    internal static readonly IReadOnlyList<ForageItem> MixedForest = ForageTables["MixedForest"];

    /// Exponential around the expected hours (so finds vary), capped at ten times.
    static double RevealAt(Rng rng, double expectedHours)
    {
        double u = rng.NextDouble();
        if (u == 0) u = double.Epsilon;
        return Math.Min(-Math.Log(u) * expectedHours, expectedHours * MaxRevealMultiplier);
    }

    static int Between(Rng rng, int min, int maxExclusive) => min + rng.NextInt(maxExclusive - min);
    static double Uniform(Rng rng, double min, double max) => min + rng.NextDouble() * (max - min);

    static List<double> Pieces(Rng rng, int count, double min, double max)
    {
        var kg = new List<double>(count);
        for (int i = 0; i < count; i++) kg.Add(Uniform(rng, min, max));
        return kg;
    }

    static readonly DiscoveryTables Finds = Content.Tables.Discoveries;

    /// Quick finds, 15-30 minutes. Details with nothing to take (tracks, scat...) are for moments
    /// (not built yet): they still take their turn in the draw but leave no pool.
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

    /// A draw that waits for a system not built yet still rolls, so building that system changes no other draw.
    static IEnumerable<PoolDef> Roll(IReadOnlyList<DiscoveryDraw> table, Rng rng)
    {
        foreach (var t in table)
        {
            if (rng.NextDouble() >= t.SpawnChance) continue;
            var made = t.Find is string id ? Make(Finds.Finds[id], rng, 0) : null;
            if (Hidden(made, RevealAt(rng, t.ExpectedHours)) is { } hidden) yield return hidden;
        }
    }

    static PoolDef? Hidden(PoolDef? made, double reveal) => made switch
    {
        StockPool stock => stock with { RevealAtHours = reveal },
        ShelterFind find => find with { RevealAtHours = reveal },
        CarcassFind find => find with { RevealAtHours = reveal },
        PileFind find => find with { RevealAtHours = reveal },
        null => null,
        _ => throw new InvalidOperationException($"No reveal for a {made.GetType().Name}"),
    };

    /// How high a find stands, for how far snow buries it: a bush, a salvage pile, a detail on the ground.
    static double PlacementHeightM(StockKind kind) => kind switch { StockKind.Salvage => 0.3, StockKind.Detail => 0.25, _ => 0.6 };

    internal static PoolDef Make(FindDef d, Rng rng, double revealAtHours)
    {
        if (d.Shelter is ShelterKind kind) return new ShelterFind(ShelterOf(d, kind, rng), revealAtHours, Found: d.Found);
        if (d.Carcass is { } c)
        {
            double pick = rng.NextDouble() * c.Animals.Sum(a => a.Weight);
            var animal = c.Animals[^1].Animal;
            foreach (var a in c.Animals) { if ((pick -= a.Weight) < 0) { animal = a.Animal; break; } }
            return new CarcassFind(d.Name, animal, Uniform(rng, c.HarvestedPct[0], c.HarvestedPct[1]), revealAtHours, d.Found);
        }
        if (d.Outcomes != null)
        {
            var (held, kept, found) = RollOutcome(d, rng);
            return d.Pile ? new PileFind(d.Name, held, kept, revealAtHours, found)
                : new StockPool(d.Name, d.Work!.Value, d.Minutes, held, revealAtHours, PlacementHeightM(d.Work.Value), kept.Count > 0 ? kept : null, Found: found);
        }
        if (d.Pile)
        {
            var (held, kept) = RollLoot(Content.Loot[d.Loot!], rng);
            return new PileFind(d.Name, held, kept, revealAtHours, d.Found);
        }
        var work = d.Work ?? throw new InvalidOperationException($"{d.Name} is neither stock nor shelter");
        if (d.Loot != null)
        {
            var (loot, lootGear) = RollLoot(Content.Loot[d.Loot], rng);
            return new StockPool(d.Name, work, d.Minutes, loot, revealAtHours, PlacementHeightM(work), lootGear.Count > 0 ? lootGear : null, Found: d.Found);
        }
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
            // Salvage and details: everything comes at once, after the minutes.
            items.Add((work == StockKind.Harvest ? new StockItem(resource, units, i.KgPerUnit, i.RespawnHoursPerUnit!.Value)
                : All(resource, i.KgPerUnitMax is double hi ? Pieces(rng, units, i.KgPerUnit, hi) : [.. Enumerable.Repeat(i.KgPerUnit, units)])) with
            { RequiresTool = i.RequiresTool });
        }
        foreach (var i in d.Items ?? []) Take(i);
        int wood = choices.Count > 0 ? rng.NextInt(choices[0].AnyOf.Count) : 0;
        foreach (var (index, anyOf) in choices) items[index] = items[index] with { Resource = anyOf[wood] };
        return new StockPool(d.Name, work, d.Minutes, items, revealAtHours, PlacementHeightM(work), gear.Count > 0 ? gear : null, Found: d.Found);
    }

    /// One of a find's outcomes, by weight, with its loot tables rolled and merged, and its line after each found line.
    static (List<StockItem> Items, List<Gear> Gear, IReadOnlyList<string> Found) RollOutcome(FindDef d, Rng rng)
    {
        var outcomes = d.Outcomes!;
        double pick = rng.NextDouble() * outcomes.Sum(o => o.Weight);
        var outcome = outcomes[^1];
        foreach (var o in outcomes) { if ((pick -= o.Weight) < 0) { outcome = o; break; } }
        var kg = new SortedDictionary<Resource, List<double>>();
        var gear = new List<Gear>();
        foreach (var table in outcome.Loot)
        {
            var (items, kept) = RollLoot(Content.Loot[table], rng, outcome.Scale);
            foreach (var i in items)
            {
                if (!kg.TryGetValue(i.Resource, out var all)) kg[i.Resource] = all = [];
                all.AddRange(i.PieceKgs!);
            }
            gear.AddRange(kept);
        }
        return ([.. kg.Select(x => All(x.Key, x.Value))], gear, [.. d.Found!.Select(f => $"{f} {outcome.Line}")]);
    }

    /// A cap left out is the kind's. A find's caps are at least its own levels (rolls can run past the kind's caps), so work on
    /// it never clamps a good one down.
    static ShelterDef ShelterOf(FindDef d, ShelterKind kind, Rng rng)
    {
        var k = Shelters.Kinds[kind];
        double? Level(double? level, double spread) => level is double l && spread > 0 ? l + rng.NextDouble() * spread : level;
        static double? Cap(double? level, double? cap, double kindCap) => level is double l ? Math.Max(cap ?? kindCap, l) : cap;
        double? insulation = Level(d.Insulation, d.InsulationSpread), overhead = Level(d.Overhead, d.OverheadSpread), wind = Level(d.Wind, d.WindSpread);
        return new ShelterDef(d.Name, kind, insulation, overhead, wind,
            Cap(insulation, d.InsulationCap, k.InsulationCap), Cap(overhead, d.OverheadCap, k.OverheadCap), Cap(wind, d.WindCap, k.WindCap));
    }

    static StockItem All(Resource r, IReadOnlyList<double> pieces) => new(r, 1, pieces.Sum(), PieceKgs: pieces);

    static void AddUnit(Inventory to, StockItem item)
    {
        if (!Items.IsDiscrete(item.Resource)) Items.Add(to, item.Resource, item.KgPerUnit);
        else if (item.PieceKgs != null) Items.AddPieces(to, item.Resource, item.PieceKgs);
        else Items.AddPiece(to, item.Resource, item.KgPerUnit);
    }

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

    /// Worked ground thins as 1/(1 + 2h) and regrows linearly to its base over four weeks from the last search.
    public static double ForageDensity(World world, PoolId id)
    {
        var def = (ForagePool)Def(world, id);
        if (StateOf(world, id) is not ForageState s || s.HoursForaged <= 0) return def.Density;
        double depleted = def.Density / (1.0 + s.HoursForaged * 2);
        double hoursSince = (world.Minute - s.LastForagedMinute) / 60.0;
        return Math.Min(def.Density, depleted + hoursSince / ForageRespawnHours * (def.Density - depleted));
    }

    public static double GrazedLevel(World world, PoolId id, Resource r) =>
        StateOf(world, id) is ForageState s && s.Grazed.TryGetValue(r, out var g)
            ? Math.Max(0, g.Level - (world.Minute - g.Minute) / 60.0 / ForageRespawnHours) : 0;

    public static void SetGrazed(World world, PoolId id, Resource r, double level) =>
        StateFor(world, id, () => new ForageState()).Grazed[r] = new Grazing(Math.Clamp(level, 0, 1), world.Minute);

    /// Drops grazing regrown to zero, then a forage state with nothing searched or grazed, then the tile's empty
    /// pools: each reads the same gone as kept, so the save holds only what differs (Tracks.Sweep, hourly).
    internal static void DropRegrown(World world, TileState tile)
    {
        if (tile.Pools is not { } pools) return;
        for (int p = pools.Count - 1; p >= 0; p--)
        {
            if (pools.Values[p] is not ForageState forage) continue;
            for (int g = forage.Grazed.Count - 1; g >= 0; g--)
            {
                var grazing = forage.Grazed.Values[g];
                if (grazing.Level - (world.Minute - grazing.Minute) / 60.0 / ForageRespawnHours <= 0) forage.Grazed.RemoveAt(g);
            }
            if (forage.Grazed.Count == 0 && forage.HoursForaged <= 0 && forage.ClueDraw == 0) pools.RemoveAt(p);
        }
        if (pools.Count == 0) tile.Pools = null;
    }

    static readonly (double Below, double Factor)[] Luck = [(0.15, 0.5), (0.75, 1.0), (0.95, 1.5), (0.99, 2.0), (1.0, 3.0)];

    /// One roll for a whole search.
    public static double RollLuck(Rng rng)
    {
        double roll = rng.NextDouble();
        return Luck.First(l => roll < l.Below).Factor;
    }

    public static LuckTier TierOf(double luckFactor) => luckFactor switch { < 0.6 => LuckTier.Lean, < 1.2 => LuckTier.Normal, < 1.8 => LuckTier.Good, _ => LuckTier.Jackpot };

    public static string QualityWord(double density) => Clues.Table.Quality.First(q => density >= q.AtLeast).Word;

    public static PoolId? ForageAt(World world, TileId tile)
    {
        var pools = At(world, tile);
        for (int i = 0; i < pools.Count; i++) if (pools[i] is ForagePool) return new PoolId(tile, i);
        return null;
    }

    public static ForageState ForageStateFor(World world, PoolId id) => StateFor(world, id, () => new ForageState());

    /// The focuses a forage ground offers, each with the words for what it would turn up: those that grow here, and General.
    public static IReadOnlyList<(ForageFocus Focus, string Hint)> Foci(ForagePool def)
    {
        string Hint(IEnumerable<ForageItem> items) => string.Join(", ", items.Select(i => Clues.Table.Hints[i.Resource]).Distinct());
        var foci = new List<(ForageFocus, string)>();
        foreach (var f in new[] { ForageFocus.Fuel, ForageFocus.Food, ForageFocus.Medicine, ForageFocus.Materials })
            if (def.Items.Any(i => Work.InFocus(i.Resource, f))) foci.Add((f, Hint(def.Items.Where(i => Work.InFocus(i.Resource, f)))));
        foci.Add((ForageFocus.General, BalancedSearch));
        return foci;
    }

    public const string BalancedSearch = "balanced search";

    /// The line a finder reads for a discovery, one of its found lines picked by where it lies, so it never rerolls. Null where it has none.
    public static string? FoundLine(World world, PoolId id)
    {
        var found = Def(world, id) switch
        {
            StockPool s => s.Found,
            ShelterFind f => f.Found,
            CarcassFind f => f.Found,
            PileFind f => f.Found,
            _ => null,
        };
        if (found is not { Count: > 0 }) return null;
        int salt = (id.Tile.X * 73856093) ^ (id.Tile.Y * 19349663) ^ (id.Index * 83492791);
        return found[(int)((uint)salt % (uint)found.Count)];
    }

    /// How hard a person has searched a tile, against the longest expected find in its draws: effort, never what is left.
    public static string SearchEffort(World world, AgentId who, TileId tile)
    {
        double hours = (world.Agent(who).Mind as PersonMind)?.Memory.Places.GetValueOrDefault(tile)?.SearchedHours ?? 0;
        double pct = hours / ThoroughSearchHours(world, tile);
        return Clues.Table.SearchBands.First(b => pct < b.BelowPct).Word;
    }

    public static double ThoroughSearchHours(World world, TileId tile)
    {
        var m = Map.Of(world);
        var draws = m.CaveAt(tile) != Cave.None && m.SiteAt(tile)?.Placement != Placement.Named ? Finds.Cave
            : Finds.Terrains.GetValueOrDefault(m.TerrainAt(tile)) ?? [];
        return draws.Select(d => d.ExpectedHours).Concat((DealtFinds(world.Seed, m).GetValueOrDefault(tile) ?? []).Select(d => d.ExpectedHours)).DefaultIfEmpty(1).Max();
    }

    /// A forecast: the kg of each resource searching here for the minutes given (each worth effectivePct of a minute's
    /// search, as impairment leaves it) is expected to find, before the
    /// searcher's eyes and tools (Work.ExpectedGather): Forage's chances at each luck, minute by minute, on ground
    /// thinning as Forage thins it, only when something turns up; grazing as it is now. rememberedDensity: the ground
    /// as a person remembers it, for a place out of sight, read with no grazing they could not see.
    public static IReadOnlyDictionary<Resource, double> ExpectedForage(World world, PoolId id, int minutes, double effectivePct = 1, double? rememberedDensity = null,
        ForageFocus focus = ForageFocus.General)
    {
        var def = (ForagePool)Def(world, id);
        double density = rememberedDensity ?? ForageDensity(world, id);
        if (minutes <= 0 || density <= 0) return new SortedDictionary<Resource, double>();
        var ungrazed = def.Items.Select(i => rememberedDensity == null ? 1 - GrazedLevel(world, id, i.Resource) : 1).ToArray();
        var kg = ExpectedForageByMinute(def, density, ungrazed, effectivePct, focus).Skip(minutes - 1).First();
        return new SortedDictionary<Resource, double>(kg.ToDictionary(k => k.Key, k => k.Value)); // a copy: the sequence's is live
    }

    /// ExpectedForage minute by minute, on ground of the density given, with each item's ungrazed share (none
    /// grazed if null), searched with the focus given: after each minute, the kg found so far. One dictionary, updated in
    /// place and never ending.
    public static IEnumerable<IReadOnlyDictionary<Resource, double>> ExpectedForageByMinute(ForagePool def, double density, double[]? ungrazed = null, double effectivePct = 1,
        ForageFocus focus = ForageFocus.General)
    {
        var kg = new SortedDictionary<Resource, double>();
        double hours = HoursBehind(def, density), stepHours = effectivePct / 60;
        while (true)
        {
            double d = def.Density / (1 + 2 * hours), findsPct = 0;
            for (int band = 0; band < Luck.Length; band++)
            {
                double bandPct = Luck[band].Below - (band == 0 ? 0 : Luck[band - 1].Below), nonePct = 1;
                for (int i = 0; i < def.Items.Count; i++)
                {
                    var item = def.Items[i];
                    double chance = d * item.Abundance * Work.FocusAbundanceFactor(item.Resource, focus) * (ungrazed?[i] ?? 1) * Luck[band].Factor * stepHours;
                    kg[item.Resource] = kg.GetValueOrDefault(item.Resource) + bandPct * chance * (item.MinKg + item.MaxKg) / 2;
                    nonePct *= chance >= 1 ? 0 : 1 - chance;
                }
                findsPct += bandPct * (1 - nonePct);
            }
            hours += stepHours * findsPct;
            yield return kg;
        }
    }

    /// A forecast: one felled tree's expected yield, the mixed wood shared out as Chop draws it.
    public static IReadOnlyDictionary<Resource, double> ExpectedTree(TreePool def) => ExpectedChop(def, null, double.MaxValue);

    /// A forecast: what chopping here for workMinutes gives from where the work stands: sticks and tinder if the tree comes down, and each piece cut.
    public static IReadOnlyDictionary<Resource, double> ExpectedChop(World world, PoolId id, double workMinutes) =>
        ExpectedChop((TreePool)Def(world, id), StateOf(world, id) as TreeState, workMinutes);

    static SortedDictionary<Resource, double> ExpectedChop(TreePool def, TreeState? s, double workMinutes)
    {
        var f = Felled;
        var kg = new SortedDictionary<Resource, double>();
        double minutes = (s?.MinutesWorked ?? 0) + workMinutes;
        if (s?.Trunk is { Count: > 0 } trunk)
        {
            double cut = Math.Min(trunk.Count, Math.Floor(minutes / MinutesPerPiece(def, s.TrunkPieces) + 1e-9));
            kg[s.TrunkWood!.Value] = cut * trunk.Average();
            return kg;
        }
        double fell = def.MinutesToFell * FellPct;
        if (minutes < fell - 1e-9) return kg;
        double pieces = f.Firewood.MiddleCount();
        double cutPieces = Math.Min(pieces, Math.Floor((minutes - fell) / MinutesPerPiece(def, pieces) + 1e-9));
        IReadOnlyList<(Resource Wood, double Share)> woods = def.Wood is Resource w ? [(w, 1.0)] : f.MixedWood;
        foreach (var (wood, share) in woods)
            if (cutPieces > 0) kg[wood] = cutPieces * share * f.FirewoodKg[wood];
        kg[Resource.Stick] = f.Sticks.MiddleCount() * f.Sticks.MiddleKg();
        kg[Resource.Tinder] = f.Tinder.MiddleCount() * f.Tinder.MiddleKg();
        return kg;
    }

    /// Depletes only when something turned up.
    /// luckFactor: the search's (WorkSession.LuckFactor); left out, this call is a search of its own and rolls one.
    /// A focus turns up more of its category (Work.FocusAbundanceFactor), so the ground thins as fast as it gives.
    public static List<(Resource Resource, double Kg)> Forage(World world, PoolId id, double hours, Rng rng, double? luckFactor = null,
        ForageFocus focus = ForageFocus.General)
    {
        var def = (ForagePool)Def(world, id);
        double luck = luckFactor ?? RollLuck(rng);
        double density = ForageDensity(world, id);
        var found = new List<(Resource, double)>();
        foreach (var item in def.Items)
        {
            double chance = density * item.Abundance * Work.FocusAbundanceFactor(item.Resource, focus) * (1 - GrazedLevel(world, id, item.Resource)) * luck * hours;
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

    /// The hours-foraged figure that gives today's density, so a new search re-thins from the regrown ground.
    static double Recovered(World world, PoolId id, ForagePool def, ForageState s) => HoursBehind(def, ForageDensity(world, id));

    static double HoursBehind(ForagePool def, double density) => density >= def.Density ? 0 : (def.Density / density - 1) / 2;

    public static bool HasTrees(World world, PoolId id)
    {
        var def = (TreePool)Def(world, id);
        if (def.Trees is not int max) return true;
        var s = StateOf(world, id) as TreeState;
        return s?.Trunk is { Count: > 0 } || Standing(world, def, s, max) > 0;
    }

    /// A cleared stand regrows one tree, a week later, and stops as soon as one tree stands.
    static int Standing(World world, TreePool def, TreeState? s, int max)
    {
        int left = max - (s?.Felled ?? 0);
        if (left > 0 || s == null) return left;
        return world.Minute - s.ClearedMinute >= def.RespawnHoursPerTree * 60 ? 1 : 0;
    }

    public static double ChopProgressPct(World world, PoolId id)
    {
        var def = (TreePool)Def(world, id);
        if (StateOf(world, id) is not TreeState s) return 0;
        if (s.Trunk == null) return s.MinutesWorked / def.MinutesToFell;
        double perPiece = MinutesPerPiece(def, s.TrunkPieces);
        return (def.MinutesToFell * FellPct + (s.TrunkPieces - s.Trunk.Count) * perPiece + s.MinutesWorked) / def.MinutesToFell;
    }

    public static readonly Felling Felled = Content.Tables.Felling;

    public const double FellPct = 0.4;

    static double MinutesPerPiece(TreePool def, double trunkPieces) => def.MinutesToFell * (1 - FellPct) / trunkPieces;

    /// The lying trunk's next piece, so a cutter whose pack can't take it stops first.
    public static double? NextPieceKg(World world, PoolId id) =>
        StateOf(world, id) is TreeState { Trunk: [.., var last] } ? last : null;

    public sealed record ChopResult(Inventory Got, bool CameDown, bool CutUp);

    /// Down at FellPct of its minutes with its sticks and tinder, the tree lies here as a trunk, cut a piece at a time over as many trips as it takes.
    /// roomKg: what the cutter can still carry; a piece over it stays on the trunk.
    public static ChopResult Chop(World world, PoolId id, double minutes, Rng rng, double roomKg = double.PositiveInfinity)
    {
        var def = (TreePool)Def(world, id);
        var s = StateFor(world, id, () => new TreeState());
        var got = new Inventory();
        bool cameDown = false;
        s.MinutesWorked += minutes;
        if (s.Trunk == null)
        {
            double fell = def.MinutesToFell * FellPct;
            if (s.MinutesWorked < fell - 1e-9) return new(got, false, false);
            s.MinutesWorked -= fell;
            Fall(world, id, def, s, rng, got);
            cameDown = true;
        }
        var trunk = s.Trunk!;
        double perPiece = MinutesPerPiece(def, s.TrunkPieces);
        while (trunk.Count > 0 && s.MinutesWorked >= perPiece - 1e-9)
        {
            if (trunk[^1] > roomKg - Items.WeightKg(got) + 1e-9) { s.MinutesWorked = perPiece; break; }
            s.MinutesWorked -= perPiece;
            Items.AddPiece(got, s.TrunkWood!.Value, trunk[^1]);
            trunk.RemoveAt(trunk.Count - 1);
        }
        if (trunk.Count > 0) return new(got, cameDown, false);
        s.Trunk = null;
        s.TrunkWood = null;
        s.TrunkPieces = 0;
        s.MinutesWorked = 0;
        return new(got, cameDown, true);
    }

    static void Fall(World world, PoolId id, TreePool def, TreeState s, Rng rng, Inventory got)
    {
        if (def.Trees is int max && Standing(world, def, s, max) <= 0) throw new InvalidOperationException($"Pool {id} has no tree to fell");
        var f = Felled;
        var wood = def.Wood ?? f.MixedWoodAt(rng.NextDouble());
        double pieceKg = f.FirewoodKg[wood], spread = f.FirewoodKgSpreadPct;
        int count = Between(rng, f.Firewood.Count[0], f.Firewood.Count[1] + 1);
        var trunk = new List<double>(count);
        for (int i = 0; i < count; i++) trunk.Add(pieceKg * ((1 - spread) + rng.NextDouble() * (2 * spread)));
        foreach (var (resource, pieces) in new[] { (Resource.Stick, f.Sticks), (Resource.Tinder, f.Tinder) })
        {
            int n = Between(rng, pieces.Count[0], pieces.Count[1] + 1);
            for (int i = 0; i < n; i++)
            {
                double kg = pieces.Kg![0] + rng.NextDouble() * (pieces.Kg[1] - pieces.Kg[0]);
                if (Items.IsDiscrete(resource)) Items.AddPiece(got, resource, kg);
                else Items.Add(got, resource, kg);
            }
        }
        s.Trunk = trunk;
        s.TrunkWood = wood;
        s.TrunkPieces = count;
        if (def.Trees is int stand)
        {
            if (s.Felled >= stand) s.ClearedMinute = world.Minute; // the regrown one, felled: the clock starts again
            else if (++s.Felled >= stand) s.ClearedMinute = world.Minute;
        }
    }

    /// Units of each item now, regrowth included (derived).
    public static int[] UnitsNow(World world, PoolId id)
    {
        var def = (StockPool)Def(world, id);
        if (StateOf(world, id) is not StockState s) return def.Items.Select(i => i.Units).ToArray();
        var units = new int[def.Items.Count];
        for (int i = 0; i < units.Length; i++)
        {
            var item = def.Items[i];
            int regrown = item.RespawnHoursPerUnit > 0 ? (int)Math.Floor((world.Minute - s.LastTakenMinute[i]) / 60.0 / item.RespawnHoursPerUnit) : 0;
            units[i] = (int)Math.Min(item.Units, (long)s.Units[i] + regrown);
        }
        return units;
    }

    public static bool HasStock(World world, PoolId id) => UnitsNow(world, id).Any(u => u > 0);

    /// Units left that this worker can take: an item needing no tool, or one whose tool is in the pack.
    public static bool HasStock(World world, PoolId id, Inventory pack) => ItemsLeft(world, id).Any(i => CanWork(i, pack));

    public static bool CanWork(StockItem item, Inventory? pack) => item.RequiresTool is not ToolType t || pack != null && Items.Tool(pack, t) != null;

    /// The tool this worker lacks, when only items needing it are left.
    public static ToolType? MissingTool(World world, PoolId id, Inventory pack) =>
        HasStock(world, id, pack) ? null : ItemsLeft(world, id).Select(i => i.RequiresTool).FirstOrDefault(t => t != null);

    public static bool WorksWith(World world, PoolId id, Inventory pack, ToolType tool) =>
        ItemsLeft(world, id).Any(i => i.RequiresTool == tool && CanWork(i, pack));

    static IEnumerable<StockItem> ItemsLeft(World world, PoolId id)
    {
        var def = (StockPool)Def(world, id);
        var units = UnitsNow(world, id);
        return def.Items.Where((_, i) => units[i] > 0);
    }

    /// Gear in a salvage comes with the first cycle. Each cycle gives a unit of every item the worker can take.
    public static Inventory WorkStock(World world, PoolId id, double minutes, Inventory? pack)
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
        bool Workable(int i) => now[i] > 0 && CanWork(def.Items[i], pack);
        while (s.MinutesWorked >= def.MinutesPerUnit - 1e-9 && Enumerable.Range(0, now.Length).Any(Workable))
        {
            s.MinutesWorked -= def.MinutesPerUnit;
            for (int i = 0; i < now.Length; i++)
            {
                if (!Workable(i)) continue;
                AddUnit(got, def.Items[i]);
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
        if (!Enumerable.Range(0, now.Length).Any(Workable)) s.MinutesWorked = 0;
        return got;
    }

    /// Discovery is the agent's memory, not the tile's. Forage, timber and a named site's stock in
    /// plain sight (RevealAtHours 0) are not hidden; every other stock and every shelter find is.
    public static bool IsHidden(PoolDef def) => def is StockPool { RevealAtHours: > 0 } or ShelterFind or CarcassFind or PileFind;

    /// What finding this pool lays on the tile in its place (a carcass, a pile): once found, it is that location feature, not a pool.
    public static bool LaysItsOwnFeature(PoolDef def) => def is CarcassFind or PileFind;

    static (double RevealAtHours, double PlacementHeightM) Hiding(PoolDef def) => def switch
    {
        StockPool s => (s.RevealAtHours, s.PlacementHeightM),
        ShelterFind f => (f.RevealAtHours, f.PlacementHeightM),
        CarcassFind f => (f.RevealAtHours, f.PlacementHeightM),
        PileFind f => (f.RevealAtHours, f.PlacementHeightM),
        _ => throw new InvalidOperationException($"{def.Name} is not hidden"),
    };

    public static bool Knows(World world, AgentId who, PoolId id) =>
        !IsHidden(Def(world, id)) || (world.Agent(who).Mind as PersonMind)?.Memory.Discovered.Contains(id) == true;

    /// Searching where the agent stands.
    public static void Search(World world, Agent agent, double perceptionWeightedHours, List<WorldEvent> events) =>
        Search(world, agent, agent.Tile, perceptionWeightedHours, events);

    /// Effort reaches every hidden pool on the tile searched (the agent's own, or one it is walking onto), slowed per pool by
    /// how far snow covers it, and is remembered (SearchEffort).
    public static void Search(World world, Agent agent, TileId tile, double perceptionWeightedHours, List<WorldEvent> events)
    {
        if (agent.Mind is not PersonMind mind) return;
        if (!mind.Memory.Places.TryGetValue(tile, out var place)) mind.Memory.Places[tile] = place = new TileMemory { SeenMinute = world.Minute };
        place.SearchedHours += perceptionWeightedHours;
        var pools = At(world, tile);
        for (int i = 0; i < pools.Count; i++)
        {
            if (!IsHidden(pools[i])) continue;
            var id = new PoolId(tile, i);
            if (mind.Memory.Discovered.Contains(id)) continue;
            var (revealAt, height) = Hiding(pools[i]);
            double hours = mind.Memory.SearchHours.GetValueOrDefault(id)
                + perceptionWeightedHours * Ground.Underfoot(world, tile).SearchFactor(0, height);
            if (hours >= revealAt)
            {
                mind.Memory.SearchHours.Remove(id);
                mind.Memory.Discovered.Add(id);
                events.Add(new PoolDiscovered(world.Minute, agent.Id, id, pools[i].Name));
                switch (pools[i])
                {
                    case ShelterFind find: Shelters.Found(world, id, find, events); break;
                    case CarcassFind find: LayCarcass(world, id, find); break;
                    case PileFind find: LayPile(world, id, find); break;
                }
            }
            else mind.Memory.SearchHours[id] = hours;
        }
    }

    /// The one carcass a find holds, laid down once, whoever finds it first; a find someone has already made lies there already.
    static void LayCarcass(World world, PoolId id, CarcassFind find)
    {
        if (FoundByAnyoneElse(world, id)) return;
        var body = SpeciesTable.NewBody(find.Animal, Rng.ForSystem(world.Seed, $"carcass find {id}"));
        Combat.LeaveCarcass(world, id.Tile, find.Animal, body, new Cause(null, FoundDead), find.HarvestedPct);
    }

    static void LayPile(World world, PoolId id, PileFind find)
    {
        if (FoundByAnyoneElse(world, id)) return;
        var pile = Items.PileAt(world, id.Tile, new Cause(null, $"hidden in the {Names.Lower(find.Name)}"));
        foreach (var item in find.Items) for (int u = 0; u < item.Units; u++) AddUnit(pile, item);
        pile.Gear.AddRange(find.Gear.Select(g => new Gear { Kind = g.Kind, Durability = g.Durability, MaxDurability = g.MaxDurability }));
    }

    public const string FoundDead = "found dead";

    static bool FoundByAnyoneElse(World world, PoolId id)
    {
        if (StateOf(world, id) is LaidState) return true;
        StateFor(world, id, () => new LaidState());
        return false;
    }
}
