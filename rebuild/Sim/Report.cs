using System.Text;
using static System.FormattableString;

namespace Survival;

/// What a find is, for the distribution targets (emergent-world-design §8).
public enum FindCategory { DeadMatter, Resource, Structure, Sign, Agent }

/// One thing a player could find in the world today. Cause is the sign's (Signs.At), null where none is kept.
public sealed record FindRow(TileId Tile, int RegionX, int RegionY, string Kind, FindCategory Category, long? AgeMinutes, string? Cause, string Text);

/// One region's finds by category and whether its survival floors hold.
/// FuelFireHours is what bare hands gather (the floor reads it); FelledFireHours what an axe adds.
public sealed record RegionRow(int X, int Y, int PassableTiles, int[] Counts, bool Water, bool Fuel, bool Shelter, double FuelFireHours, double FelledFireHours)
{
    public int Total => Counts.Sum();
}

public sealed record DistributionReport(IReadOnlyList<RegionRow> Regions, int[] Totals, double MedianFinds)
{
    public int Total => Totals.Sum();
    public double Pct(FindCategory c) => Total == 0 ? 0 : (double)Totals[(int)c] / Total;
    public bool DeadMatterOk => Pct(FindCategory.DeadMatter) <= Report.MaxDeadMatterPct;
    public bool ResourceOk => Pct(FindCategory.Resource) >= Report.MinResourcePct;
    public bool TooFew(RegionRow r) => r.Total < MedianFinds * Report.MinRegionFactor;
    public bool TooMany(RegionRow r) => r.Total > MedianFinds * Report.MaxRegionFactor;
}

public sealed record PopulationRow(Species Species, int Herds, int Members);
public sealed record CurvePoint(int Day, IReadOnlyList<PopulationRow> Populations);

/// QA instruments over a world: the find dump, the distribution report and population curves
/// (emergent-world-design §8). Reads only; Curve is the one thing here that runs the sim.
/// Ambient forage and standing timber are on nearly every tile, so they are the survivable
/// skeleton (the floors), not finds: they appear as rows only once something has worked them.
public static class Report
{
    public const int RegionTiles = 10;
    public const double MaxDeadMatterPct = 0.35, MinResourcePct = 0.25;
    /// A region with finds outside [median x Min, median x Max] fails. The doc gives only the 3x
    /// ceiling; a third of the median as the floor is a starting guess.
    public const double MinRegionFactor = 1.0 / 3, MaxRegionFactor = 3.0;

