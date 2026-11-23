using Survival;
using Xunit.Abstractions;

/// The stored-plan benchmark (spec "The planner"): a populated valley lived with animals keeping their plans until an
/// interrupt, and again planning afresh at every decision. It reports time, allocation and moves for each, and never
/// gates. PLAN_BENCH_SEEDS (comma list), PLAN_BENCH_DAYS and PLAN_BENCH_REPLAN (0 or 1, else both) pick the runs, so
/// they can be split across processes. It runs only with PLAN_BENCH set:
/// PLAN_BENCH=1 dotnet test rebuild/Sim.Tests -c Release --filter PlanBenchReport
public class PlanBenchReport(ITestOutputHelper output)
{
    [Trait("Category", TestCategories.Slow)]
    [ReportFact("PLAN_BENCH")]
    public void Bench()
    {
        var seeds = (Environment.GetEnvironmentVariable("PLAN_BENCH_SEEDS") ?? "17,23").Split(',').Select(int.Parse);
        int days = int.TryParse(Environment.GetEnvironmentVariable("PLAN_BENCH_DAYS"), out int d) ? d : 30;
        bool[] arms = Environment.GetEnvironmentVariable("PLAN_BENCH_REPLAN") switch { "0" => [false], "1" => [true], _ => [false, true] };
        foreach (int seed in seeds)
            foreach (bool replan in arms)
            {
                var w = World.New(seed);
                w.ReplanEveryDecision = replan;
                long before = GC.GetAllocatedBytesForCurrentThread();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var events = Sim.Advance(w, days * 1440);
                long mb = (GC.GetAllocatedBytesForCurrentThread() - before) >> 20;
                int moves = events.OfType<Moved>().Count(m => !w.Agents.TryGetValue(m.Who, out var a) || a.Mind is HerdMind);
                output.WriteLine($"seed {seed}, {days} days, {(replan ? "planning every decision" : "stored plans")}: " +
                    $"{clock.Elapsed.TotalSeconds:F1} s, {mb} MB, {moves} animal moves, {w.Agents.Values.Count(a => a.Mind is HerdMind)} animals left");
            }
    }
}
