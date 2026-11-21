using System.Globalization;
using Survival;
using Survival.Tune;

// The combat tuner. Commands:
//   combat --budget N [--seed S] [--workers W] [--fights F] [--report-fights F] [--sigma X] [--lambda L]
//          [--params FILE] [--bands FILE] [--out DIR]
//       CMA-ES over the parameter spec, scored by the band spec; writes runs/<timestamp>/.
//   eval [--overrides FILE] [--fights F] [--seed S] [--workers W] [--bands FILE]
//       Measures the band spec once, with today's content or with an override file.
//   worker JOB OUT
//       One candidate's fights (internal: the tuner starts these).
// Nothing here writes a content file: results are override files, applied by hand after review.

// The worker must set its overrides before anything touches Content, so it goes first.
if (args.Length == 3 && args[0] == "worker")
{
    File.WriteAllText(args[2], Specs.Write(Workers.RunInProcess(Specs.Read<Job>(File.ReadAllText(args[1])))));
    return 0;
}

if (args.Length == 0 || args[0] is not ("combat" or "eval"))
{
    Console.Error.WriteLine("usage: Tune combat --budget N [--seed S] [--workers W] [--fights F] [--params FILE] [--bands FILE] [--out DIR]\n"
        + "       Tune eval [--overrides FILE] [--fights F] [--seed S] [--workers W] [--bands FILE]");
    return 2;
}

var opts = new Dictionary<string, string>();
for (int i = 1; i < args.Length; i++)
{
    if (!args[i].StartsWith("--") || i + 1 >= args.Length) throw new ArgumentException($"Expected --name value, got '{args[i]}'");
    opts[args[i][2..]] = args[++i];
}
string? Opt(string name) => opts.Remove(name, out var v) ? v : null;
int? IntOpt(string name) => Opt(name) is string s ? int.Parse(s, CultureInfo.InvariantCulture) : null;

string project = ProjectDir();
string bandsPath = Opt("bands") ?? Path.Combine(project, "specs", "combat-bands.json");
var spec = Specs.LoadBands(File.ReadAllText(bandsPath));
ulong seed = ulong.Parse(Opt("seed") ?? "1", CultureInfo.InvariantCulture);
int workers = IntOpt("workers") ?? Environment.ProcessorCount;
var scenarios = spec.Used.ToDictionary(s => s, s => spec.Scenarios[s]);
var scratch = Path.Combine(Path.GetTempPath(), $"tune-{Environment.ProcessId}");

if (args[0] == "eval")
{
    var overrides = Opt("overrides") is string file ? Specs.Read<Dictionary<string, double>>(File.ReadAllText(file)) : [];
    int fightsEach = IntOpt("fights") ?? spec.ReportFights;
    Unknown();
    var clock = System.Diagnostics.Stopwatch.StartNew();
    // One process per slice of scenarios, so every core fights.
    var names = scenarios.Keys.ToList();
    int slices = Math.Min(workers, names.Count);
    var jobs = Enumerable.Range(0, slices).Select(k => new Job(overrides,
        names.Where((_, i) => i % slices == k).ToDictionary(n => n, n => scenarios[n]), fightsEach, Seeds.Mix(seed, Seeds.Of("report")),
        Math.Max(1, workers / slices))).ToList();
    var parts = Workers.Run(jobs, workers, scratch);
    var r = new JobResult(names.ToDictionary(n => n, n => parts.First(p => p.Means.ContainsKey(n)).Means[n]),
        names.ToDictionary(n => n, n => parts.First(p => p.Results.ContainsKey(n)).Results[n]));
    Console.WriteLine(Tuner.BandTable(spec, r));
    Console.WriteLine($"fitness {Tuner.Score(spec, r):F4}   ({fightsEach} fights per scenario, {clock.Elapsed.TotalSeconds:F0}s)");
    Directory.Delete(scratch, recursive: true);
    return 0;
}

int budget = IntOpt("budget") ?? throw new ArgumentException("combat needs --budget N (candidate evaluations)");
string paramsPath = Opt("params") ?? Path.Combine(project, "specs", "combat.json");
var options = new TuneOptions(budget, seed, workers, double.Parse(Opt("sigma") ?? "0.2", CultureInfo.InvariantCulture),
    IntOpt("fights"), IntOpt("report-fights"), IntOpt("lambda"));
string outDir = Opt("out") ?? Path.Combine(project, "runs", DateTime.Now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture));
Unknown();

// The orchestrator reads today's tables as embedded, untouched: candidates live in the workers.
var files = Specs.EmbeddedContent();
var ps = Specs.LoadParameters(File.ReadAllText(paramsPath), files);
var today = ps.Select(p => Specs.Current(files, p.Paths[0])).ToArray();
Console.WriteLine($"{ps.Count} parameters, {scenarios.Count} scenarios, budget {budget}, seed {seed}, {workers} workers -> {outDir}");
var wall = System.Diagnostics.Stopwatch.StartNew();
var result = Tuner.Run(ps, spec, today, options, scratch, Console.WriteLine);
Directory.CreateDirectory(outDir);
File.Copy(paramsPath, Path.Combine(outDir, "params.json"), overwrite: true);
File.Copy(bandsPath, Path.Combine(outDir, "bands.json"), overwrite: true);
Tuner.Write(outDir, result, spec, options, "dotnet run -c Release --project rebuild/Tools/Tune -- " + string.Join(' ', args), wall.Elapsed.TotalSeconds);
Console.WriteLine($"fitness {result.FitnessBefore:F4} -> {result.FitnessAfter:F4}; wrote {outDir}");
Directory.Delete(scratch, recursive: true);
return 0;

void Unknown()
{
    if (opts.Count > 0) throw new ArgumentException($"Unknown option(s): {string.Join(", ", opts.Keys.Select(k => "--" + k))}");
}

// rebuild/Tools/Tune, found from wherever the build put this executable.
static string ProjectDir()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Tune.csproj"))) return dir.FullName;
    throw new DirectoryNotFoundException("Cannot find rebuild/Tools/Tune above " + AppContext.BaseDirectory);
}
