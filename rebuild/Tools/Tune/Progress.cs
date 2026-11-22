using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Survival.Tune;

// The run log, progress.jsonl: one JSON object per line, appended as the run goes, so a viewer can
// watch it live. The first line is the header; each later line is one generation. The shape is a
// search's, not CMA-ES's: populations of candidates, each a parameter vector with a fitness, fought
// in named scenarios and measured on named metrics. A later training run (one population per
// species policy) writes the same shape. Seeds are written as strings: they are 64-bit and
// JavaScript's numbers are not.

/// One searched number of a population: as the parameter spec gives it, plus where the search began.
public sealed record ParamInfo(string Name, IReadOnlyList<string> Paths, double Min, double Max, bool Log, bool Integer, double Start);

public sealed record PopulationInfo(string Id, string Search, int Size, IReadOnlyList<ParamInfo> Parameters);

public sealed record BandInfo(string Name, string Kind, string Metric, double? Min, double? Max, double Scale, double Weight, bool Scored);

public sealed record ScenarioInfo(string Setup, string Weapon, string Cond, string Policy, bool Watchable, IReadOnlyList<BandInfo> Bands);

public sealed record OrderInfo(string Name, string Metric, IReadOnlyList<string> Scenarios, bool Scored);

/// How every fight's seed is derived, so any fight of the run can be fought again.
public sealed record SeedRule(string RunSeed, string GenerationSeed, string FightSeed, int MapSeed, int FightsPerScenario);

public sealed record RunHeader(string Type, int Schema, string Started, string Command, Provenance Provenance, SeedRule Seeds,
    IReadOnlyList<PopulationInfo> Populations, IReadOnlyDictionary<string, ScenarioInfo> Scenarios, IReadOnlyList<OrderInfo> Orders,
    IReadOnlyList<string> Metrics, IReadOnlyDictionary<string, string> Endings, JsonObject Specs);

public sealed record Spread(double Best, double Median, double Worst, double BestSoFar);

/// A point of the search: its place in the unit box, the values it is fought with, and the fitness
/// the search saw (score plus OutsidePenalty for leaving the box). The mean is never fought during the
/// search, so it has no fitness.
public sealed record CandidateRecord(double? Fitness, IReadOnlyList<double> Unit, IReadOnlyList<double> Values);

public sealed record BandResult(string Name, double Value, double Distance);

/// One scenario as the best candidate fought it: each metric's mean, the endings counted, every
/// fight in order as FightOutcome.Code (space-separated; index i is fight i, on seed
/// Seeds.Fight(generationSeed, scenario, i)), and each band's value and distance.
public sealed record ScenarioResult(IReadOnlyDictionary<string, double> Metrics, IReadOnlyDictionary<string, int> Results, string Fights,
    IReadOnlyList<BandResult> Bands);

public sealed record BestRecord(int Candidate, double Fitness, IReadOnlyDictionary<string, ScenarioResult> Scenarios, IReadOnlyList<BandResult> Orders);

public sealed record PopulationRecord(string Id, Spread Fitness, double Sigma, double AxisRatio, CandidateRecord Mean,
    IReadOnlyList<CandidateRecord> Candidates, BestRecord Best);

public sealed record GenerationRecord(string Type, int Generation, int Evaluations, double Seconds, string GenerationSeed,
    IReadOnlyList<PopulationRecord> Populations);

/// Appends the log line by line. Each line is written whole and flushed, so a reader never sees half a record.
public sealed class ProgressLog(string path)
{
    public const string FileName = "progress.jsonl";
    public const int Schema = 1;
    public const string Population = "combat";

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public string Path { get; } = path;

    public void Write<T>(T record)
    {
        string line = JsonSerializer.Serialize(record, Json) + "\n";
        using var f = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
        var bytes = System.Text.Encoding.UTF8.GetBytes(line);
        f.Write(bytes);
        f.Flush(flushToDisk: true);
    }

