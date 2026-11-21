// PROTOTYPE, throwaway. The requirements-tree planner for one goal ("a fire that lasts the night").
// It reads only the Sim's public surface: Verbs.At and Sim.Refusal, the forecasts Fire/Trip/Tiers expose,
// content tables, and the agent's own Beliefs. Every rule beyond the general mechanisms is tagged
// [RULE n] so FINDINGS.md can list them.
using Stopwatch = System.Diagnostics.Stopwatch;
using Survival;

namespace NpcReq;

/// How crude each forecast is. Level A is the crudest that could work; B uses the Sim's own forecasts.
public sealed record Crude(string Name, bool FireByFuelType, bool YieldDepletes, bool YieldFromMemory, bool TravelByTrip, bool CostIncludesGather, bool ShareTrips)
{
    public static readonly Crude A = new("A (crude)", FireByFuelType: false, YieldDepletes: false, YieldFromMemory: true, TravelByTrip: false, CostIncludesGather: true, ShareTrips: true);
    public static readonly Crude B = new("B (Sim forecasts)", FireByFuelType: true, YieldDepletes: true, YieldFromMemory: true, TravelByTrip: true, CostIncludesGather: true, ShareTrips: true);
}

// ---------- the requirements tree ----------

/// An "all of" child: an amount of something. Units: fuel-hours for Fuel, kg for resources, 1 for a tool.
public sealed record Req(string Name, IReadOnlySet<Resource> AnyOf, double Amount, Func<Resource, double> UnitsPerKg, Func<Gear, bool>? Tool = null)
{
    public bool IsTool => Tool != null;
}

public enum SourceKind { Pack, CampStash, Stash, Forage, Chop, Recipe }

/// One option of a "one of". Supplies Units of the Req; costs travel there and home plus work.
public sealed record Source(SourceKind Kind, TileId Tile, int Pool, double Units, double Kg, double TravelMin, double WorkMin, string? Recipe = null)
{
    public double Minutes => TravelMin + WorkMin;
}

/// One leaf of the shopping list: do this verb on this tile.
public sealed record Leaf(SourceKind Kind, TileId Tile, int Pool, string For, double Units, double Kg, double WorkMin, string? Recipe = null)
{
    public override string ToString() => $"{Kind}@{Rel(Tile)} {For} {Units:0.##} ({WorkMin:0}m)";
    public static TileId Home;
    public static TileId Origin;
    public static string Rel(TileId t) => (t == Home ? "home" : "") + $"({t.X - Origin.X:+0;-0;0},{t.Y - Origin.Y:+0;-0;0})";
}

public sealed record Plan(List<Leaf> Leaves, double ForecastMinutes, List<string> Impossible, int Depth);

public sealed class Goal
{
    public required long Started { get; init; }
    public required long Until { get; init; }          // sunrise
    public required double ForecastMinutes { get; init; }
    public double BudgetMinutes => Math.Max(60, 1.5 * ForecastMinutes);
    public required Tier Tier { get; init; }
}

public sealed class Stats
{
    public int Steps, Plans, GoalsMade, Trips;
    public readonly Dictionary<string, int> Drops = [];
    public readonly SortedDictionary<string, int> Refusals = new();      // Sim refusals met, by reason
    public readonly SortedDictionary<string, int> Interruptions = new(); // Sim interruptions met
    public readonly List<string> Drift = [];                             // Proposed said yes, Sim said no
    public readonly SortedDictionary<string, int> Rules = new();         // [RULE n] firings
    public double PlanMicros, PlanMicrosMax;
    public int SourceFlips;
    public readonly List<double> RiskSeen = [];
    public string? LastTarget;
    public void Rule(string r) => Rules[r] = Rules.GetValueOrDefault(r) + 1;
}

public sealed class Planner(AgentId who, TileId home, Crude crude, Stats stats, Action<string> log)
{
    public Goal? Goal;
    /// Where the fire is. The goal's site is a one-of too (SITE): committing to a site elsewhere moves home there.
    public TileId Home { get; private set; } = home;
    public bool ChooseSite = true;
    const int Radius = 12;
    /// Most a forage source is planned for in one go, hours. [RULE 1]
    const double MaxStintHours = 1.5;
    /// The work quantum: re-plan at least this often while working. [RULE 2]
    const int StintMinutes = 30;

    // ================= what the NPC knows =================

    Agent Me(World w) => w.Agent(who);
    Beliefs Knows(World w) => Knowledge.Of(Me(w));
    static long NextSunrise(long minute)
    {
        int mod = Weather.MinuteOfDay(minute), rise = Weather.SunriseHour(minute) * 60;
        return minute + (mod < rise ? rise - mod : 1440 - mod + rise);
    }
    static bool DarkAt(long minute) { int h = Weather.MinuteOfDay(minute) / 60; return h < 5 || h >= 21; } // Work.TooDarkToWork's hours: not public [RULE 3]