    public static IReadOnlyList<FindRow> Finds(World w)
    {
        var m = Map.Of(w);
        var rows = new List<FindRow>();
        void Add(TileId t, string kind, FindCategory cat, long? age, string text, Cause? cause = null) =>
            rows.Add(new FindRow(t, t.X / RegionTiles, t.Y / RegionTiles, kind, cat, age, cause?.ToString(), text));

        for (int x = 0; x < m.Width; x++)
            for (int y = 0; y < m.Height; y++)
            {
                var t = new TileId(x, y);
                if (m.SiteAt(t) is { } site) Add(t, "place", FindCategory.Structure, null, site.Name);
                var pools = Pools.At(w, t);
                for (int i = 0; i < pools.Count; i++)
                {
                    var id = new PoolId(t, i);
                    switch (pools[i])
                    {
                        case StockPool s:
                            var units = Pools.UnitsNow(w, id);
                            string left = string.Join(", ", s.Items.Zip(units).Select(p => Invariant($"{p.First.Resource} {p.Second}/{p.First.Units}")));
                            Add(t, "stock", FindCategory.Resource, null, $"{s.Name} ({s.Kind}{(Pools.IsHidden(s) ? ", hidden" : "")}): {left}");
                            break;
                        case ForagePool f when Pools.StateOf(w, id) is ForageState:
                            double grazed = f.Items.Max(it => Pools.GrazedLevel(w, id, it.Resource));
                            Add(t, "worked forage", FindCategory.Resource, null,
                                Invariant($"forage density {Pools.ForageDensity(w, id):0.00} of {f.Density:0.00}, grazed {grazed:P0}"));
                            break;
                        case TreePool tr when Pools.StateOf(w, id) is TreeState ts:
                            Add(t, "worked trees", FindCategory.Resource, null,
                                Invariant($"{tr.Name}: {ts.Felled} felled{(Pools.HasTrees(w, id) ? "" : ", cleared")}"));
                            break;
                        // By the find's pool, not its name: a find can be called what the shelter in its way is.
                        case ShelterFind sf when w.WaitingShelters.GetValueOrDefault(t, -1) == i && Shelters.Peek(w, t)?.FromFind != i:
                            Add(t, "shelter find", FindCategory.Structure, null, $"{sf.Name} ({sf.Shelter.Kind}), found, waiting for the tile");
                            break;
                        case ShelterFind sf when !FoundByAnyone(w, id):
                            Add(t, "shelter find", FindCategory.Structure, null, $"{sf.Name} ({sf.Shelter.Kind}), hidden");
                            break;
                        case ShelterFind sf when Shelters.Peek(w, t) is { } other && other.FromFind != i:
                            Add(t, "shelter find", FindCategory.Structure, null, $"{sf.Name} ({sf.Shelter.Kind}), found, the {other.Name} stands in its place");
                            break;
                        case ShelterFind: break; // found: standing, the shelter's own row says so
                        case ForagePool or TreePool: break; // the skeleton: see the class comment
                        default: throw new InvalidOperationException($"Report has no row for pool {pools[i].GetType().Name}");
                    }
                }
                foreach (var sign in Signs.At(w, t)) AddSign(t, sign);
            }

        // The carrion flock circling flesh on the tile (Tags.Beacon), as the find's text says it.
        string Circled(TileId t) => Fauna.FlockAt(w, t) != null ? ", ravens circling" : "";

        // The signs, as finds. What a pool shows of being worked is its pool row's business, above.
        void AddSign(TileId t, Sign sign)
        {
            long? age = sign.MadeMinute is long made ? w.Minute - made : null;
            switch (sign.Kind)
            {
                case SignKind.Body when sign.Thing.Index != AgentId.Player.Value: // the player is not a find
                    var dead = w.Agent(new AgentId(sign.Thing.Index));
                    Add(t, "body", FindCategory.DeadMatter, age,
                        $"dead {dead.Species} ({dead.Id}), {Remains.StageOf(w, dead).ToString().ToLowerInvariant()}{Circled(t)}", sign.Cause);
                    break;
                case SignKind.Carcass:
                    var c = Carcasses.Find(w, t, sign.Thing.Index)!;
                    Add(t, "carcass", FindCategory.DeadMatter, age,
                        Invariant($"{c.Animal} carcass, {Carcasses.TotalRemainingKg(c):0.0} kg left, decay {Carcasses.DecayLevelNow(w, t, c):P0}{Circled(t)}"), sign.Cause);
                    break;
                case SignKind.Bones:
                    var b = Carcasses.Find(w, t, sign.Thing.Index)!;
                    Add(t, "bones", FindCategory.DeadMatter, age, Invariant($"{b.Animal} bones, {Carcasses.TotalRemainingKg(b):0.0} kg"), sign.Cause);
                    break;
                case SignKind.RavensOver:
                    Add(t, "ravens", FindCategory.Sign, age, $"ravens over {sign.Cause?.What ?? "something"}", sign.Cause);
                    break;
                case SignKind.Stash:
                    var stash = w.Tiles[t].Stash!;
                    Add(t, "stash", FindCategory.Resource, age, "stash: " + string.Join(", ",
                        stash.Kg.Select(kv => Invariant($"{kv.Key} {kv.Value:0.##}kg")).Concat(stash.Gear.Select(g => Items.Def(g).Name))), sign.Cause);
                    break;
                case SignKind.FirePit:
                    var f = w.Fires[new FireId(sign.Thing.Index)];
                    Add(t, "fire pit", FindCategory.Structure, age, Invariant($"{f.Pit} pit, {Fire.Phase(f)}, charcoal {f.CharcoalKg:0.0} kg"), sign.Cause);
                    break;
                case SignKind.Shelter:
                    var s = Shelters.Peek(w, t)!; // melted as of now, on a copy: a thawed snow shelter is gone
                    Add(t, "shelter", FindCategory.Structure, age, Invariant($"{s.Name} ({s.Kind}), quality {Shelters.Quality(s):P0}"), sign.Cause);
                    break;
                case SignKind.Prints:
                    var p = Tracks.Read(w, t).First(x => (int)x.Maker == sign.Thing.Index);
                    Add(t, "tracks", FindCategory.Sign, age,
                        Invariant($"{p.Maker} prints, traffic {p.Traffic}, {p.Pattern} {p.Heading}, {p.FreshnessPct:P0} fresh"), sign.Cause);
                    break;
                // An edge's one ref is named from its first tile (EdgeId.A), whose other end is east or south.
                case SignKind.Trail:
                    var to = Map.Step(t, sign.Thing.TrailDirection!.Value);
                    Add(t, "trail", FindCategory.Sign, age, $"{Tracks.TierOf(w, t, to)} to {TileIdConverter.Format(to)}", sign.Cause);
                    break;
                case SignKind.Body: break; // the player
                case SignKind.WorkedGround or SignKind.Grazed or SignKind.Felled or SignKind.Picked: break; // the pool rows, above
                default: throw new InvalidOperationException($"Report has no row for sign {sign.Kind}");
            }
        }

        foreach (var (t, b) in w.Beddings)
            Add(t, "bedding", FindCategory.Structure, null, Shelters.Beddings[b].Name);
        foreach (var (id, a) in w.Agents)
        {
            if (id == AgentId.Player) continue;
            int alive = a.Members.Count(Body.IsAlive);
            if (alive == 0) continue; // a body: a sign, above
            string state = a.Mind is HerdMind h ? h.State.ToString() : a.Mind.GetType().Name;
            Add(a.Tile, a.Mind is HerdMind ? "herd" : "person", FindCategory.Agent, null, $"{a.Species} x{alive}, {state} ({id})");
        }

        rows.Sort((p, q) =>
            p.Tile.CompareTo(q.Tile) is int c and not 0 ? c
            : string.CompareOrdinal(p.Kind, q.Kind) is int k and not 0 ? k
            : string.CompareOrdinal(p.Text, q.Text));
        return rows;
    }

