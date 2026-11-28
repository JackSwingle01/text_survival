using Xunit.Abstractions;

namespace Survival.Tests;

/// The food chain bench: who kills and eats what in the populated valley, per day and week, over many seeds. It
/// reports and never gates. FOOD_BENCH_SEEDS (comma list) and FOOD_BENCH_DAYS override the run (default 30 days);
/// FOOD_BENCH_OUT writes the report to a file. It runs only with FOOD_BENCH set:
/// FOOD_BENCH=1 dotnet test rebuild/Sim.Tests -c Release --filter FoodBenchRun
/// The nightly run keeps main's on the `baselines` branch (.github/workflows/nightly.yml).
public class FoodBenchRun(ITestOutputHelper output)
{
    static readonly int[] DefaultSeeds = [1, 3, 5, 7, 11, 17, 42, 101, 202, 303, 404, 505];
    const int DefaultDays = 30;

    [Trait("Category", TestCategories.Slow)]
    [ReportFact("FOOD_BENCH")]
    public void Bench()
    {
        var env = Environment.GetEnvironmentVariable("FOOD_BENCH_SEEDS");
        int[] seeds = env is { Length: > 0 } ? [.. env.Split(',').Select(int.Parse)] : DefaultSeeds;
        int days = int.TryParse(Environment.GetEnvironmentVariable("FOOD_BENCH_DAYS"), out int d) ? d : DefaultDays;
        var results = seeds.AsParallel().AsOrdered().Select(seed => FoodBench.Run(TestWorlds.New(seed), seed, days)).ToList();
        string text = FoodBenchReport.Format(results, days);
        if (Environment.GetEnvironmentVariable("FOOD_BENCH_OUT") is { Length: > 0 } file) File.WriteAllText(file, text);
        output.WriteLine(text);
    }
}
