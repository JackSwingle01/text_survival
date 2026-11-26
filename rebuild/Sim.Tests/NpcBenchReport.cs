using Survival;
using Xunit.Abstractions;

/// The NPC bench: a lone NPC at the start camp of each seed, lived on its own mind. It reports and never gates:
/// how long each lived, what killed it, and the decisions worth a look. Fixed seeds plus fresh random ones each
/// run, so nothing is fitted to particular seeds. NPC_BENCH_SEEDS (comma list) and NPC_BENCH_DAYS override the
/// run; NPC_BENCH_OUT appends each seed's line to a file as that seed finishes (finish order, so sort it), so
/// seeds can be split across processes and a seed that never ends hides no other. It runs only with
/// NPC_BENCH set: NPC_BENCH=1 dotnet test rebuild/Sim.Tests -c Release --filter NpcBenchReport
/// The nightly run keeps main's on the `baselines` branch (.github/workflows/nightly.yml).
public class NpcBenchReport(ITestOutputHelper output)
{
    static readonly int[] FixedSeeds = [1, 3, 5, 7, 11, 17, 42, 777, 2024];
    const int RandomSeeds = 6, DefaultDays = 30;

    [Trait("Category", TestCategories.Slow)]
    [ReportFact("NPC_BENCH")]
    public void Bench()
    {
        var env = Environment.GetEnvironmentVariable("NPC_BENCH_SEEDS");
        int[] seeds = env is { Length: > 0 }
            ? [.. env.Split(',').Select(int.Parse)]
            : [.. FixedSeeds, .. Enumerable.Range(0, RandomSeeds).Select(_ => Random.Shared.Next(100_000, 1_000_000))];
        int days = int.TryParse(Environment.GetEnvironmentVariable("NPC_BENCH_DAYS"), out int d) ? d : DefaultDays;
        var outFile = Environment.GetEnvironmentVariable("NPC_BENCH_OUT");
        var writing = new object();
        var lines = seeds.AsParallel().AsOrdered().Select(seed =>
        {
            string line = Run(seed, days);
            if (outFile is { Length: > 0 }) lock (writing) File.AppendAllText(outFile, line + Environment.NewLine);
            return line;
        }).ToList();
        foreach (var line in lines) output.WriteLine(line);
    }

    static string Run(int seed, int days)
    {
        var w = World.New(seed);
        var npc = NpcBench.Spawn(w, Map.Of(w).Camp);
        var agent = w.Agent(npc);
        int fires = 0, felled = 0, refused = 0, stuck = 0, dropped = 0, injuries = 0, coldNights = 0, hunts = 0, campMoves = 0;
        double kcal = 0, forageMinutes = 0, lowestCoreF = agent.Body.CoreTempF;
        var reversals = new Reversals(npc);
        var camp = Memories.CampOf(w, agent);
        long until = days * 1440L;
        var trace = Environment.GetEnvironmentVariable("NPC_BENCH_TRACE") is { Length: > 0 } dir ? new StreamWriter(Path.Combine(dir, $"trace-{seed}.txt")) : null;
        Activity? last = null;
        while (w.Minute < until && Sim.IsAlive(agent))
        {
            if (trace != null && Sim.ActivityOf(agent) is { } now && !ReferenceEquals(now, last))
            {
                last = now;
                var mind = (NpcMind)agent.Mind;
                var b = agent.Body;
                int clock = Weather.MinuteOfDay(w.Minute);
                trace.WriteLine($"{w.Minute / 1440.0,6:0.00} {clock / 60:00}:{clock % 60:00} at {agent.Tile.X - mind.Camp.X:+0;-0;0},{agent.Tile.Y - mind.Camp.Y:+0;-0;0} " +
                    $"camp {mind.Camp} plan {mind.Plan?.Stat}/{mind.Plan?.Priority} core {b.CoreTempF:0.0} water {b.HydrationPct:0.00} energy {b.EnergyPct:0.00} kcal {b.CaloriesKcal:0} | {now}");
            }
            if (Sim.ActivityOf(agent) is Gather { Focus: ForageFocus.Food }) forageMinutes++;
            foreach (var e in Sim.Advance(w, 1))
            {
                reversals.Read(e);
                if (trace != null && e is not (Moved or EffectStarted or EffectEnded) && e.ToString()!.Contains(npc.ToString()))
                    trace.WriteLine($"{w.Minute / 1440.0,6:0.00}        {e}");
                switch (e)
                {
                    case FireLit f when f.By == npc: fires++; break;
                    case TreeFelled t when t.Who == npc: felled++; break;
                    case PlanRefused r when r.Who == npc: refused++; break;
                    case NeedStuck s when s.Who == npc: stuck++; break;
                    case NeedDropped x when x.Who == npc: dropped++; break;
                    case Injured i when i.Who == npc: injuries++; break;
                    case Ate a when a.Who == npc: kcal += a.Kcal; break;
                    case EncounterEnded x when x.Agent == npc: hunts++; break;
                }
            }
            lowestCoreF = Math.Min(lowestCoreF, agent.Body.CoreTempF);
            if (Weather.MinuteOfDay(w.Minute) == 2 * 60)
            {
                Fire.CatchUpOn(w, agent.Tile, []);
                if (!Fire.FlamesOn(w, agent.Tile).Any()) coldNights++;
            }
            var nowCamp = Memories.CampOf(w, agent);
            if (nowCamp != camp) { campMoves++; camp = nowCamp; }
        }
        trace?.Dispose();
        bool alive = Sim.IsAlive(agent);
        double lived = w.Minute / 1440.0;
        return $"seed {seed,7}: {(alive ? "alive" : "dead "),-5} day {lived,5:0.0}{(alive ? "" : $" ({Body.CauseOfDeath(agent.Body).What})"),-18} " +
               $"fat {agent.Body.FatKg,4:0.0} kg, lowest core {lowestCoreF:0.0}F, {kcal / Math.Max(1, lived),5:0} kcal/day, forage {forageMinutes / 60 / Math.Max(1, lived):0.0} h/day, " +
               $"fires {fires}, felled {felled}, fights {hunts}, injuries {injuries}, nights without a flame {coldNights}, " +
               $"camp moves {campMoves}, reversals {reversals.Count}, refused {refused}, stuck {stuck}, dropped {dropped}";
    }
}