    static bool FoundByAnyone(World w, PoolId id) =>
        w.Agents.Values.Any(a => (a.Mind as PersonMind)?.Knows.Discovered.Contains(id) == true);

    public static string DumpText(World w) => DumpText(w, Finds(w));

    public static string DumpText(World w, IReadOnlyList<FindRow> finds)
    {
        var sb = new StringBuilder();
        sb.Append(Invariant($"# finds, seed {w.Seed}, minute {w.Minute} (day {w.Minute / 1440}), {finds.Count} rows\n"));
        sb.Append("# tile\tregion\tcategory\tkind\tage_min\tcause\ttext\n");
        foreach (var f in finds)
            sb.Append(Invariant($"{TileIdConverter.Format(f.Tile)}\t{f.RegionX},{f.RegionY}\t{f.Category}\t{f.Kind}\t{(f.AgeMinutes is long a ? a.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-")}\t{f.Cause ?? "-"}\t{f.Text}\n"));
        return sb.ToString();
    }

    /// What someone standing here sees: the place, what is plainly there and what they have found.
    /// Reads only, as the rest of Report: a snow shelter is melted and a carcass decayed on copies.
    public static IReadOnlyList<string> Look(World w, AgentId who)
    {
        var p = w.Agent(who);
        var m = Map.Of(w);
        var lines = new List<string> { Map.Describe(m, p.Tile) + (Map.DiscoveryText(w, p.Tile) is { } text ? $": {text}" : "") };
        foreach (var f in Map.SiteFeatures(w.Seed, m, p.Tile))
            if (f.RevealAtHours == 0 && f is SiteWater { Frozen: true } or SiteSmallGame or SiteCache) // open water is a pool, below
                lines.Add(f switch
                {
                    SiteWater x => Invariant($"water: {x.Name}{(x.Frozen ? Invariant($", ice {x.Ice:0.0}") : "")}"),
                    SiteSmallGame x => $"small game: {string.Join(", ", x.Animals.Select(a => a.Animal))}",
                    SiteCache x => Invariant($"cache: {x.Name}, holds {x.CapacityKg:0} kg"),
                    _ => throw new InvalidOperationException($"Look has no line for {f.GetType().Name}"),
                });
        if (Shelters.Peek(w, p.Tile) is { } shelter) lines.Add($"shelter: {shelter.Name}");
        var pools = Pools.At(w, p.Tile);
        for (int i = 0; i < pools.Count; i++)
        {
            var id = new PoolId(p.Tile, i);
            if (!Pools.Knows(w, who, id)) continue; // what you have not found, you cannot see
            lines.Add(pools[i] switch
            {
                ForagePool => Invariant($"[{i}] forage, density {Pools.ForageDensity(w, id):0.00}"),
                TreePool t => Invariant($"[{i}] {t.Name} ({(Pools.HasTrees(w, id) ? Invariant($"{Pools.ChopProgressPct(w, id):P0} cut") : "cleared")})"),
                StockPool s => $"[{i}] {s.Name}: " + string.Join(", ", s.Items.Zip(Pools.UnitsNow(w, id)).Select(x => Invariant($"{x.First.Resource} {x.Second}/{x.First.Units}"))),
                _ => $"[{i}] {pools[i].Name}",
            });
        }
        var tile = w.Tiles.GetValueOrDefault(p.Tile);
        foreach (var c in tile?.Carcasses ?? [])
            lines.Add(Invariant($"carcass {c.Id}: {c.Animal}, decay {Carcasses.DecayLevelNow(w, p.Tile, c):P0}, {Carcasses.TotalRemainingKg(c):0.0} kg left"));
        if (tile?.Stash is { } stash && (stash.Kg.Count > 0 || stash.Gear.Count > 0)) lines.Add($"stash: {Items.Describe(stash)}");
        if (Work.HasOpenWater(w, p.Tile)) lines.Add("open water");
        return lines;
    }

