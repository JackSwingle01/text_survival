using text_survival.Actors;
using text_survival.Tests.Support;
using Xunit.Abstractions;

namespace text_survival.Tests.Actors;

/// <summary>
/// Observation and benchmarking tests for the starting NPC's autonomous behavior, built on
/// <see cref="NPCSimulation"/>. Gated behind NPC_SIM=1 - see
/// documentation/npc-simulation-plan.md for what these are for and how to read the logs.
/// </summary>
[Collection("NPCSimulation")]
public class NPCSimulationTests
{
    private const int SeventyTwoHoursMinutes = 72 * 60;

    private readonly ITestOutputHelper _output;

    public NPCSimulationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SimulationFact]
    public void Baseline_72h_WritesLog()
    {
        var sim = NPCSimulation.Create(SimulationScenario.Baseline);
        sim.Run(SeventyTwoHoursMinutes, captureTileView: true);
        var path = sim.WriteLog("baseline-single");
        _output.WriteLine($"Log: {path}");
        _output.WriteLine(sim.Summarize().ToString());
    }

    [SimulationFact]
    public void FireLit_72h_WritesLog()
    {
        var sim = NPCSimulation.Create(SimulationScenario.FireLit);
        sim.Run(SeventyTwoHoursMinutes, captureTileView: true);
        var path = sim.WriteLog("firelit-single");
        _output.WriteLine($"Log: {path}");
        _output.WriteLine(sim.Summarize().ToString());
    }

    [SimulationFact]
    public void NpcAtCamp_72h_WritesLog()
    {
        var sim = NPCSimulation.Create(SimulationScenario.NpcAtCamp);
        sim.Run(SeventyTwoHoursMinutes, captureTileView: true);
        var path = sim.WriteLog("npcatcamp-single");
        _output.WriteLine($"Log: {path}");
        _output.WriteLine(sim.Summarize().ToString());
    }

    [SimulationFact]
    public void Batch_Baseline_10runs()
    {
        RunBatch(SimulationScenario.Baseline, "baseline-batch", 10);
    }

    private const int OneWeekMinutes = 7 * 24 * 60;

    private static readonly int[] ComparisonSeeds = [1, 2, 3];

    /// <summary>
    /// One deterministic run per seed (not a batch - a fixed seed makes repeated sampling
    /// pointless) for before/after comparisons of an AI change. Run this, make the change,
    /// run it again, and diff the two tables - same world layout and RNG sequence both
    /// times, so any difference in outcome is the code change, not luck.
    /// </summary>
    [SimulationFact]
    public void SeededComparison_ThreeSeeds()
    {
        var summaries = new List<(int Seed, SimulationSummary Summary)>();

        foreach (var seed in ComparisonSeeds)
        {
            var sim = NPCSimulation.Create(SimulationScenario.Baseline, seed);
            sim.Run(SeventyTwoHoursMinutes, captureTileView: true);
            var path = sim.WriteLog($"seeded-{seed}");
            var summary = sim.Summarize();
            summaries.Add((seed, summary));
            _output.WriteLine($"Seed {seed}: {path}");
        }

        _output.WriteLine("");
        _output.WriteLine("Seed | Survived(min) | Died | MinWarmPct | FireActiveMin | Sticks | ColdIdle | Reversals");
        foreach (var (seed, s) in summaries)
        {
            _output.WriteLine(
                $"{seed,4} | {s.SurvivedMinutes,14} | {(s.Died ? s.DeathCause : "alive"),-16} | {s.MinWarmPct,10:P0} | " +
                $"{s.CampFireActiveMinutes,13} | {s.SticksGathered,6} | {s.ColdIdleMinutes,8} | {s.Reversals,9}");
        }
    }

    /// <summary>
    /// Runs N simulations of the given scenario, writes one log per run plus a combined
    /// metrics table, and prints the table to test output. This is the benchmark used to
    /// evaluate each AI hypothesis in documentation/npc-simulation-plan.md - run it before
    /// and after a change and compare the mean/median rows.
    /// </summary>
    private void RunBatch(SimulationScenario scenario, string name, int runCount)
    {
        var summaries = new List<SimulationSummary>();

        for (int i = 0; i < runCount; i++)
        {
            var sim = NPCSimulation.Create(scenario);
            sim.Run(SeventyTwoHoursMinutes, captureTileView: i == 0);
            var path = sim.WriteLog($"{name}-{i:D2}");
            var summary = sim.Summarize();
            summaries.Add(summary);
            _output.WriteLine($"Run {i}: {path}");
        }

        _output.WriteLine("");
        _output.WriteLine(FormatTable(summaries));
    }

    private static string FormatTable(List<SimulationSummary> summaries)
    {
        double Mean(Func<SimulationSummary, double> f) => summaries.Average(f);
        double Median(Func<SimulationSummary, double> f)
        {
            var values = summaries.Select(f).OrderBy(v => v).ToList();
            int mid = values.Count / 2;
            return values.Count % 2 == 0 ? (values[mid - 1] + values[mid]) / 2.0 : values[mid];
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Runs: {summaries.Count}");
        sb.AppendLine($"Died: {summaries.Count(s => s.Died)}/{summaries.Count}");
        sb.AppendLine($"SurvivedMinutes:      mean={Mean(s => s.SurvivedMinutes):F0}  median={Median(s => s.SurvivedMinutes):F0}");
        sb.AppendLine($"MinWarmPct:           mean={Mean(s => s.MinWarmPct):F2}  median={Median(s => s.MinWarmPct):F2}");
        sb.AppendLine($"MinutesBelowWarm25:   mean={Mean(s => s.MinutesBelowWarm25):F0}  median={Median(s => s.MinutesBelowWarm25):F0}");
        sb.AppendLine($"CampFireActiveMinutes:mean={Mean(s => s.CampFireActiveMinutes):F0}  median={Median(s => s.CampFireActiveMinutes):F0}");
        sb.AppendLine($"FireStartSuccesses:   mean={Mean(s => s.FireStartSuccesses):F1}  median={Median(s => s.FireStartSuccesses):F1}");
        sb.AppendLine($"TendFireCount:        mean={Mean(s => s.TendFireCount):F1}  median={Median(s => s.TendFireCount):F1}");
        sb.AppendLine($"ForageMinutes:        mean={Mean(s => s.ForageMinutes):F0}  median={Median(s => s.ForageMinutes):F0}");
        sb.AppendLine($"SticksGathered:       mean={Mean(s => s.SticksGathered):F1}  median={Median(s => s.SticksGathered):F1}");
        sb.AppendLine($"ColdIdleMinutes:      mean={Mean(s => s.ColdIdleMinutes):F0}  median={Median(s => s.ColdIdleMinutes):F0}");
        sb.AppendLine($"Reversals:            mean={Mean(s => s.Reversals):F0}  median={Median(s => s.Reversals):F0}");
        sb.AppendLine($"StuckStreakMax:       mean={Mean(s => s.StuckStreakMax):F0}  median={Median(s => s.StuckStreakMax):F0}");
        sb.AppendLine($"ActionStarts:         mean={Mean(s => s.ActionStarts):F0}  median={Median(s => s.ActionStarts):F0}");
        return sb.ToString();
    }

    // Fixed at the midpoints of NPCFactory's random ranges (Selfishness 0.2-0.5,
    // Sociability 0.5-0.8) so Boldness is the only thing varied across profiles - any
    // difference in behavior between profiles is attributable to Boldness alone.
    private static readonly (string Name, Personality Personality)[] PersonalityProfiles =
    [
        ("Timid", new Personality { Boldness = 0.15, Selfishness = 0.35, Sociability = 0.65 }),
        ("Baseline", new Personality { Boldness = 0.50, Selfishness = 0.35, Sociability = 0.65 }),
        ("Bold", new Personality { Boldness = 0.85, Selfishness = 0.35, Sociability = 0.65 }),
    ];

    private static readonly int[] MatrixSeeds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    /// <summary>
    /// Runs each personality profile across a fixed set of seeds for a full simulated
    /// week, and reports both survival and the behavioral fingerprint metrics (distance
    /// ranged, resources gathered, combat, crafting) per profile. This is the harness's
    /// answer to "does personality actually produce different behavior, and do outcomes
    /// vary by seed instead of the same thing killing every NPC" - read the per-seed death
    /// causes as well as the averages, since a good AI should show a spread of causes, not
    /// one cause dominating every run.
    /// </summary>
    [SimulationFact]
    public void PersonalityMatrix_OneWeek()
    {
        var byProfile = new Dictionary<string, List<SimulationSummary>>();

        foreach (var (profileName, personality) in PersonalityProfiles)
        {
            var summaries = new List<SimulationSummary>();
            foreach (var seed in MatrixSeeds)
            {
                var sim = NPCSimulation.Create(SimulationScenario.Baseline, seed, personality);
                sim.Run(OneWeekMinutes, captureTileView: seed == MatrixSeeds[0]);
                var path = sim.WriteLog($"matrix-{profileName.ToLowerInvariant()}-seed{seed}");
                var summary = sim.Summarize();
                summaries.Add(summary);
                _output.WriteLine($"{profileName} seed {seed}: {path}  " +
                    $"survived={summary.SurvivedMinutes / 1440.0:F1}d died={(summary.Died ? summary.DeathCause : "no")} " +
                    $"maxDist={summary.MaxDistanceFromCamp} tilesVisited={summary.UniqueTilesVisited}");
            }
            byProfile[profileName] = summaries;
        }

        _output.WriteLine("");
        _output.WriteLine(FormatProfileTable(byProfile));
    }

    private static string FormatProfileTable(Dictionary<string, List<SimulationSummary>> byProfile)
    {
        double Mean(List<SimulationSummary> l, Func<SimulationSummary, double> f) => l.Average(f);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{"Profile",-9} | {"Survived(d)",11} | {"Died%",6} | {"MaxDist",7} | {"TilesVisited",12} | {"TilesMoved",10} | {"Sticks",6} | {"WaterL",6} | {"Crafted",7} | {"Combat",6} | {"Flee",4}");
        foreach (var (profile, summaries) in byProfile)
        {
            double diedPct = 100.0 * summaries.Count(s => s.Died) / summaries.Count;
            sb.AppendLine(
                $"{profile,-9} | {Mean(summaries, s => s.SurvivedMinutes / 1440.0),11:F1} | {diedPct,5:F0}% | " +
                $"{Mean(summaries, s => s.MaxDistanceFromCamp),7:F1} | {Mean(summaries, s => s.UniqueTilesVisited),12:F1} | " +
                $"{Mean(summaries, s => s.TotalTilesMoved),10:F1} | {Mean(summaries, s => s.SticksGathered),6:F1} | " +
                $"{Mean(summaries, s => s.WaterGatheredL),6:F1} | {Mean(summaries, s => s.ItemsCrafted),7:F1} | " +
                $"{Mean(summaries, s => s.CombatEngagements),6:F1} | {Mean(summaries, s => s.FleeCount),4:F1}");
        }

        sb.AppendLine();
        sb.AppendLine("Death causes by profile (should vary by seed for a well-balanced AI, not collapse to one cause):");
        foreach (var (profile, summaries) in byProfile)
        {
            var causes = summaries
                .Select(s => s.Died ? (s.DeathCause ?? "unknown") : "survived")
                .GroupBy(c => c)
                .Select(g => $"{g.Key}={g.Count()}");
            sb.AppendLine($"  {profile,-9}: {string.Join(", ", causes)}");
        }

        return sb.ToString();
    }

    private static readonly int[] GroupSizes = [1, 2, 3, 4];
    private static readonly int[] GroupSeeds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    /// <summary>
    /// Compares solo survival (group size 1, using <see cref="NPCGroupSimulation"/> rather
    /// than <see cref="NPCSimulation"/> so the comparison uses the exact same code path at
    /// every size) against groups of 2-4 sharing one camp's fire and cache, across the same
    /// 10 seeds and a full simulated week. Tests the hypothesis that a solo survivor
    /// struggles to cover fire-tending, foraging, and stockpiling alone in a way a group,
    /// splitting that load across more hands (even with no explicit cooperation logic -
    /// this only tests what falls out of them sharing one cache and fire), would not.
    /// </summary>
    [SimulationFact]
    public void GroupSize_OneWeek()
    {
        var bySize = new Dictionary<int, List<GroupSummary>>();

        foreach (var size in GroupSizes)
        {
            var summaries = new List<GroupSummary>();
            foreach (var seed in GroupSeeds)
            {
                var sim = NPCGroupSimulation.Create(size, seed);
                sim.Run(OneWeekMinutes);
                var summary = sim.Summarize();
                summaries.Add(summary);
            }
            bySize[size] = summaries;
            _output.WriteLine($"Group size {size}: {summaries.Count} seeds run");
        }

        _output.WriteLine("");
        _output.WriteLine(FormatGroupSizeTable(bySize));
    }

    private static string FormatGroupSizeTable(Dictionary<int, List<GroupSummary>> bySize)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{"GroupSize",9} | {"AvgSurvived(d)",14} | {"MinSurvived(d)",14} | {"MaxSurvived(d)",14} | {"AnySurvivedWk%",14} | {"AllSurvivedWk%",14} | {"CacheFuel",9} | {"CacheWater",10}");
        foreach (var (size, summaries) in bySize)
        {
            double avgDays = summaries.Average(g => g.AverageSurvivedMinutes) / 1440.0;
            double minDays = summaries.Average(g => g.MinSurvivedMinutes) / 1440.0;
            double maxDays = summaries.Average(g => g.MaxSurvivedMinutes) / 1440.0;
            double anySurvivedPct = 100.0 * summaries.Count(g => g.MembersAliveAtEnd > 0) / summaries.Count;
            double allSurvivedPct = 100.0 * summaries.Count(g => g.MembersAliveAtEnd == g.Members.Count) / summaries.Count;
            double avgFuel = summaries.Average(g => g.CacheFuelKgFinal);
            double avgWater = summaries.Average(g => g.CacheWaterLFinal);
            sb.AppendLine($"{size,9} | {avgDays,14:F2} | {minDays,14:F2} | {maxDays,14:F2} | {anySurvivedPct,13:F0}% | {allSurvivedPct,13:F0}% | {avgFuel,9:F1} | {avgWater,10:F1}");
        }
        return sb.ToString();
    }
}

[CollectionDefinition("NPCSimulation", DisableParallelization = true)]
public class NPCSimulationCollection { }
