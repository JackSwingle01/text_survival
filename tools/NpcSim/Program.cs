using System.Diagnostics;
using System.Globalization;
using text_survival.Actors;

namespace NpcSim;

/// <summary>
/// Batch runner for the NPC survival simulation.
///
/// This used to be a set of xunit tests, which was the wrong shape for it: the "tests"
/// asserted nothing, took minutes, and had to run serially because the harness redirected
/// Console.Out. It is an experiment bench, so it lives in tools/ next to PixelArtCli and
/// emits CSV that can be diffed between two versions of the AI rather than console tables
/// that have to be read by eye.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        var options = Options.Parse(args);

        return args[0] switch
        {
            "run" => RunBatch(options),
            "verify" => Verify(options),
            _ => Unknown(args[0]),
        };
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'. Try --help.");
        return 2;
    }

    private static void PrintUsage() => Console.WriteLine("""
        npcsim - NPC survival simulation bench

          npcsim run     [options]   run seeds and report survival metrics
          npcsim verify  [options]   assert the same seed produces identical runs

        Options:
          --seeds 1-10 | 1,2,3   seeds to run                 (default 1-10)
          --days N               simulated days per run       (default 7)
          --scenario NAME        baseline | firelit | npcatcamp (default baseline)
          --group N              members sharing one camp; 0 = solo NPCSimulation (default 0)
          --boldness X           override Boldness 0..1       (default: leave as rolled)
          --parallel N           worker threads (default 1; >1 is NOT yet reproducible)
          --out FILE.csv         also write per-seed rows as CSV
          --trace                capture per-decision narration (much slower)
          --fuel-target KG       override the camp fuel stockpile target (default 40)
          --gear NAME            starting | fresh | best  (default starting)

        Examples:
          npcsim run --seeds 1-10 --days 7 --out before.csv
          npcsim run --seeds 1-10 --days 7 --boldness 1.0 --out bold.csv
          npcsim verify --seeds 1-5
        """);

    private static int RunBatch(Options o)
    {
        var sw = Stopwatch.StartNew();
        var rows = Execute(o);
        sw.Stop();

        rows = rows.OrderBy(r => r.Seed).ToList();

        Console.WriteLine($"scenario={o.Scenario} days={o.Days} seeds={rows.Count} " +
            $"group={(o.GroupSize > 0 ? o.GroupSize.ToString() : "solo")} " +
            $"boldness={(o.Boldness?.ToString("F2", CultureInfo.InvariantCulture) ?? "rolled")} " +
            $"parallel={o.Parallelism}");
        Console.WriteLine();
        Console.WriteLine($"{"Seed",5} | {"Days",6} | {"Death",-18} | {"FireMin",7} | {"Sticks",6} | {"MaxDist",7} | {"Tiles",5} | {"TileRev",7}");
        foreach (var r in rows)
            Console.WriteLine($"{r.Seed,5} | {r.Days,6:F2} | {r.DeathCause,-18} | {r.FireMinutes,7} | " +
                $"{r.Sticks,6} | {r.MaxDistance,7} | {r.UniqueTiles,5} | {r.TileReversals,7}");

        Console.WriteLine();
        Console.WriteLine($"mean days      : {rows.Average(r => r.Days):F2}");
        Console.WriteLine($"died           : {rows.Count(r => r.Died)}/{rows.Count}");
        Console.WriteLine($"mean tileRev   : {rows.Average(r => r.TileReversals):F1}");
        var causes = rows.GroupBy(r => r.DeathCause).OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}={g.Count()}");
        Console.WriteLine($"mean cacheFuel : {rows.Average(r => r.CacheFuelKg):F1} kg   " +
            $"mean cacheWater: {rows.Average(r => r.CacheWaterL):F2} L   " +
            $"mean fuelGath  : {rows.Average(r => r.FuelGatheredKg):F1} kg");
        Console.WriteLine($"maxBodyTempF   : {rows.Max(r => r.MaxBodyTempF):F1}   " +
            $"mean minsOver99F: {rows.Average(r => r.MinutesOver99F):F0}");
        Console.WriteLine($"causes         : {string.Join(", ", causes)}");
        Console.WriteLine($"wall clock     : {sw.Elapsed.TotalSeconds:F1}s");

        if (o.OutPath != null)
        {
            WriteCsv(o.OutPath, rows);
            Console.WriteLine($"wrote          : {o.OutPath}");
        }
        return 0;
    }

    private static void WriteCsv(string path, List<RunRow> rows)
    {
        using var w = new StreamWriter(path);
        w.WriteLine("seed,days,survivedMinutes,died,deathCause,fireMinutes,sticks,maxDistance,uniqueTiles,tilesMoved,tileReversals,minutesBelowWarm25,cacheFuelKg,cacheWaterL,fuelGatheredKg,waterGatheredL");
        foreach (var r in rows)
            w.WriteLine(string.Join(",",
                r.Seed, r.Days.ToString("F4", CultureInfo.InvariantCulture), r.SurvivedMinutes,
                r.Died, Csv(r.DeathCause), r.FireMinutes, r.Sticks, r.MaxDistance,
                r.UniqueTiles, r.TilesMoved, r.TileReversals, r.MinutesBelowWarm25,
                r.CacheFuelKg.ToString("F2", CultureInfo.InvariantCulture),
                r.CacheWaterL.ToString("F2", CultureInfo.InvariantCulture),
                r.FuelGatheredKg.ToString("F2", CultureInfo.InvariantCulture),
                r.WaterGatheredL.ToString("F2", CultureInfo.InvariantCulture)));
    }

    private static string Csv(string s) => s.Contains(',') ? $"\"{s}\"" : s;

    private static List<RunRow> Execute(Options o)
    {
        var rows = new List<RunRow>();
        var gate = new object();

        Parallel.ForEach(o.Seeds, new ParallelOptions { MaxDegreeOfParallelism = o.Parallelism }, seed =>
        {
            var row = o.GroupSize > 0 ? RunGroup(o, seed) : RunSolo(o, seed);
            lock (gate) rows.Add(row);
        });

        return rows;
    }

    private static RunRow RunSolo(Options o, int seed)
    {
        if (o.FuelTarget is double t) NPC.FuelStockpileTargetKg = t;
        var sim = NPCSimulation.Create(o.Scenario, seed, o.Personality());
        Reclothe(sim, o.Gear);
        sim.Run(o.Minutes, captureTileView: false, traceDecisions: o.Trace);
        var s = sim.Summarize();

        return new RunRow(seed, s.SurvivedMinutes / 1440.0, s.SurvivedMinutes, s.Died,
            s.Died ? (s.DeathCause ?? "unknown") : "survived",
            s.CampFireActiveMinutes, s.SticksGathered, s.MaxDistanceFromCamp,
            s.UniqueTilesVisited, s.TotalTilesMoved, TileReversals(sim), s.MinutesBelowWarm25,
            s.CacheFuelKgFinal, s.CacheWaterLFinal, s.FuelGatheredKg, s.WaterGatheredL,
            sim.Snapshots.Count > 0 ? sim.Snapshots.Max(x => x.BodyTempF) : 0,
            sim.Snapshots.Count(x => x.BodyTempF > 99.0));
    }

    /// <summary>
    /// The starting kit has no head covering at all - no EquipSlot.Head gear exists outside
    /// crafting recipes - so "best" adds nothing there either; it upgrades chest and legs to
    /// the warmest hide the game defines.
    /// </summary>
    private static void Reclothe(NPCSimulation sim, string gear)
    {
        if (gear == "starting") return;
        var inv = sim.Npc.Inventory;
        if (gear == "best")
        {
            inv.Equip(text_survival.Items.Gear.MammothHideChest());
            inv.Equip(text_survival.Items.Gear.MammothHideLegs());
        }
        else if (gear == "fresh")
        {
            inv.Equip(text_survival.Items.Gear.FurChestWrap());
            inv.Equip(text_survival.Items.Gear.FurLegWraps());
        }
        else throw new ArgumentException($"Unknown --gear '{gear}' (starting|fresh|best)");
        inv.Equip(text_survival.Items.Gear.WornHideBoots(durability: 30));
        inv.Equip(text_survival.Items.Gear.HideHandwraps(durability: 50));
    }

    private static RunRow RunGroup(Options o, int seed)
    {
        if (o.FuelTarget is double t) NPC.FuelStockpileTargetKg = t;
        var sim = NPCGroupSimulation.Create(o.GroupSize, seed, o.Personality());
        sim.Run(o.Minutes);
        var g = sim.Summarize();

        string cause = g.Members.All(m => !m.Died)
            ? "survived"
            : string.Join("/", g.Members.Select(m => m.DeathCause ?? "alive").Distinct());

        return new RunRow(seed, g.AverageSurvivedMinutes / 1440.0, (int)g.AverageSurvivedMinutes,
            g.MembersAliveAtEnd == 0, cause, 0, 0, 0, 0, 0, 0, 0,
            g.CacheFuelKgFinal, g.CacheWaterLFinal, g.FuelGatheredKg, 0, 0, 0);
    }

    /// <summary>
    /// A-B-A oscillation counted over the sequence of DISTINCT tiles occupied. The old
    /// in-harness Reversals counter compared three consecutive per-minute snapshots, but a
    /// tile takes 5-10 minutes to cross, so the middle sample was essentially never a
    /// different tile and it reported 0 for runs that shuttled until they died.
    /// </summary>
    private static int TileReversals(NPCSimulation sim)
    {
        var path = new List<string>();
        foreach (var snap in sim.Snapshots)
            if (path.Count == 0 || path[^1] != snap.LocationName)
                path.Add(snap.LocationName);

        int reversals = 0;
        for (int i = 2; i < path.Count; i++)
            if (path[i] == path[i - 2] && path[i] != path[i - 1])
                reversals++;
        return reversals;
    }

    /// <summary>
    /// The determinism guard that used to be an xunit test. Same seeds, three times over:
    /// any difference means something drew from an unseeded RNG or carried state between
    /// runs, which sets the noise floor every comparison has to clear before it means
    /// anything. Exits non-zero so CI can run it.
    /// </summary>
    private static int Verify(Options o)
    {
        var attempts = new List<List<RunRow>>();
        for (int i = 0; i < 3; i++)
            attempts.Add(Execute(o).OrderBy(r => r.Seed).ToList());

        var failures = new List<string>();
        for (int i = 0; i < attempts[0].Count; i++)
            for (int a = 1; a < attempts.Count; a++)
                if (attempts[0][i] != attempts[a][i])
                    failures.Add($"seed {attempts[0][i].Seed}:\n    run 1: {attempts[0][i]}\n    run {a + 1}: {attempts[a][i]}");

        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"NOT REPRODUCIBLE ({failures.Count} mismatches):");
            foreach (var f in failures) Console.Error.WriteLine("  " + f);
            return 1;
        }

        Console.WriteLine($"reproducible: {attempts[0].Count} seeds identical across 3 runs " +
            $"at parallelism {o.Parallelism}");
        return 0;
    }
}

