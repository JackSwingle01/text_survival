using System.Diagnostics;

namespace Survival.Tune;

/// One candidate's fights: the content overrides it is made of, the scenarios, how many fights
/// each, and the seed family. Fight i of scenario s runs on seed Seeds.Fight(SeedBase, s, i), so
/// every candidate given the same SeedBase meets the same dice (common random numbers).
public sealed record Job(IReadOnlyDictionary<string, double> Overrides, IReadOnlyDictionary<string, Scenario> Scenarios,
    int Fights, ulong SeedBase, int Threads);

/// Per scenario, the mean of each metric over its fights, how its fights ended, and every fight in
/// order as FightOutcome.Code, space-separated (fight i is the i-th).
public sealed record JobResult(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Means,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Results, IReadOnlyDictionary<string, string>? Fights = null);

public static class Seeds
{
    /// SplitMix-style mixing: any change in any input changes the seed.
    public static ulong Mix(ulong a, ulong b)
    {
        ulong z = a ^ (b + 0x9E3779B97F4A7C15 + (a << 6) + (a >> 2));
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }

    public static ulong Of(string s)
    {
        ulong hash = 14695981039346656037;
        foreach (char c in s) { hash ^= c; hash *= 1099511628211; }
        return hash;
    }

    public static int Fight(ulong seedBase, string scenario, int index) => (int)(Mix(Mix(seedBase, Of(scenario)), (ulong)index) & 0x7FFFFFFF);
}

/// Runs jobs, each in a process of its own: content loads once per process, so a candidate's
/// overrides need a fresh one (ContentOverrides). Inside a process, fights run on threads, one
/// world each.
public static class Workers
{
    /// The worker's side: set the overrides before anything reads Content, then fight.
    public static JobResult RunInProcess(Job job)
    {
        ContentOverrides.Set(job.Overrides);
        return Fight(job);
    }

    /// The fights of a job in this process, whatever content it loaded with.
    public static JobResult Fight(Job job)
    {
        var fights = new Fights();
        var means = new Dictionary<string, IReadOnlyDictionary<string, double>>();
        var results = new Dictionary<string, IReadOnlyDictionary<string, int>>();
        var each = new Dictionary<string, string>();
        foreach (var (name, s) in job.Scenarios)
        {
            var outs = new FightOutcome[job.Fights];
            Parallel.For(0, job.Fights, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, job.Threads) },
                i => outs[i] = fights.Run(s, Seeds.Fight(job.SeedBase, name, i)));
            means[name] = FightOutcome.Metrics.ToDictionary(m => m, m => outs.Average(o => o.Metric(m)));
            results[name] = outs.GroupBy(o => o.Result).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
            each[name] = string.Join(' ', outs.Select(o => o.Code()));
        }
        return new JobResult(means, results, each);
    }

    /// Runs every job in its own worker process, at most `parallel` at once, and returns their
    /// results in job order. A worker that fails fails the call, with its error output.
    public static JobResult[] Run(IReadOnlyList<Job> jobs, int parallel, string scratchDir)
    {
        var results = new JobResult[jobs.Count];
        try
        {
            Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) },
                k => results[k] = InProcess<Job, JobResult>("worker", jobs[k], scratchDir, $"{k}"));
        }
        // One failed worker is the error, not a wrapper around it.
        catch (AggregateException e) when (e.InnerExceptions.Count >= 1) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerExceptions[0]).Throw(); }
        return results;
    }

    /// One fight fought again in a process of its own, so its overrides load fresh.
    public static ReplayResult Replay(ReplayJob job, string scratchDir) =>
        InProcess<ReplayJob, ReplayResult>("replay", job, scratchDir, Guid.NewGuid().ToString("N"));

    /// Runs `Tune <command> JOB OUT` and reads OUT back. A worker that fails fails the call, with its error output.
    static TOut InProcess<TIn, TOut>(string command, TIn job, string scratchDir, string tag)
    {
        Directory.CreateDirectory(scratchDir);
        // The tool itself, whoever called: its apphost beside Tune.dll, else the dll under dotnet.
        string dll = typeof(Workers).Assembly.Location, host = Path.ChangeExtension(dll, OperatingSystem.IsWindows() ? ".exe" : null);
        string jobPath = Path.Combine(scratchDir, $"job{tag}.json"), outPath = Path.Combine(scratchDir, $"out{tag}.json");
        File.WriteAllText(jobPath, Specs.Write(job));
        var psi = new ProcessStartInfo(File.Exists(host) ? host : "dotnet") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        if (!File.Exists(host)) psi.ArgumentList.Add(dll);
        foreach (var a in new[] { command, jobPath, outPath }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start a worker");
        var errTask = p.StandardError.ReadToEndAsync();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        string err = errTask.Result;
        if (p.ExitCode != 0) throw new InvalidOperationException($"Worker {tag} failed ({p.ExitCode}):\n{err}");
        var result = Specs.Read<TOut>(File.ReadAllText(outPath));
        File.Delete(jobPath);
        File.Delete(outPath);
        return result;
    }
}