    /// Travel minutes from a tile over explored ground (Dijkstra). Crude A: the 5-minute crossing floor per tile.
    readonly Dictionary<(TileId, long), Dictionary<TileId, double>> distCache = [];
    /// Crossing minutes per edge, kept for the game hour and shared by every planner: the sky and ground change slowly.
    public static bool EdgeCache = true;
    static readonly Dictionary<(TileId, TileId, int), double> edges = [];
    static long edgeHour = -1;
    static double EdgeMinutes(World w, Agent me, TileId t, TileId n, WeatherSample sky, double enc)
    {
        long hour = w.Minute / 60;
        if (hour != edgeHour) { edges.Clear(); edgeHour = hour; }
        int band = (int)(enc * 4);
        if (edges.TryGetValue((t, n, band), out var m)) return m;
        return edges[(t, n, band)] = Trip.Terms(w, me.Body, t, n, Trip.IsHazardousNow(w, t) || Trip.IsHazardousNow(w, n) ? Pace.Careful : Pace.Quick, sky, enc).TotalMinutes;
    }
    Dictionary<TileId, double> Distances(World w, TileId from)
    {
        if (distCache.Count > 0 && distCache.Keys.First().Item2 != w.Minute) distCache.Clear();
        if (distCache.TryGetValue((from, w.Minute), out var cached)) return cached;
        return distCache[(from, w.Minute)] = DistancesUncached(w, from);
    }
    Dictionary<TileId, double> DistancesUncached(World w, TileId from)
    {
        var m = Map.Of(w);
        var me = Me(w);
        var explored = Knows(w).Explored;
        var sky = Weather.At(w, w.Minute);
        double enc = Items.EncumbrancePct(me);
        var dist = new Dictionary<TileId, double> { [from] = 0 };
        var pq = new PriorityQueue<TileId, double>();
        pq.Enqueue(from, 0);
        while (pq.TryDequeue(out var t, out var d))
        {
            if (d > dist[t]) continue;
            foreach (var dir in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
            {
                var n = Map.Step(t, dir);
                if (!m.InBounds(n) || Math.Abs(n.X - from.X) + Math.Abs(n.Y - from.Y) > Radius) continue;
                if (!m.TerrainAt(n).IsPassable() || Map.Blocked(m, t, n) != null) continue;
                if (explored != null && !explored.Contains(n)) continue;
                double step = !crude.TravelByTrip ? Traversal.MinimumCrossingMinutes
                    : EdgeCache ? EdgeMinutes(w, me, t, n, sky, enc)
                    : Trip.Terms(w, me.Body, t, n, Trip.IsHazardousNow(w, t) || Trip.IsHazardousNow(w, n) ? Pace.Careful : Pace.Quick, sky, enc).TotalMinutes;
                double nd = d + step;
                if (nd < dist.GetValueOrDefault(n, double.MaxValue)) { dist[n] = nd; pq.Enqueue(n, nd); }
            }
        }
        return dist;
    }

    // ================= forecasts =================

    double HoursPerKg(Resource r) => crude.FireByFuelType ? Fire.FuelHours(r, 1) : 1.0;

    /// The camp fire's hours left as the NPC knows them: seen if in sight or underfoot, else remembered and aged.
    double CampFireHours(World w) => FireHoursAt(w, Home);
    double FireHoursAt(World w, TileId site)
    {
        var me = Me(w);
        if (me.Tile == site || Sight.InSight(w, me, site))
        {
            if (Fire.PitAt(w, site) is not FireId id) return 0;
            var f = w.Fires[id];
            if (f.LastUpdatedMinute != w.Minute) return Remembered();
            if (!Fire.IsActive(f)) return 0;
            return crude.FireByFuelType ? Fire.HoursItWillBurn(f) : Fire.TotalKg(f) * 1.0;
        }
        return Remembered();
        double Remembered() => Knows(w).Places.TryGetValue(site, out var mem) && mem.FireHours is double h
            ? Math.Max(0, h - (w.Minute - (mem.VisitedMinute ?? mem.SeenMinute)) / 60.0) : 0;
    }

    /// What the NPC believes the camp stash holds: memory of it (Receipt keeps it current while home).
    SortedDictionary<Resource, double> CampStash(World w) => StashAt(w, Home);
    SortedDictionary<Resource, double> StashAt(World w, TileId t) =>
        Knows(w).Places.TryGetValue(t, out var mem) && mem.Stash != null ? mem.Stash : new();

    /// Core temperature cooling while out, F/hour. A: flat 1 F/h below freezing. B: by the felt air. [forecast]
    double CoolingFPerHour(World w)
    {
        var sky = Weather.At(w, w.Minute);
        double felt = Weather.WindChillF(sky.TemperatureF, sky.WindMph);
        return crude.TravelByTrip ? Math.Max(0, (60 - felt) / 40) : felt < 32 ? 1.0 : 0;
    }

    /// Expected kg/hour of each resource foraged on a tile, from what the NPC knows of it.
    readonly Dictionary<TileId, (Dictionary<Resource, double>, int)> rateCache = [];
    long rateMinute = -1;
    Dictionary<Resource, double> ForageRates(World w, TileId t, out int pool)
    {
        if (rateMinute != w.Minute) { rateCache.Clear(); rateMinute = w.Minute; }
        if (rateCache.TryGetValue(t, out var c)) { pool = c.Item2; return c.Item1; }
        var r = ForageRatesUncached(w, t, out pool);
        rateCache[t] = (r, pool);
        return r;
    }
    Dictionary<Resource, double> ForageRatesUncached(World w, TileId t, out int pool)
    {
        pool = -1;
        var pools = Pools.At(w, t); // generated defs: static facts of the place (its table and base density), not state
        var rates = new Dictionary<Resource, double>();
        for (int i = 0; i < pools.Count; i++)
            if (pools[i] is ForagePool fp)
            {
                pool = i;
                double density = crude.YieldFromMemory && Knows(w).Places.TryGetValue(t, out var mem) && mem.ForageDensity is double d ? d : fp.Density;
                foreach (var it in fp.Items) rates[it.Resource] = rates.GetValueOrDefault(it.Resource) + density * it.Abundance * (it.MinKg + it.MaxKg) / 2;
                break;
            }
        return rates;
    }

    /// Hours of foraging to get `units` at a starting rate of `rate0` units/hour. B: thinning as Pools does,
    /// integrated (units = r0 ln(1+2t)/2), which is a private copy of Pools' rule: the Sim should expose it. [RULE 4]
    double HoursFor(double units, double rate0) => !crude.YieldDepletes ? units / rate0
        : 2 * units / rate0 > 20 ? double.PositiveInfinity : (Math.Exp(2 * units / rate0) - 1) / 2;
    double UnitsIn(double hours, double rate0) => crude.YieldDepletes ? rate0 * Math.Log(1 + 2 * hours) / 2 : rate0 * hours;

    /// Expected yield of one tree (Content felling table), per resource.
    static Dictionary<Resource, double> TreeYield(TreePool def)
    {
        var f = Pools.Felled;
        double logs = (f.Logs.Count[0] + f.Logs.Count[1]) / 2.0;
        var y = new Dictionary<Resource, double>();
        if (def.Wood is Resource wood) y[wood] = logs * f.LogKg[wood];
        else foreach (var (r, share) in new[] { (Resource.Pine, 0.40), (Resource.Birch, 0.35), (Resource.Oak, 0.25) }) y[r] = logs * share * f.LogKg[r]; // the mix is in the table but MixedWoodAt only samples it [RULE 5]
        y[Resource.Kindling] = 5 * 0.35;
        y[Resource.Tinder] = 2.5 * 0.055;
        return y;
    }

    // ================= structured refusals (PROPOSED: what Fire should return) =================

    public static readonly HashSet<Resource> Tinders = [.. Fire.Props.Where(p => p.Value.MinFireTempF == 0 && p.Value.IgnitionBonusPct > 0).Select(p => p.Key)];
    public static readonly HashSet<Resource> Fuels = [.. Fire.Props.Keys];
    static readonly ToolType[] Starters = [ToolType.FireStriker, ToolType.BowDrill, ToolType.HandDrill];

    /// Every shortfall for lighting a fire with what is carried, as data: (thing, amount). Mirrors Fire.Refusal's
    /// StartFire checks, all at once instead of the first. States are listed apart from items.
    public static (List<(string What, double Amount)> Items, List<string> States) LightShortfalls(World w, AgentId who)
    {
        var pack = Items.Pack(w, who);
        var items = new List<(string, double)>();
        var states = new List<string>();
        var tile = w.Agent(who).Tile;
        if (Fire.PitAt(w, tile) is FireId id && Fire.IsActive(w.Fires[id])) states.Add("fire already burning");
        if (!Starters.Any(t => Items.Tool(pack, t) != null)) items.Add(("fire starter", 1));
        double tinder = Tinders.Max(r => Items.Have(pack, r));
        if (tinder < Verbs.TinderKg) items.Add(("tinder", Verbs.TinderKg - tinder));
        return (items, states);
    }

    /// The odds the Start a fire verb shows now (Verb.Odds), or the bare tool odds if no verb is listed.
    double StrikeOdds(World w)
    {
        var v = Verbs.At(w, who).All.FirstOrDefault(v => v.Activity is StartFire);
        return v?.Odds?.TotalPct ?? 0.35;
    }

    // ================= the tree =================

    Req FuelReq(double hours) => new("fuel", Fuels, hours, HoursPerKg);
    static Req TinderReq(double kg) => new("tinder", Tinders, kg, _ => 1);
    static Req KindlingReq(double kg) => new("kindling", new HashSet<Resource> { Resource.Kindling }, kg, _ => 1);
    static Req StarterReq() => new("fire starter", new HashSet<Resource>(), 1, _ => 1, g => Items.Def(g).Tool is ToolType t && Starters.Contains(t));

    /// The tree for "a fire that lasts until sunrise", fresh: the goal's all-of, each child's one-of, leaves.
    public Plan Build(World w, long until) => Build(w, until, Home);
    /// For the cost benchmark: a fresh Dijkstra and fresh tile rates, as a re-plan after a step sees them.
    public void ClearCaches() { distCache.Clear(); rateCache.Clear(); }
    public Dictionary<TileId, double> DistancesFresh(World w) => DistancesUncached(w, Me(w).Tile);
    public Plan Build(World w, long until, TileId Home)
    {
        var me = Me(w);
        var pack = Items.Pack(w, who);
        var here = me.Tile;
        var fromHere = Distances(w, here);
        var fromHome = here == Home ? fromHere : Distances(w, Home);
        darkSkips = 0;
        double homeLeg = fromHome.GetValueOrDefault(here, 0);

        // What can be claimed without leaving: pack, then camp stash. Claimed by the all-of's children in order.
        var packLeft = new Dictionary<Resource, double>(pack.Kg);
        var stashLeft = new Dictionary<Resource, double>(StashAt(w, Home));
        var leaves = new List<Leaf>();
        var impossible = new List<string>();
        int depth = 1;
        double freeKg = Items.CapacityKg(pack) - Items.CarriedKg(me);
        // At home, logs in the pack are room: putting them down is a minute (R13 unloads them).
        if (here == Home) freeKg += pack.Kg.Where(x => Fire.Props.TryGetValue(x.Key, out var fp) && fp.MinFireTempF > 0).Sum(x => x.Value);
        double tripMinutes = 0;
        int unsafeSkips = 0;
        bool stayIsSafe = FireHoursAt(w, Home) > 0 && here == Home;

        double fireHours = FireHoursAt(w, Home);
        double hoursNeeded = Math.Max(0, (until - w.Minute) / 60.0);
        var children = new List<Req>();
        // The fire must be burning when the fuel goes on it: light it if it will be out by the time we're back.
        bool needLight = fireHours * 60 < homeLeg + 1;
        if (needLight)
        {
            children.Add(StarterReq());
            // Tinder for the strikes the odds say it will take, at least two. [RULE 6]
            double odds = StrikeOdds(w);
            children.Add(TinderReq(Verbs.TinderKg * Math.Clamp(1.5 / Math.Max(odds, 0.05), 2, 8)));
            children.Add(KindlingReq(Verbs.KindlingKg));  // the Light refusal never asks for kindling; logs do [RULE 7]
        }
        children.Add(FuelReq(Math.Max(0, hoursNeeded - fireHours)));

        foreach (var req in children)
        {
            double left = req.Amount;
            if (req.IsTool)
            {
                if (pack.Gear.Any(req.Tool!)) continue;
                // one of: a recipe that makes it (depth 2), its inputs from pack/stash/forage (depth 3)
                var recipe = Recipes.All.FirstOrDefault(r => r.Result.Gear is string g && req.Tool!(Items.Make(g)));
                if (recipe == null) { impossible.Add(req.Name); continue; }
                depth = Math.Max(depth, 3);
                leaves.Add(new Leaf(SourceKind.Recipe, Home, -1, req.Name, 1, 0, recipe.Minutes, recipe.Id));
                foreach (var mat in recipe.Materials) Claim(new Req(mat.AnyOf[0].ToString(), mat.AnyOf.ToHashSet(), mat.Kg, _ => 1), mat.Kg);
                continue;
            }
            Claim(req, left);
        }

        // The whole goal's forecast: this trip, scaled by how much of the fuel it covers (later trips look alike).
        double fuelPlanned = leaves.Where(l => l.For.Contains("fuel") && l.Kind != SourceKind.CampStash).Sum(l => l.Units);
        double fuelShort = Math.Max(0, hoursNeeded - fireHours) - leaves.Where(l => l.For.Contains("fuel") && l.Kind == SourceKind.CampStash).Sum(l => l.Units)
            - pack.Kg.Where(x => Fuels.Contains(x.Key)).Sum(x => x.Value * HoursPerKg(x.Key));
        double trips = fuelPlanned > 0.1 ? Math.Max(1, fuelShort / fuelPlanned) : 1;
        double forecast = (leaves.Sum(l => l.WorkMin) + tripMinutes) * trips;
        if (darkSkips > 0) stats.Rule("R8 chop needs light by dark hours");
        return new Plan(leaves, forecast, impossible, depth);

        void Claim(Req req, double left)
        {
            depth = Math.Max(depth, 2);
            // pack
            foreach (var r in req.AnyOf.OrderByDescending(req.UnitsPerKg))
            {
                if (left <= 1e-6) return;
                double kg = Math.Min(packLeft.GetValueOrDefault(r), left / req.UnitsPerKg(r));
                if (kg <= 0) continue;
                packLeft[r] -= kg; left -= kg * req.UnitsPerKg(r);
            }
            // camp stash
            foreach (var r in req.AnyOf.OrderByDescending(req.UnitsPerKg))
            {
                if (left <= 1e-6) return;
                double kg = Math.Min(stashLeft.GetValueOrDefault(r), left / req.UnitsPerKg(r));
                if (kg <= 0) continue;
                stashLeft[r] -= kg; left -= kg * req.UnitsPerKg(r);
                leaves.Add(new Leaf(SourceKind.CampStash, Home, -1, $"{req.Name}:{r}", kg * req.UnitsPerKg(r), kg, 1));
            }
            // away: one of every known tile's forage, trees, and remembered stashes, cheapest per unit first, filled greedily
            while (left > 1e-6)
            {
                Source? best = null; double bestPerUnit = double.MaxValue;
                foreach (var (t, dOut) in fromHere)
                {
                    double travel = dOut + fromHome.GetValueOrDefault(t, dOut) - homeLeg;
                    bool onList = crude.ShareTrips && leaves.Any(l => l.Tile == t && l.Kind is SourceKind.Forage or SourceKind.Chop or SourceKind.Stash);
                    if (onList) travel = 0;
                    foreach (var s in Offers(w, t, req, left, travel, Home))
                    {
                        if (s.Units <= 1e-6) continue;
                        // The forecast must say it survives the trip: core at the end above the Critical line.
                        bool warmHere = t == Home && fireHours > 0;
                        double outMin = dOut + fromHome.GetValueOrDefault(t, dOut) + (warmHere ? 0 : s.WorkMin);
                        // Unsafe only against staying: with no fire to stay by, staying is no safer. [RULE 18]
                        if (stayIsSafe && me.Body.CoreTempF - CoolingFPerHour(w) * outMin / 60 <= Tiers.HypothermiaCoreF) { unsafeSkips++; continue; }
                        double cost = (crude.CostIncludesGather ? s.Minutes : s.TravelMin + 1) / s.Units;
                        if (cost < bestPerUnit) (best, bestPerUnit) = (s, cost);
                    }
                }
                if (best == null) { impossible.Add($"{req.Name} {left:0.##}{(unsafeSkips > 0 ? " (every source fails the survival forecast)" : "")}"); return; }
                // one trip: what fits in the pack
                double kgCap = freeKg - leaves.Where(l => l.Kind != SourceKind.CampStash).Sum(l => l.Kg);
                if (kgCap <= 0.2) return; // the rest is a later trip
                double frac = Math.Min(1, kgCap / Math.Max(best.Kg, 1e-9));
                var take = best with { Units = best.Units * frac, Kg = best.Kg * frac, WorkMin = best.WorkMin * (best.Kind == SourceKind.Chop ? 1 : frac) };
                var same = leaves.FindIndex(l => l.Tile == take.Tile && l.Kind == take.Kind);
                if (same >= 0)
                {
                    var l = leaves[same];
                    leaves[same] = l with { For = l.For.Contains(req.Name) ? l.For : $"{l.For}+{req.Name}", Units = l.Units + take.Units, Kg = l.Kg + take.Kg,
                        WorkMin = take.Kind == SourceKind.Chop ? l.WorkMin : l.WorkMin + take.WorkMin };
                }
                else
                {
                    leaves.Add(new Leaf(take.Kind, take.Tile, take.Pool, req.Name, take.Units, take.Kg, take.WorkMin));
                    tripMinutes += take.TravelMin;
                }
                left -= take.Units;
                if (frac < 1) return;
            }
        }
    }

    /// What one tile offers toward a requirement: its forage, its trees, a stash remembered there.
    int darkSkips;
    IEnumerable<Source> Offers(World w, TileId t, Req req, double want, double travel, TileId Home)
    {
        var pack = Items.Pack(w, who);
        var mem = Knows(w).Places.GetValueOrDefault(t);
        if (t != Home && mem?.Stash is { } st)
        {
            double units = 0, kg = 0;
            foreach (var r in req.AnyOf) { double k = st.GetValueOrDefault(r); kg += k; units += k * req.UnitsPerKg(r); }
            if (units > 0) { double f = Math.Min(1, want / units); yield return new Source(SourceKind.Stash, t, -1, units * f, kg * f, travel, 2); }
        }
        var rates = ForageRates(w, t, out int fpool);
        if (fpool >= 0)
        {
            double rate = 0, kgRate = 0;
            foreach (var r in req.AnyOf) { double k = rates.GetValueOrDefault(r); rate += k * req.UnitsPerKg(r); kgRate += k; }
            if (rate > 1e-6)
            {
                double hours = Math.Min(MaxStintHours, HoursFor(want, rate));
                double units = UnitsIn(hours, rate);
                yield return new Source(SourceKind.Forage, t, fpool, units, units / rate * kgRate, travel, hours * 60);
            }
        }
        if (mem?.Trees == true && Items.Tool(pack, ToolType.Axe) != null)
        {
            var pools = Pools.At(w, t);
            for (int i = 0; i < pools.Count; i++)
                if (pools[i] is TreePool tp && Pools.HasTrees(w, new PoolId(t, i)))
                {
                    var y = TreeYield(tp);
                    double units = 0, kg = 0;
                    foreach (var r in req.AnyOf) { kg += y.GetValueOrDefault(r); units += y.GetValueOrDefault(r) * req.UnitsPerKg(r); }
                    double work = tp.MinutesToFell * (1 - Pools.ChopProgressPct(w, new PoolId(t, i)));
                    // Chopping needs light: a tree that would come down after dark is no source without a torch (depth 3). [state]
                    long done = w.Minute + (long)(travel / 2 + work);
                    if (DarkAt(done) && Items.Tool(pack, ToolType.Torch) == null) { darkSkips++; break; }
                    if (units > 0) yield return new Source(SourceKind.Chop, t, i, Math.Min(units, want), kg * Math.Min(1, want / units), travel, work);
                    break;
                }
        }
    }

    // ================= the step =================

    /// The next activity to run and its minutes. Rebuilds the tree, checks interrupts, picks a leaf.
    public (Activity Act, int Minutes, string Why) Decide(World w)
    {
        var sw = Stopwatch.StartNew();
        var r = DecideInner(w);
        double us = sw.Elapsed.TotalMicroseconds;
        stats.PlanMicros += us; stats.PlanMicrosMax = Math.Max(stats.PlanMicrosMax, us); stats.Plans++;
        return r;
    }

    (Activity, int, string) DecideInner(World w)
    {
        var me = Me(w);
        var pack = Items.Pack(w, who);
        var here = me.Tile;
        long until = NextSunrise(w.Minute);
        if (Weather.MinuteOfDay(w.Minute) / 60 < Weather.SunriseHour(w.Minute) || Weather.MinuteOfDay(w.Minute) / 60 >= 12) { }
        // Tiers. Warmth by the forecast core on reaching home and camp fuel against tonight.
        var fromHome = Distances(w, Home);
        double homeLeg = fromHome.GetValueOrDefault(here, 0);
        double coreAtHome = me.Body.CoreTempF - CoolingFPerHour(w) * homeLeg / 60;
        // Wood counts as warmth only if a fire can be had with it: burning, or the makings in hand. [RULE 17]
        bool lightable = CampFireHours(w) > 0 || LightShortfalls(w, who).Items.Count == 0 && Items.Have(pack, Resource.Kindling) + CampStash(w).GetValueOrDefault(Resource.Kindling) >= 0.3;
        double fuelHours = !lightable ? 0 : CampFireHours(w) + CampStash(w).Where(x => Fuels.Contains(x.Key)).Sum(x => x.Value * HoursPerKg(x.Key))
            + pack.Kg.Where(x => Fuels.Contains(x.Key)).Sum(x => x.Value * HoursPerKg(x.Key));
        double tonight = (until - w.Minute) / 60.0;
        var others = Tiers.Of(w, who);
        // The fire forecast is rough: half an hour short of the night is a night. [RULE 21]
        var warmth = Tiers.WarmthTier(coreAtHome, Goal != null, fuelHours, tonight - 0.5);
        // The ladder: worst tier, ties warmth > water > rest > food. Want-tier warmth is the spare half-day: out of scope.
        var ladder = new[] { (Stat.Warmth, warmth), (Stat.Water, others.Water), (Stat.Rest, others.Rest), (Stat.Food, others.Food) };
        var (stat, tier) = ladder.OrderByDescending(x => x.Item2).First();

        if (Goal != null)
        {
            string? drop = null;
            if (stat != Stat.Warmth && tier > Goal.Tier) drop = $"{stat} reached {tier}";
            else if (w.Minute - Goal.Started > Goal.BudgetMinutes) drop = "time budget";
            else if (here != Home && Tiers.CoreTier(coreAtHome, false) > Goal.Tier) drop = "way home dangerous"; // a trip must not make warmth worse than the need it serves
            else if (warmth < Tier.Need) drop = "done";
            if (drop != null)
            {
                stats.Drops[drop] = stats.Drops.GetValueOrDefault(drop) + 1;
                log($"   -- goal dropped: {drop}");
                Goal = null;
            }
        }

        if (Goal == null && stat == Stat.Warmth && warmth >= Tier.Need)
        {
            if (ChooseSite) ChooseFireSite(w, until);
            var p0 = Build(w, until);
            Goal = new Goal { Started = w.Minute, Until = until, ForecastMinutes = p0.ForecastMinutes, Tier = warmth };
            stats.GoalsMade++;
            log($"   ++ goal: fire until {Clock(until)}, need {Math.Max(0, tonight - CampFireHours(w)):0.0} fuel-h, forecast {Goal.ForecastMinutes:0} min, budget {Goal.BudgetMinutes:0}");
        }

        if (Goal != null)
        {
            var plan = Build(w, Goal.Until);
            if (plan.Impossible.Count > 0) log($"   !! no source for: {string.Join(", ", plan.Impossible)}");
            log($"      list: {string.Join(" | ", plan.Leaves)}   [stash mem: {string.Join(",", CampStash(w).Select(x => $"{x.Key}{x.Value}"))}; true: {(w.Tiles.GetValueOrDefault(Home)?.Stash is {} st ? Harness.Describe(st) : "-")}]");
            var step = Pick(w, plan);
            if (step != null) return step.Value;
            if (plan.Impossible.Count > 0)
            {
                stats.Drops["step impossible"] = stats.Drops.GetValueOrDefault("step impossible") + 1;
                Goal = null;
            }
        }
        return Keep(w, stat, tier);
    }

    /// The goal's site, one of: home, or one of the nearest known places with wood, by forecast minutes to have the
    /// fire there (walk there plus its plan). Committed with the goal: home moves there.
    void ChooseFireSite(World w, long until)
    {
        var fromHere = Distances(w, Me(w).Tile);
        var candidates = fromHere.Where(x => x.Key != Home && (Pools.At(w, x.Key).Any(p => p is TreePool) ||
                ForageRates(w, x.Key, out _).Keys.Any(r => Fire.Props.TryGetValue(r, out var fp) && fp.MinFireTempF > 0)))
            .OrderBy(x => x.Value).Take(6).Select(x => x.Key).Prepend(Home).ToList();
        TileId best = Home; double bestCost = double.MaxValue, homeCost = 0; string why = "";
        foreach (var site in candidates)
        {
            var p = Build(w, until, site);
            // A plan held back only by the survival forecast is "stay put", not "impossible".
            bool blocked = p.Impossible.Any(x => !x.Contains("survival forecast"));
            double cost = fromHere.GetValueOrDefault(site, 999) + p.ForecastMinutes + (blocked ? 10000 : 0);
            if (site == Home) homeCost = cost;
            // Home is kept unless another site is clearly quicker: the forecasts are too rough to split hairs. [RULE 20]
            else if (cost > 0.75 * homeCost - 15) continue;
            if (cost < bestCost) (best, bestCost, why) = (site, cost, $"walk {fromHere.GetValueOrDefault(site):0} + plan {p.ForecastMinutes:0}, home {homeCost:0}");
        }
        if (best != Home)
        {
            stats.Rule("SITE camp moves to the fire's site");
            log($"   >> fire site: {Leaf.Rel(best)} ({why}) instead of home ({Leaf.Rel(Home)}): home moves");
            Home = best;
            Leaf.Home = best;
        }
    }

    /// No goal (the fire will last, or nothing can be done): keep the fire and rest beside it.
    (Activity, int, string) Keep(World w, Stat stat, Tier tier)
    {
        var me = Me(w);
        if (me.Tile != Home) return WalkToward(w, Home, "home: nothing to do out here");
        // Maintenance: feed the fire from the stash whenever it has room and needs it, and sleep between. [RULE 9]
        if (Feed(w, out var feed)) return feed;
        double fireH = CampFireHours(w);
        if (fireH <= 0) return (new Wait(who), 15, "wait: no fire, no plan");
        stats.Rule("R10 sleep timed to the fire");
        double need = (NextSunrise(w.Minute) - w.Minute) / 60.0;
        // The fire outlasts the night: sleep to morning. Else wake when the pit has room, or before it dies.
        int minutes = (int)Math.Clamp(fireH >= need ? need * 60 : Math.Min(RoomInMinutes(w), fireH * 60 - 30), 15, 8 * 60);
        return (new Sleep(who), minutes, $"sleep {minutes}m (fire {fireH:0.0}h, {stat} {tier})");
    }

    /// Minutes until the pit has room for a kilo, at its current burn rate.
    double RoomInMinutes(World w)
    {
        if (Fire.PitAt(w, Home) is not FireId id) return 480;
        var f = w.Fires[id];
        double over = Fire.TotalKg(f) - (Fire.MaxFuelKg(f.Pit) - Verbs.FuelKg);
        double rate = Fire.BurnRateKgPerHour(f);
        return over <= 0 ? 0 : rate <= 0 ? 480 : over / rate * 60;
    }

    /// Feeding the fire: unstash what it needs, add what it accepts. Uses Verbs.At and its refusals.
    bool Feed(World w, out (Activity, int, string) step)
    {
        step = default;
        var me = Me(w);
        var pack = Items.Pack(w, who);
        if (me.Tile != Home || Fire.PitAt(w, Home) is not FireId id || !Fire.IsActive(w.Fires[id])) return false;
        double fireH = CampFireHours(w), need = (NextSunrise(w.Minute) - w.Minute) / 60.0;
        if (fireH >= need) return false;
        // Stoke only with room for a full kilo: AddFuel takes a sliver into a nearly full pit. [RULE 15]
        if (RoomInMinutes(w) > 0) return false;
        var verbs = Verbs.At(w, who);
        var adds = verbs.Things.Where(t => t.Thing.Kind == ThingKind.Fire).SelectMany(t => t.Menu).Where(v => v.Activity is AddFuel).ToList();
        foreach (var v in adds.Where(v => !v.Allowed)) Note(v.Refused!);
        // Most hours per kilo among what it accepts; kindling and tinder are kept back for lighting while logs wait. [RULE 11]
        var ok = adds.Where(v => v.Allowed).Select(v => (v, r: ((AddFuel)v.Activity).Fuel)).ToList();
        var logs = ok.Where(x => Fire.Props[x.r].MinFireTempF > 0).OrderByDescending(x => Fire.FuelHours(x.r, 1)).ToList();
        if (logs.Count > 0) { step = (logs[0].v.Activity, 1, $"stoke {logs[0].r}"); return true; }
        bool logsWaiting = adds.Any(v => !v.Allowed && Fire.Props[((AddFuel)v.Activity).Fuel].MinFireTempF > 0 && v.Refused!.Contains("not hot enough"));
        if (logsWaiting)
        {
            // State: too cold for logs. Its verb: add quick fuel if any, else wait for it to heat. [RULE 12]
            var quick = ok.FirstOrDefault(x => x.r == Resource.Kindling);
            if (quick.v != null) { step = (quick.v.Activity, 1, "kindling: fire too cold for logs"); return true; }
            step = (new Wait(who), 5, "wait: fire too cold for logs"); return true;
        }
        bool full = adds.Any(v => !v.Allowed && v.Refused!.Contains("full"));
        if (full) return false;
        if (ok.Count > 0 && pack.Kg.Where(x => Fuels.Contains(x.Key) && Fire.Props[x.Key].MinFireTempF > 0).Sum(x => x.Value) <= 0)
        {
            // only kindling/tinder in hand: unstash logs first if there are any
        }
        // Unstash fuel from the camp stash.
        var take = verbs.Things.Where(t => t.Thing.Kind == ThingKind.Stash).SelectMany(t => t.Menu)
            .Where(v => v.Allowed && v.Activity is Unstash u && Fuels.Contains(u.Resource) && Fire.Props[u.Resource].MinFireTempF > 0)
            .OrderByDescending(v => Fire.FuelHours(((Unstash)v.Activity).Resource, 1)).FirstOrDefault();
        if (take != null && pack.Kg.Where(x => Fire.Props.ContainsKey(x.Key) && Fire.Props[x.Key].MinFireTempF > 0).Sum(x => x.Value) < 0.5)
        {
            var u = (Unstash)take.Activity;
            step = (u with { Kg = Math.Min(u.Kg, 3) }, 1, $"unstash {u.Resource}"); return true;
        }
        var kindling = ok.FirstOrDefault(x => x.r == Resource.Kindling);
        if (kindling.v != null) { step = (kindling.v.Activity, 1, "stoke kindling (no logs)"); return true; }
        return false;
    }

    /// The shopping list to an action: home leaves at home, then nearest away leaf, then home.
    (Activity, int, string)? Pick(World w, Plan plan)
    {
        var me = Me(w);
        var pack = Items.Pack(w, who);
        var here = me.Tile;
        if (here == Home)
        {
            // Light first, when the refusal says nothing is missing.
            var (short_, states) = LightShortfalls(w, who);
            bool burning = CampFireHours(w) > 0;
            bool gatherLeft = plan.Leaves.Any(l => l.Kind is SourceKind.Forage or SourceKind.Chop or SourceKind.Stash);
            bool roomLeft = Items.CapacityKg(pack) - Items.CarriedKg(me) > 0.3;
            bool coldNow = Tiers.CoreTier(me.Body.CoreTempF, false) >= Tier.Need;
            // An all-of's consuming verb runs when its inputs are in hand: light once this trip's list is
            // gathered (or the pack is full), unless the body needs the warmth now. [RULE 14]
            if (!burning && short_.Count == 0 && (!gatherLeft || !roomLeft || coldNow))
            {
                if (coldNow && gatherLeft) stats.Rule("R14 light early: cold now");
                var v = Verbs.At(w, who).All.FirstOrDefault(v => v.Activity is StartFire);
                if (v != null && v.Allowed) return (v.Activity, 1, $"light ({v.Odds?.TotalPct:P0})");
                if (v != null) { Note(v.Refused!); stats.Drift.Add($"{Clock(w.Minute)} proposed ok, Sim: {v.Refused}"); }
            }
            if (Feed(w, out var feed)) return feed;
            var make = plan.Leaves.FirstOrDefault(l => l.Kind == SourceKind.Recipe);
            if (make != null)
            {
                var v = Verbs.At(w, who).Make.First(v => v.Activity is Make m && m.Recipe == make.Recipe);
                if (v.Allowed) return (v.Activity, Verbs.UntilDoneMinutes, $"make {make.Recipe}");
                Note(v.Refused!);
            }
            // Stashed fuel goes onto the fire straight from the stash (Feed); only makings are taken up. [RULE 16]
            var stash = plan.Leaves.FirstOrDefault(l => l.Kind == SourceKind.CampStash && !l.For.StartsWith("fuel"));
            if (stash != null)
            {
                var r = Enum.Parse<Resource>(stash.For.Split(':')[1]);
                var v = Verbs.At(w, who).Things.SelectMany(t => t.Menu).FirstOrDefault(v => v.Activity is Unstash u && u.Resource == r);
                if (v is { Allowed: true }) return (((Unstash)v.Activity) with { Kg = Math.Min(stash.Kg + 0.05, ((Unstash)v.Activity).Kg) }, 1, $"unstash {r} for {stash.For}");
                if (v != null) Note(v.Refused!);
            }
            // Leaving: put down the fuel in the pack the fire can't take yet, to carry more. [RULE 13]
            var away = plan.Leaves.Where(l => l.Kind is SourceKind.Forage or SourceKind.Chop or SourceKind.Stash || l.Kind == SourceKind.CampStash && !l.For.StartsWith("fuel")).ToList();
            if (away.Count > 0)
            {
                var logs = pack.Kg.Where(x => Fire.Props.ContainsKey(x.Key) && Fire.Props[x.Key].MinFireTempF > 0 && x.Value > 0).ToList();
                if (logs.Count > 0) { stats.Rule("R13 unload before a trip"); return (new Stash(who, logs[0].Key, logs[0].Value), 1, $"unload {logs[0].Key} before the trip"); }
            }
        }
        var awayLeaves = plan.Leaves.Where(l => l.Kind is SourceKind.Forage or SourceKind.Chop or SourceKind.Stash).ToList();
        double room = Items.CapacityKg(pack) - Items.CarriedKg(me);
        if (awayLeaves.Count > 0 && room > 0.3)
        {
            var dist = Distances(w, here);
            var next = awayLeaves.OrderBy(l => dist.GetValueOrDefault(l.Tile, 999)).First(); // nearest place first
            string target = $"{next.Kind}@{Leaf.Rel(next.Tile)}";
            if (stats.LastTarget != null && stats.LastTarget != target && here != next.Tile) stats.SourceFlips++;
            stats.LastTarget = target;
            if (here != next.Tile) return WalkToward(w, next.Tile, $"to {next} [{plan.Leaves.Count} leaves]");
            switch (next.Kind)
            {
                case SourceKind.Forage:
                {
                    var g = new Gather(who, next.Pool, ForageFocus.Fuel);
                    var refused = Sim.Refusal(w, g);
                    if (refused != null) { Note(refused); break; }
                    return (g, (int)Math.Clamp(next.WorkMin, 10, StintMinutes), $"forage {next}");
                }
                case SourceKind.Chop:
                {
                    var g = new Gather(who, next.Pool);
                    var refused = Sim.Refusal(w, g);
                    if (refused != null) { Note(refused); break; }
                    return (g, Math.Max(10, (int)next.WorkMin + 1), $"chop {next}");
                }
                case SourceKind.Stash:
                {
                    var v = Verbs.At(w, who).Things.SelectMany(t => t.Menu).Where(v => v.Activity is Unstash u && Fuels.Contains(u.Resource))
                        .OrderByDescending(v => Fire.FuelHours(((Unstash)v.Activity).Resource, 1)).FirstOrDefault();
                    if (v is { Allowed: true }) return (v.Activity, 1, $"pick up {((Unstash)v.Activity).Resource}");
                    if (v != null) Note(v.Refused!);
                    break;
                }
            }
        }
        if (here != Home && (awayLeaves.Count == 0 || room <= 0.3)) return WalkToward(w, Home, room <= 0.3 ? "home: pack full" : "home: list done");
        return null;
    }

    (Activity, int, string) WalkToward(World w, TileId target, string why)
    {
        var me = Me(w);
        var dist = Distances(w, target); // from the target: step to the neighbour closest to it
        var best = new[] { Direction.North, Direction.East, Direction.South, Direction.West }
            .Select(d => (d, n: Map.Step(me.Tile, d))).Where(x => dist.ContainsKey(x.n) && Sim.Refusal(w, new Travel(who, x.d)) == null)
            .OrderBy(x => dist[x.n]).FirstOrDefault();
        if (best.n == default)
        {
            var why2 = Sim.Refusal(w, new Travel(who, Direction.North)) ?? "no known way";
            Note(why2);
            return (new Rest(who), 30, $"rest: can't walk ({why2})");
        }
        // Pace, one of: quick or careful, by minutes plus the injury odds priced at an hour each. [RULE 19]
        var paces = Verbs.At(w, who).Walk.Where(v => v.Allowed && v.Activity is Travel t && t.Dir == best.d)
            .OrderBy(v => v.Trip!.TotalMinutes + 60 * v.Risk!.AnyPct).ToList();
        var pick = paces.First();
        var go = (Travel)pick.Activity;
        stats.RiskSeen.Add(pick.Risk!.AnyPct);
        if (go.Pace == Pace.Careful) stats.Rule("R19 careful pace");
        return (go, Verbs.UntilDoneMinutes, $"{why} ({go.Pace} {pick.Trip!.TotalMinutes}m, risk {pick.Risk!.AnyPct:P0})");
    }

    void Note(string reason) => stats.Refusals[reason] = stats.Refusals.GetValueOrDefault(reason) + 1;
    public static string Clock(long minute) { int m = Weather.MinuteOfDay(minute); return $"{m / 60:00}:{m % 60:00}"; }
}