    // ---- distribution ----

    public static DistributionReport Distribution(World w) => Distribution(w, Finds(w));

    /// Regions with no passable ground (the mountain wall) are left out: nothing can be there.
    public static DistributionReport Distribution(World w, IReadOnlyList<FindRow> finds)
    {
        var m = Map.Of(w);
        int rw = (m.Width + RegionTiles - 1) / RegionTiles, rh = (m.Height + RegionTiles - 1) / RegionTiles;
        int cats = Enum.GetValues<FindCategory>().Length;
        var counts = new int[rw, rh, cats];
        foreach (var f in finds) counts[f.RegionX, f.RegionY, (int)f.Category]++;

        var regions = new List<RegionRow>();
        for (int ry = 0; ry < rh; ry++)
            for (int rx = 0; rx < rw; rx++)
            {
                int passable = 0;
                double fuelHours = 0, felledHours = 0;
                bool water = false, shelter = false;
                for (int x = rx * RegionTiles; x < Math.Min(m.Width, (rx + 1) * RegionTiles); x++)
                    for (int y = ry * RegionTiles; y < Math.Min(m.Height, (ry + 1) * RegionTiles); y++)
                    {
                        var t = new TileId(x, y);
                        if (!m.TerrainAt(t).IsPassable()) continue;
                        passable++;
                        var pools = Pools.At(w, t);
                        water |= Work.HasOpenWater(w, t) || pools.Any(p => p is StockPool s && s.Items.Any(i => i.Resource == Resource.Water));
                        var fuel = FuelFireHoursPerWorkHour(w, t);
                        fuelHours += fuel.ByHand;
                        felledHours += fuel.WithAxe;
                        shelter |= Shelters.Peek(w, t) != null || m.CaveAt(t) != Cave.None || pools.Any(p => p is ShelterFind);
                    }
                if (passable == 0) continue;
                regions.Add(new RegionRow(rx, ry, passable, Enumerable.Range(0, cats).Select(c => counts[rx, ry, c]).ToArray(), water,
                    fuelHours >= MinFuelFireHours, shelter, fuelHours, felledHours));
            }
        var totals = Enumerable.Range(0, cats).Select(c => regions.Sum(r => r.Counts[c])).ToArray();
        if (totals.Sum() != finds.Count) throw new InvalidOperationException($"Distribution counted {totals.Sum()} of {finds.Count} finds");
        return new DistributionReport(regions, totals, Median(regions.Select(r => (double)r.Total)));
    }