/// <summary>One run's outcome. A record so the determinism check is a plain equality test.</summary>
public record RunRow(
    int Seed, double Days, int SurvivedMinutes, bool Died, string DeathCause,
    int FireMinutes, int Sticks, int MaxDistance, int UniqueTiles, int TilesMoved,
    int TileReversals, int MinutesBelowWarm25,
    double CacheFuelKg, double CacheWaterL, double FuelGatheredKg, double WaterGatheredL,
    double MaxBodyTempF, int MinutesOver99F);

public sealed class Options
{
    public List<int> Seeds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    public int Days = 7;
    public SimulationScenario Scenario = SimulationScenario.Baseline;
    public int GroupSize;
    public double? Boldness;
    /// <summary>
    /// Defaults to 1. In-process parallelism is NOT currently reproducible - the same seed
    /// gives different outcomes at --parallel > 1, so it must not be used for the before/after
    /// comparisons this bench exists for. Utils.Rng, the event-cooldown table, the stockpile
    /// target and EnvironmentalDetail's id counter are all [ThreadStatic] now, so something
    /// else is still shared; `npcsim verify --parallel N` is the gate that catches it.
    /// To go faster today, shard across PROCESSES instead - that is verified identical to
    /// serial (see README).
    /// </summary>
    public int Parallelism = 1;
    public string? OutPath;
    public bool Trace;

