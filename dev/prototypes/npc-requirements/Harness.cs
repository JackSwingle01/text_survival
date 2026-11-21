// PROTOTYPE, throwaway. Sets up each scenario on the real Sim (World.New + Sim.Step) and runs the planner
// for one night, the NPC played through the player's agent so no old NPC mind runs alongside it.
using Survival;

namespace NpcReq;

public sealed record Scenario(string Name, string Camp, bool Snowy, Action<World, AgentId, TileId>? Setup = null, bool Axe = true);

public static class Harness
{
    public static int Seed = 5;
    static readonly long WinterBase = (349 - 182) * 1440L + 6 * 60; // Dec 15, 15:00

    public static IEnumerable<string> Run(Scenario sc, Crude crude, bool trace, bool bench = false, bool chooseSite = true)
    {
        var w = World.New(Seed);
        var m = Map.Of(w);
        var who = AgentId.Player;
        // No herds: this is about fire logistics, not predators. [harness]
        foreach (var id in w.Agents.Keys.Where(k => k != who).ToList()) w.Agents.Remove(id);

        var camp = sc.Camp == "forest" ? FindForestCamp(w) : FindPlainCamp(w, 3);
        w.Minute = PickEvening(w, sc.Snowy);
        var me = w.Agent(who);
        me.Tile = camp;
        var pack = Items.Pack(w, who);
        if (sc.Axe) pack.Gear.Add(Items.Make("stone_axe"));
        Items.Add(pack, Resource.Water, 1.0);
        Items.Add(pack, Resource.DriedMeat, 0.3);
        sc.Setup?.Invoke(w, who, camp);
        // What it knows: it has lived here a while. It has stood on camp and looked around.
        var ev = new List<WorldEvent>();
        Knowledge.Observe(w, me, ev);

        var stats = new Stats();
        var lines = new List<string>();
        void Log(string s) { if (trace) Console.WriteLine(s); }
        Leaf.Home = camp; Leaf.Origin = camp;
        var origin = camp;
        var planner = new Planner(who, camp, crude, stats, Log) { ChooseSite = chooseSite };
        var sky0 = Weather.At(w, w.Minute);
        Console.WriteLine($"## {sc.Name} - forecasts {crude.Name}");
        Console.WriteLine($"start {Planner.Clock(w.Minute)}, {sky0.Condition} {sky0.TemperatureF:0}F wind {sky0.WindMph:0} mph, camp {m.TerrainAt(camp)}, sunrise {Weather.SunriseHour(w.Minute)}:00, pack {Describe(pack)}");

        long end = NextSunrise(w.Minute);
        if (bench)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int N = 200;
            long until = end;
            for (int i = 0; i < N; i++) { planner.ClearCaches(); planner.Build(w, until); }
            double build = sw.Elapsed.TotalMicroseconds / N; sw.Restart();
            for (int i = 0; i < N; i++) planner.DistancesFresh(w);
            double dij = sw.Elapsed.TotalMicroseconds / N; sw.Restart();
            for (int i = 0; i < N; i++) Verbs.At(w, who);
            double verbs = sw.Elapsed.TotalMicroseconds / N; sw.Restart();
            for (int i = 0; i < N; i++) { planner.ClearCaches(); planner.Decide(w); planner.Goal = null; }
            double decide = sw.Elapsed.TotalMicroseconds / N;
            return [$"BENCH {sc.Name} / {crude.Name}: Build {build:0} us (of which one Dijkstra {dij:0} us, two per Build), Decide-with-site-choice {decide:0} us, Verbs.At {verbs:0} us"];
        }
        double minCore = me.Body.CoreTempF;
        int fireOutMinutes = 0, nightOut = 0;
        bool wasHome = true;
        var reversals = new NpcShuttleReversals();
        long fireFirstLit = -1;
        var injuries = new List<string>();
        while (w.Minute < end && Body.IsAlive(me.Body))
        {
            var (act, minutes, why) = planner.Decide(w);
            stats.Steps++;
            long t0 = w.Minute;
            var r = Sim.Step(w, act, Math.Max(1, Math.Min(minutes, (int)(end - w.Minute))));
            reversals.See(r.Events, who);
            foreach (var e in r.Events)
            {
                if (e is FireLit && fireFirstLit < 0) fireFirstLit = e.Minute;
                if (e is Injured inj && inj.Who == who) { injuries.Add($"{Planner.Clock(inj.Minute)} {inj.Region} {inj.Type} {inj.Amount:0.00} during {why}"); if (trace) Console.WriteLine($"   ** injured: {injuries[^1]}"); }
            }
            if (r.Outcome == StepOutcome.Refused) stats.Refusals[r.Reason!] = stats.Refusals.GetValueOrDefault(r.Reason!) + 1;
            if (r.Outcome == StepOutcome.Interrupted) stats.Interruptions[r.Reason!] = stats.Interruptions.GetValueOrDefault(r.Reason!) + 1;
            if (r.Outcome == StepOutcome.Refused && r.MinutesElapsed == 0) Sim.Step(w, new Wait(who), 1); // never spin
            minCore = Math.Min(minCore, me.Body.CoreTempF);
            camp = planner.Home;
            bool home = me.Tile == camp;
            if (wasHome && !home) stats.Trips++;
            wasHome = home;
            // Fire out after first lit, in the dark hours before sunrise.
            if (fireFirstLit >= 0 && !(Fire.PitAt(w, camp) is FireId fid && Fire.IsActive(w.Fires[fid]))) fireOutMinutes += (int)(w.Minute - t0);
            // The night: from 21:00, the dark hours, to sunrise.
            if (w.Minute - Weather.MinuteOfDay(w.Minute) + 21 * 60 <= w.Minute || Weather.MinuteOfDay(w.Minute) < 12 * 60)
                if (!(Fire.PitAt(w, camp) is FireId nid && Fire.IsActive(w.Fires[nid]))) nightOut += (int)(w.Minute - t0);
            var fireNow = Fire.PitAt(w, camp) is FireId f2 ? w.Fires[f2] : null;
            string fire = fireNow == null ? "no fire" : Fire.IsActive(fireNow) ? $"fire {Fire.HoursItWillBurn(fireNow):0.0}h/{Fire.TotalKg(fireNow):0.0}kg" : "fire out";
            if (trace && (act is not Travel || r.Outcome != StepOutcome.Finished || true))
                Console.WriteLine($"{Planner.Clock(t0)} {Leaf.Rel(me.Tile),-8} {why,-58} -> {r.Outcome}{(r.Reason is string s && r.Outcome != StepOutcome.Finished ? $" ({s})" : "")} | {fire} | core {me.Body.CoreTempF:0.0} | {Describe(pack)}");
        }
        var final = Fire.PitAt(w, camp) is FireId ff ? w.Fires[ff] : null;
        bool lasted = final != null && Fire.IsActive(final);
        var summary = new List<string>
        {
            $"RESULT {sc.Name} / {crude.Name}: fire-site={Leaf.Rel(planner.Home)} alive={Body.IsAlive(me.Body)} fire-at-sunrise={(lasted ? "yes" : "NO")} fire-out-min-after-first-lit={fireOutMinutes} night-out={nightOut} " +
            $"{(Body.IsAlive(me.Body) ? "" : $"death=({me.Body.DeathCause?.What}, hyd {me.Body.HydrationPct:P0}, energy {me.Body.EnergyPct:P0}) ")}first-lit={(fireFirstLit < 0 ? "never" : Planner.Clock(fireFirstLit))} trips={stats.Trips} reversals={reversals.Count} goals={stats.GoalsMade} " +
            $"drops=[{string.Join(", ", stats.Drops.Select(d => $"{d.Key}:{d.Value}"))}] flips={stats.SourceFlips} minCore={minCore:0.0} " +
            $"plan={stats.PlanMicros / Math.Max(1, stats.Plans):0}us avg/{stats.PlanMicrosMax:0}us max over {stats.Plans}",
            $"  refusals: {string.Join("; ", stats.Refusals.Select(x => $"{x.Key} x{x.Value}"))}",
            $"  interruptions: {string.Join("; ", stats.Interruptions.Select(x => $"{x.Key} x{x.Value}"))}",
            $"  rules fired: {string.Join("; ", stats.Rules.Select(x => $"{x.Key} x{x.Value}"))}",
        };
        if (injuries.Count > 0) summary.Add($"  injuries: {string.Join("; ", injuries)}");
        if (stats.Drift.Count > 0) summary.Add($"  DRIFT: {string.Join("; ", stats.Drift.Distinct())}");
        return summary;
    }

    static long NextSunrise(long minute)
    {
        int mod = Weather.MinuteOfDay(minute), rise = Weather.SunriseHour(minute) * 60;
        return minute + (mod < rise ? rise - mod : 1440 - mod + rise);
    }

    public static string Describe(Inventory p) => string.Join(" ", p.Kg.Where(x => x.Value > 0.005 && x.Key is not (Resource.Water or Resource.DriedMeat)).Select(x => $"{x.Key}{x.Value:0.0#}"))
        + (p.Gear.Any(g => Items.Def(g).Tool == ToolType.Axe) ? " +axe" : "");

    /// A winter evening at 15:00 with the sky asked for: snowing, or dry and cold.
    static long PickEvening(World w, bool snowy)
    {
        for (int d = 0; d < 60; d++)
        {
            long m = WinterBase + d * 1440L;
            var s = Weather.At(w, m);
            var later = Weather.At(w, m + 6 * 60);
            bool snow = s.Condition is WeatherCondition.LightSnow or WeatherCondition.HeavySnow && later.Condition is WeatherCondition.LightSnow or WeatherCondition.HeavySnow or WeatherCondition.Blizzard;
            bool dry = s.Condition is WeatherCondition.Clear or WeatherCondition.Cloudy && later.Condition is WeatherCondition.Clear or WeatherCondition.Cloudy;
            if (snowy ? snow && s.TemperatureF < 25 : dry && s.TemperatureF is > 0 and < 20) return m;
        }
        throw new InvalidOperationException("no such evening");
    }

    static bool Wooded(World w, TileId t) => Pools.At(w, t).Any(p => p is TreePool);
    static bool WoodForage(World w, TileId t) => Pools.At(w, t).OfType<ForagePool>().Any(f => f.Items.Any(i => i.Resource is Resource.Pine or Resource.Birch));

    static IEnumerable<TileId> Valley(World w)
    {
        var m = Map.Of(w);
        for (int y = 30; y < m.Height - 5; y++)
            for (int x = 5; x < m.Width - 5; x++)
            {
                var t = new TileId(x, y);
                if (m.SiteAt(t) == null && t != m.Camp && m.CaveAt(t) == Cave.None) yield return t;
            }
    }

    static TileId FindForestCamp(World w)
    {
        var m = Map.Of(w);
        return Valley(w).First(t => m.TerrainAt(t) == Terrain.Forest && Neighbours(t).All(n => m.TerrainAt(n) == Terrain.Forest && m.SiteAt(n) == null));
    }

    static TileId FindPlainCamp(World w, int d)
    {
        var m = Map.Of(w);
        return Valley(w).First(t => m.TerrainAt(t) == Terrain.Plain && Nearest(w, t, x => Wooded(w, x) || WoodForage(w, x)) == d && Nearest(w, t, x => Wooded(w, x)) == d);
    }

    static int Nearest(World w, TileId t, Func<TileId, bool> pred)
    {
        var m = Map.Of(w);
        for (int r = 0; r <= 6; r++)
            for (int dx = -r; dx <= r; dx++)
            {
                int dy = r - Math.Abs(dx);
                foreach (var y in dy == 0 ? new[] { t.Y } : new[] { t.Y - dy, t.Y + dy })
                {
                    var n = new TileId(t.X + dx, y);
                    if (m.InBounds(n) && pred(n)) return r;
                }
            }
        return 99;
    }

    static IEnumerable<TileId> Neighbours(TileId t) => [new(t.X + 1, t.Y), new(t.X - 1, t.Y), new(t.X, t.Y + 1), new(t.X, t.Y - 1)];
}

/// The shuttle test's reversal count (Sim.Tests NpcShuttleTests.Reversals), copied: a one-tile move straight back
/// within 15 minutes having done nothing on the tile turned back from.
public sealed class NpcShuttleReversals
{
    Moved? previous; bool worked;
    public int Count { get; private set; }
    public void See(IEnumerable<WorldEvent> events, AgentId who)
    {
        foreach (var e in events)
        {
            if (e switch { Gathered g => g.Who == who, Stashed s => s.Who == who, Unstashed u => u.Who == who, TreeFelled t => t.Who == who, FuelAdded f => f.By == who, FireLit l => l.By == who, _ => false }) worked = true;
            if (e is not Moved mv || mv.Who != who) continue;
            if (previous != null && mv.To == previous.From && mv.Minute - previous.Minute <= 15 && !worked) Count++;
            (previous, worked) = (mv, false);
        }
    }
}