    /// The fuel floor (§8 floors per region, as a minimum fuel-quality sum): a region passes when an
    /// hour's work by hand on each of its tiles would bring in at least this many hours of fire, every
    /// source weighed by how long its wood burns (Fire.Props). The floor is about surviving, and the
    /// player starts with no axe: standing trees (and stock that needs one) are counted apart, as
    /// felled fuel, and do not hold a region up. Open plain gives about 0.02 an hour in tinder and
    /// twigs, rock and barren less, so open steppe or rock with little deadfall falls under it.
    // 12 is a night: a fire kept through twelve dark hours at about a kilo an hour.
    public const double MinFuelFireHours = 12;

    /// Hours of fire an hour's work here brings in now: forage by today's density, a standing tree
    /// by its felling time, stock by what is left (all of it, if it takes under an hour). Mixed
    /// forest is the felling's own draw (Pools.Felled's mixed wood). By hand: forage and stock anyone can take; with an
    /// axe: the trees, and stock that needs a tool (content's only one is an axe).
    public static (double ByHand, double WithAxe) FuelFireHoursPerWorkHour(World w, TileId t)
    {
        var pools = Pools.At(w, t);
        double hours = 0, felled = 0;
        for (int i = 0; i < pools.Count; i++)
        {
            var id = new PoolId(t, i);
            switch (pools[i])
            {
                case ForagePool f:
                    hours += Pools.ForageDensity(w, id) * FireHoursPerDensity(f);
                    break;
                case TreePool tree when Pools.HasTrees(w, id):
                    felled += TreeFireHours(tree.Wood) / (tree.MinutesToFell / 60.0);
                    break;
                case StockPool s when s.Items.Any(x => Fire.IsFuel(x.Resource)):
                    var units = Pools.UnitsNow(w, id);
                    double fire = 0;
                    for (int k = 0; k < s.Items.Count; k++)
                        if (Fire.IsFuel(s.Items[k].Resource)) fire += units[k] * s.Items[k].KgPerUnit / Fire.Props[s.Items[k].Resource].BurnRateKgPerHour;
                    fire /= Math.Max(1, s.Items.Max(x => x.Units) * s.MinutesPerUnit / 60.0); // a small stock gives all it has
                    if (s.RequiresTool == null) hours += fire;
                    else felled += fire;
                    break;
            }
        }
        return (hours, felled);
    }

    /// A forage pool's fire per unit of density: what its fuel items burn for, by abundance and their
    /// middle weight. Fixed by content, so worked out once per pool row (a whole-map pass asked it of
    /// every tile).
    static double FireHoursPerDensity(ForagePool f) =>
        FireFactors.GetValue(f, p => new(p.Items.Where(x => Fire.IsFuel(x.Resource))
            .Sum(item => item.Abundance * (item.MinKg + item.MaxKg) / 2 / Fire.Props[item.Resource].BurnRateKgPerHour))).Value;

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ForagePool, System.Runtime.CompilerServices.StrongBox<double>> FireFactors = new();

    /// One felled tree's fire: Pools.Chop's yield at the middle of each of its draws (Pools.Felled).
    internal static double TreeFireHours(Resource? wood)
    {
        var f = Pools.Felled;
        double Logs(Resource r) => f.Logs.MiddleCount() * f.LogKg[r] / Fire.Props[r].BurnRateKgPerHour;
        double logs = wood is Resource r ? Logs(r) : f.MixedWood.Sum(m => m.Share * Logs(m.Wood));
        return logs + f.Kindling.MiddleCount() * f.Kindling.MiddleKg() / Fire.Props[Resource.Kindling].BurnRateKgPerHour
            + f.Tinder.MiddleCount() * f.Tinder.MiddleKg() / Fire.Props[Resource.Tinder].BurnRateKgPerHour;
    }