    /// <summary>Overrides NPC.FuelStockpileTargetKg (default 40) for the H13 sweep.</summary>
    public double? FuelTarget;

    /// <summary>starting | fresh | best - what the NPC is wearing. Tests whether better insulation helps.</summary>
    public string Gear = "starting";

    public int Minutes => Days * 24 * 60;

    /// <summary>
    /// Selfishness and Sociability are held at the midpoints of NPCFactory's ranges so that
    /// Boldness is the only thing varied, and any behavioral difference is attributable to it.
    /// </summary>
    public Personality? Personality() =>
        Boldness is double b ? new Personality { Boldness = b, Selfishness = 0.35, Sociability = 0.65 } : null;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException($"{a} needs a value");

            switch (a)
            {
                case "--seeds": o.Seeds = ParseSeeds(Next()); break;
                case "--days": o.Days = int.Parse(Next()); break;
                case "--scenario": o.Scenario = Enum.Parse<SimulationScenario>(Next(), ignoreCase: true); break;
                case "--group": o.GroupSize = int.Parse(Next()); break;
                case "--boldness": o.Boldness = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--parallel": o.Parallelism = int.Parse(Next()); break;
                case "--out": o.OutPath = Next(); break;
                case "--trace": o.Trace = true; break;
                case "--fuel-target": o.FuelTarget = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--gear": o.Gear = Next(); break;
                default: throw new ArgumentException($"Unknown option '{a}'");
            }
        }
        if (o.Parallelism <= 0) o.Parallelism = Environment.ProcessorCount;
        if (o.Parallelism > 1)
            Console.Error.WriteLine(
                $"WARNING: --parallel {o.Parallelism} is not reproducible yet; results will vary " +
                "run to run. Shard across processes instead if you need comparable numbers.");
        return o;
    }

    private static List<int> ParseSeeds(string spec)
    {
        if (spec.Contains('-'))
        {
            var parts = spec.Split('-', 2);
            int lo = int.Parse(parts[0]), hi = int.Parse(parts[1]);
            return Enumerable.Range(lo, hi - lo + 1).ToList();
        }
        return spec.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }
}