    /// Reads a log back: its header and every whole generation line so far (a line still being
    /// written, with no newline yet, is left for the next read).
    public static (RunHeader Header, List<GenerationRecord> Generations) Read(string path)
    {
        using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        string text = new StreamReader(f).ReadToEnd();
        var lines = text[..(text.LastIndexOf('\n') + 1)].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) throw new InvalidDataException($"{path} has no header yet");
        var header = JsonSerializer.Deserialize<RunHeader>(lines[0], Json) ?? throw new InvalidDataException("empty header");
        if (header.Type != "header" || header.Schema != Schema) throw new InvalidDataException($"{path}: not a schema {Schema} progress log");
        return (header, [.. lines.Skip(1).Select(l => JsonSerializer.Deserialize<GenerationRecord>(l, Json)!)]);
    }

    public static RunHeader Header(IReadOnlyList<Parameter> ps, double[] today, BandSpec spec, TuneOptions opt, int lambda,
        string command, Provenance provenance, string bandsJson, string paramsJson)
    {
        var parse = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var scenarios = spec.Used.ToDictionary(s => s, s =>
        {
            var c = spec.Scenarios[s];
            var bands = spec.Bands.Select(b => (b, "band")).Concat(spec.Penalties.Select(b => (b, "penalty"))).Where(x => x.b.Scenario == s)
                .Select(x => new BandInfo(x.b.Name, x.Item2, x.b.Metric, x.b.Min, x.b.Max, x.b.Scale, x.b.Weight, x.b.Scored)).ToList();
            return new ScenarioInfo(c.Setup, c.Weapon, c.Cond, c.Policy, Fights.Watchable(c), bands);
        });
        return new RunHeader("header", Schema, DateTime.Now.ToString("O"), command, provenance,
            new SeedRule(opt.Seed.ToString(), "Seeds.Mix(runSeed, generation)",
                "Seeds.Fight(generationSeed, scenario, index) = Mix(Mix(generationSeed, Of(scenario)), index) & 0x7FFFFFFF", Fights.DefaultMapSeed,
                opt.Fights ?? spec.Fights),
            [new PopulationInfo(Population, "cmaes", lambda, [.. ps.Select((p, i) => new ParamInfo(p.Name, p.Paths, p.Min, p.Max, p.Log, p.Integer, today[i]))])],
            scenarios, [.. spec.Orders.Select(o => new OrderInfo(o.Name, o.Metric, o.Scenarios, o.Scored))], FightOutcome.Metrics,
            FightOutcome.EndingCodes.ToDictionary(kv => kv.Value.ToString(), kv => kv.Key),
            new JsonObject { ["bands"] = JsonNode.Parse(bandsJson, documentOptions: parse), ["params"] = JsonNode.Parse(paramsJson, documentOptions: parse) });
    }

    /// The generation's line, from what the tuner computed anyway: no fight is fought for it.
    public static GenerationRecord Generation(int index, int evaluations, double seconds, ulong generationSeed, BandSpec spec,
        double[][] units, double[][] values, double[] fitness, int best, double bestSoFar, double sigma, double axisRatio,
        double[] meanUnit, double[] meanValues, JobResult bestResult)
    {
        var sorted = fitness.Order().ToArray();
        var scenarios = bestResult.Means.Keys.ToDictionary(s => s, s => new ScenarioResult(bestResult.Means[s], bestResult.Results[s],
            bestResult.Fights?[s] ?? "",
            [.. spec.AllBands.Where(b => b.Scenario == s).Select(b => new BandResult(b.Name, bestResult.Means[s][b.Metric], b.Distance(bestResult.Means[s][b.Metric]))),
                .. spec.Spread is { } sp ? [new BandResult("spread", OutcomeSpread.Commonest(bestResult.Means[s]), sp.Distance(s, bestResult.Means[s]))] : Array.Empty<BandResult>()]));
        // An order has no single value: its value is its distance.
        var orders = spec.Orders.Select(o => o.Distance(o.Scenarios.ToDictionary(s => s, s => bestResult.Means[s][o.Metric])))
            .Zip(spec.Orders, (d, o) => new BandResult(o.Name, d, d)).ToList();
        var pop = new PopulationRecord(Population, new Spread(sorted[0], sorted[sorted.Length / 2], sorted[^1], bestSoFar), sigma, axisRatio,
            new CandidateRecord(null, meanUnit, meanValues),
            [.. units.Select((u, k) => new CandidateRecord(fitness[k], u, values[k]))],
            new BestRecord(best, fitness[best], scenarios, orders));
        return new GenerationRecord("generation", index, evaluations, seconds, generationSeed.ToString(), [pop]);
    }
}