    static double Median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToList();
        if (v.Count == 0) return 0;
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    public static string DistributionText(World w, DistributionReport d)
    {
        var sb = new StringBuilder();
        var cats = Enum.GetValues<FindCategory>();
        sb.Append(Invariant($"# distribution, seed {w.Seed}, day {w.Minute / 1440}, {d.Total} finds in {d.Regions.Count} regions of {RegionTiles}x{RegionTiles}\n"));
        foreach (var c in cats) sb.Append(Invariant($"{c,-11} {d.Totals[(int)c],6}  {d.Pct(c),6:P1}\n"));
        sb.Append(Invariant($"dead matter <= {MaxDeadMatterPct:P0}: {(d.DeadMatterOk ? "PASS" : "FAIL")}\n"));
        sb.Append(Invariant($"resources >= {MinResourcePct:P0}: {(d.ResourceOk ? "PASS" : "FAIL")}\n"));
        int few = d.Regions.Count(d.TooFew), many = d.Regions.Count(d.TooMany);
        sb.Append(Invariant($"median finds/region {d.MedianFinds:0.#}: {few} below {MinRegionFactor:0.##}x, {many} above {MaxRegionFactor:0.#}x\n"));
        sb.Append(Invariant($"floors failing: water {d.Regions.Count(r => !r.Water)}, fuel (under {MinFuelFireHours:0} h of fire by hand) {d.Regions.Count(r => !r.Fuel)}, shelter {d.Regions.Count(r => !r.Shelter)} of {d.Regions.Count}\n"));
        sb.Append(Invariant($"populations: {string.Join(", ", Populations(w).Select(p => $"{p.Species} {p.Members} in {p.Herds}"))}\n"));
        sb.Append("\nregion  tiles " + string.Join(" ", cats.Select(c => Invariant($"{Short(c),5}"))) + " total  floors(water fuel shelter) fuel_h felled_h count\n");
        foreach (var r in d.Regions)
            sb.Append(Invariant($"{r.X,2},{r.Y,-2}  {r.PassableTiles,5} ") + string.Join(" ", r.Counts.Select(n => Invariant($"{n,5}")))
                + Invariant($" {r.Total,5}  {Flag(r.Water)} {Flag(r.Fuel)} {Flag(r.Shelter)} {r.FuelFireHours,7:0.0} {r.FelledFireHours,8:0.0}  {(d.TooFew(r) ? "LOW" : d.TooMany(r) ? "HIGH" : "ok")}\n"));
        return sb.ToString();
    }

    static readonly string[] ShortNames = ["dead", "res", "struc", "sign", "agent"]; // FindCategory order
    static string Short(FindCategory c) => ShortNames[(int)c];

    static string Flag(bool ok) => ok ? "y" : "N";

    // ---- populations ----

    /// Living members per species, the player left out.
    public static IReadOnlyList<PopulationRow> Populations(World w) =>
        w.Agents.Values.Where(a => a.Id != AgentId.Player && Sim.IsAlive(a))
            .GroupBy(a => a.Species).OrderBy(g => g.Key)
            .Select(g => new PopulationRow(g.Key, g.Count(), g.Sum(a => a.Members.Count(Body.IsAlive))))
            .ToList();

    /// Runs the world on chunkDays at a time and records the populations after each chunk.
    public static IReadOnlyList<CurvePoint> Curve(World w, int days, int chunkDays)
    {
        if (chunkDays <= 0 || days <= 0 || days % chunkDays != 0)
            throw new ArgumentException($"Curve needs days ({days}) a positive multiple of chunkDays ({chunkDays})");
        var points = new List<CurvePoint>();
        int startDay = (int)(w.Minute / 1440);
        for (int d = chunkDays; d <= days; d += chunkDays)
        {
            Sim.Advance(w, chunkDays * 1440);
            points.Add(new CurvePoint(startDay + d, Populations(w)));
        }
        return points;
    }

    public static string CurveText(IReadOnlyList<CurvePoint> curve)
    {
        var species = curve.SelectMany(p => p.Populations.Select(r => r.Species)).Distinct().OrderBy(s => s).ToList();
        var sb = new StringBuilder("day  " + string.Join(" ", species.Select(s => $"{s,11}")) + "\n");
        foreach (var p in curve)
            sb.Append(Invariant($"{p.Day,4} ") + string.Join(" ", species.Select(s => Invariant($"{p.Populations.FirstOrDefault(r => r.Species == s)?.Members ?? 0,11}"))) + "\n");
        return sb.ToString();
    }
}
