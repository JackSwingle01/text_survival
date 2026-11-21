using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Survival.Tune;

/// What a tuning run is asked to do. Budget is candidate evaluations, not wall time, so a run is
/// a function of its inputs and seed.
public sealed record TuneOptions(int Budget, ulong Seed = 1, int Workers = 0, double Sigma = 0.2, int? Fights = null,
    int? ReportFights = null, int? Lambda = null);

/// One generation's line of the convergence log.
public sealed record Generation(int Index, int Evaluations, double Best, double Median, double BestSoFar, double Sigma, double AxisRatio, double Seconds);

/// A finished run: the parameters at the start and the chosen end, both measured again on seeds
/// the search never saw, and the log.
public sealed record TuneResult(IReadOnlyList<Parameter> Parameters, double[] Before, double[] After, string AfterIs,
    JobResult BeforeReport, JobResult AfterReport, double FitnessBefore, double FitnessAfter, IReadOnlyList<Generation> Log);

/// CMA-ES over the parameters' unit box, scored by the band spec. Each candidate's fights run in
/// a worker process with its overrides; a generation shares its seeds; the next draws new ones.
public static class Tuner
{
    /// Penalty per squared unit outside the box: the candidate is fought at the nearest edge, and
    /// this leads the mean back inside.
    const double OutsidePenalty = 1.0;

    public static IReadOnlyDictionary<string, double> Overrides(IReadOnlyList<Parameter> ps, IReadOnlyList<double> values)
    {
        var o = new SortedDictionary<string, double>(StringComparer.Ordinal);
        for (int i = 0; i < ps.Count; i++) foreach (var path in ps[i].Paths) o[path] = values[i];
        return o;
    }

    static double[] Values(IReadOnlyList<Parameter> ps, double[] unit) => [.. ps.Select((p, i) => p.FromUnit(unit[i]))];

    static double Outside(double[] unit) => unit.Sum(u => u < 0 ? u * u : u > 1 ? (u - 1) * (u - 1) : 0);

    /// The fitness of a measured candidate under a spec.
    public static double Score(BandSpec spec, JobResult r) => spec.Fitness(r.Means);

    public static TuneResult Run(IReadOnlyList<Parameter> ps, BandSpec spec, double[] today, TuneOptions opt, string scratch,
        Action<string>? log = null)
    {
        int workers = opt.Workers > 0 ? opt.Workers : Environment.ProcessorCount;
        int fights = opt.Fights ?? spec.Fights;
        var scenarios = spec.Used.ToDictionary(s => s, s => spec.Scenarios[s]);
        var start = ps.Select((p, i) => p.ToUnit(today[i])).ToArray();
        var es = new Cmaes(start, opt.Sigma, Seeds.Mix(opt.Seed, Seeds.Of("cmaes")), opt.Lambda);
        int threads = Math.Max(1, workers / es.Lambda);
        var history = new List<Generation>();
        double bestSoFar = double.PositiveInfinity;
        double[] bestUnit = start;
        int evals = 0;
        var clock = Stopwatch.StartNew();
        while (evals + es.Lambda <= opt.Budget)
        {
            var xs = es.Ask();
            ulong seedBase = Seeds.Mix(opt.Seed, (ulong)es.Generation + 1);
            var jobs = xs.Select(x => new Job(Overrides(ps, Values(ps, x)), scenarios, fights, seedBase, threads)).ToList();
            var results = Workers.Run(jobs, workers, Path.Combine(scratch, "jobs"));
            var f = xs.Select((x, k) => Score(spec, results[k]) + OutsidePenalty * Outside(x)).ToArray();
            evals += xs.Length;
            int best = Array.IndexOf(f, f.Min());
            if (f[best] < bestSoFar) { bestSoFar = f[best]; bestUnit = [.. xs[best].Select(u => Math.Clamp(u, 0, 1))]; }
            es.Tell(xs, f);
            var sorted = f.OrderBy(v => v).ToArray();
            var g = new Generation(es.Generation, evals, f[best], sorted[sorted.Length / 2], bestSoFar, es.Sigma, es.AxisRatio, clock.Elapsed.TotalSeconds);
            history.Add(g);
            log?.Invoke($"gen {g.Index,3}  evals {g.Evaluations,5}  best {g.Best:F4}  median {g.Median:F4}  best so far {g.BestSoFar:F4}  sigma {g.Sigma:F4}  {g.Seconds:F0}s");
        }

        // Measure the start, the final mean and the best candidate seen again, on seeds the search
        // never used and with more fights: the best seen was picked on noise and flatters itself.
        int reportFights = opt.ReportFights ?? spec.ReportFights;
        ulong reportSeed = Seeds.Mix(opt.Seed, Seeds.Of("report"));
        var mean = es.Mean.Select(u => Math.Clamp(u, 0, 1)).ToArray();
        var candidates = new[] { today, Values(ps, mean), Values(ps, bestUnit) };
        var reports = Workers.Run([.. candidates.Select(v => new Job(Overrides(ps, v), scenarios, reportFights, reportSeed, Math.Max(1, workers / 3)))],
            workers, Path.Combine(scratch, "jobs"));
        var scores = reports.Select(r => Score(spec, r)).ToArray();
        int pick = scores[1] <= scores[2] ? 1 : 2;
        return new TuneResult(ps, today, candidates[pick], pick == 1 ? "final mean" : "best candidate seen",
            reports[0], reports[pick], scores[0], scores[pick], history);
    }

    // ---- what a run writes ----

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string Pct(double v) => (v * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    static string Show(Band b, double v) => b.Metric == "rounds" ? F(v) : Pct(v);
    static string Target(Band b) => b.Metric == "rounds"
        ? $"{(b.Min is double lo ? F(lo) : "")}-{(b.Max is double hi ? F(hi) : "")}"
        : b.Min == null ? $"<= {Pct(b.Max!.Value)}" : b.Max == null ? $">= {Pct(b.Min.Value)}" : $"{Pct(b.Min.Value)}-{Pct(b.Max.Value)}";

    /// The bands, orders and penalties, measured: one row each, before and after when both are given.
    public static string BandTable(BandSpec spec, JobResult before, JobResult? after = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(after == null ? "| Band | Scenario | Target | Value | Distance |" : "| Band | Scenario | Target | Before | After | Distance before | Distance after |");
        sb.AppendLine(after == null ? "|---|---|---|---|---|" : "|---|---|---|---|---|---|---|");
        foreach (var (kind, bands) in new[] { ("", spec.Bands), ("penalty: ", spec.Penalties) })
            foreach (var b in bands)
            {
                double v0 = before.Means[b.Scenario][b.Metric];
                string name = kind + b.Name + (b.Scored ? "" : " (not scored)");
                string d0 = F(b.Distance(v0));
                if (after == null) { sb.AppendLine($"| {name} | {b.Scenario} | {Target(b)} {b.Metric} | {Show(b, v0)} | {d0} |"); continue; }
                double v1 = after.Means[b.Scenario][b.Metric];
                sb.AppendLine($"| {name} | {b.Scenario} | {Target(b)} {b.Metric} | {Show(b, v0)} | {Show(b, v1)} | {d0} | {F(b.Distance(v1))} |");
            }
        foreach (var o in spec.Orders)
        {
            string Values(JobResult r) => string.Join(" > ", o.Scenarios.Select(s => (o.Metric == "rounds" ? F(r.Means[s][o.Metric]) : Pct(r.Means[s][o.Metric]))));
            double D(JobResult r) => o.Distance(o.Scenarios.ToDictionary(s => s, s => r.Means[s][o.Metric]));
            string name = "order: " + o.Name + (o.Scored ? "" : " (not scored)");
            string target = $"{string.Join(" > ", o.Scenarios)} on {o.Metric}";
            if (after == null) sb.AppendLine($"| {name} | | {target} | {Values(before)} | {F(D(before))} |");
            else sb.AppendLine($"| {name} | | {target} | {Values(before)} | {Values(after)} | {F(D(before))} | {F(D(after))} |");
        }
        return sb.ToString();
    }

    /// Every metric of every scenario, before and after, and how the fights ended.
    public static string OutcomeTables(JobResult before, JobResult after)
    {
        var sb = new StringBuilder();
        foreach (var s in before.Means.Keys)
        {
            sb.AppendLine($"### {s}\n");
            sb.AppendLine("| Metric | Before | After |\n|---|---|---|");
            foreach (var m in FightOutcome.Metrics)
                sb.AppendLine($"| {m} | {(m == "rounds" ? F(before.Means[s][m]) : Pct(before.Means[s][m]))} | {(m == "rounds" ? F(after.Means[s][m]) : Pct(after.Means[s][m]))} |");
            string Ends(JobResult r) => string.Join(", ", r.Results[s].Select(e => $"{e.Key} {e.Value}"));
            sb.AppendLine($"| endings | {Ends(before)} | {Ends(after)} |\n");
        }
        return sb.ToString();
    }

    public static void Write(string dir, TuneResult r, BandSpec spec, TuneOptions opt, string command, double wallSeconds)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "best.overrides.json"), Specs.Write(Overrides(r.Parameters, r.After)));
        File.WriteAllText(Path.Combine(dir, "before.overrides.json"), Specs.Write(Overrides(r.Parameters, r.Before)));
        File.WriteAllText(Path.Combine(dir, "outcomes.md"),
            $"# Outcomes, before and after\n\n{spec.ReportFights} fights per scenario on report seeds.\n\n## Bands\n\n"
            + BandTable(spec, r.BeforeReport, r.AfterReport) + "\n## Scenarios\n\n" + OutcomeTables(r.BeforeReport, r.AfterReport));
        var log = new StringBuilder("generation\tevaluations\tbest\tmedian\tbest_so_far\tsigma\taxis_ratio\tseconds\n");
        foreach (var g in r.Log)
            log.AppendLine(string.Join('\t', g.Index, g.Evaluations, F(g.Best), F(g.Median), F(g.BestSoFar), F(g.Sigma), F(g.AxisRatio), F(g.Seconds)));
        File.WriteAllText(Path.Combine(dir, "convergence.tsv"), log.ToString());

        var md = new StringBuilder();
        md.AppendLine("# Combat tuning run\n");
        md.AppendLine($"Command: `{command}`\n");
        md.AppendLine($"Seed {opt.Seed}, budget {opt.Budget} evaluations ({r.Log.Count} generations of {(r.Log.Count > 0 ? r.Log[0].Evaluations : 0)}), "
            + $"{opt.Fights ?? spec.Fights} fights per scenario per candidate, wall time {wallSeconds / 60:F1} min.\n");
        md.AppendLine($"Fitness on {opt.ReportFights ?? spec.ReportFights} report fights per scenario: **{F(r.FitnessBefore)} before, {F(r.FitnessAfter)} after** ({r.AfterIs}).\n");
        var reached = spec.AllBands.Where(b => b.Scored && b.Distance(r.AfterReport.Means[b.Scenario][b.Metric]) == 0).Select(b => b.Name).ToList();
        var missed = spec.AllBands.Where(b => b.Scored && b.Distance(r.AfterReport.Means[b.Scenario][b.Metric]) > 0).Select(b => b.Name).ToList();
        md.AppendLine($"Bands and penalties met after: {reached.Count} of {reached.Count + missed.Count}.");
        if (missed.Count > 0) md.AppendLine($"Missed: {string.Join("; ", missed)}.");
        var unscored = spec.AllBands.Where(b => !b.Scored).Select(b => b.Name).Concat(spec.Orders.Where(o => !o.Scored).Select(o => o.Name)).ToList();
        if (unscored.Count > 0) md.AppendLine($"Not scored (today's rules cannot express them): {string.Join("; ", unscored)}.");
        md.AppendLine("\n## Bands\n");
        md.AppendLine(BandTable(spec, r.BeforeReport, r.AfterReport));
        md.AppendLine("## Parameters\n\n| Parameter | Before | After | Range |\n|---|---|---|---|");
        for (int i = 0; i < r.Parameters.Count; i++)
        {
            var p = r.Parameters[i];
            string edge = p.ToUnit(r.After[i]) is double u && (u < 0.02 || u > 0.98) ? " (at the edge)" : "";
            md.AppendLine($"| {p.Name} | {F(r.Before[i])} | {F(r.After[i])}{edge} | {F(p.Min)}-{F(p.Max)}{(p.Log ? " log" : "")} |");
        }
        md.AppendLine("\n## Applying it\n");
        md.AppendLine("Nothing here changes a content file. `best.overrides.json` is the chosen parameters as content overrides; check it with");
        md.AppendLine($"`dotnet run -c Release --project rebuild/Tools/Tune -- eval --overrides {Path.Combine(dir, "best.overrides.json")}`,");
        md.AppendLine("then copy the values into the tables by hand, in a reviewed change. Files: `outcomes.md` (every metric, before and after), `convergence.tsv`.");
        File.WriteAllText(Path.Combine(dir, "RESULT.md"), md.ToString());
    }
}
