using System.Text.Json;
using System.Text.Json.Nodes;

namespace Survival.Tune;

/// One tunable number: where it lives (content paths, as Content.Override reads them; several paths
/// move together), its range, and whether to search it on a log scale or as a whole number.
public sealed record Parameter(string Name, IReadOnlyList<string> Paths, double Min, double Max, bool Log = false, bool Integer = false, string? Note = null)
{
    /// Where this number sits in the search's unit box: 0 at Min, 1 at Max, linear or logarithmic.
    public double ToUnit(double value) => Log ? Math.Log(value / Min) / Math.Log(Max / Min) : (value - Min) / (Max - Min);

    /// The value at a point of the unit box, clamped to the range and rounded if whole.
    public double FromUnit(double u)
    {
        u = Math.Clamp(u, 0, 1);
        double v = Log ? Min * Math.Pow(Max / Min, u) : Min + u * (Max - Min);
        return Integer ? Math.Round(v) : v;
    }
}

/// A target on one scenario's metric: the mean over its fights should lie in [Min, Max]. Distance
/// outside is divided by Scale (1 for rates, so 0.1 out is 0.1; rounds use a bigger scale) and
/// weighted. A band today's rules cannot express stays in the spec with Scored false: it is still
/// measured and reported, never scored.
public sealed record Band(string Name, string Scenario, string Metric, double? Min = null, double? Max = null,
    double Scale = 1, double Weight = 1, bool Scored = true, string? Note = null)
{
    public double Distance(double value) =>
        (Min is double lo && value < lo ? lo - value : Max is double hi && value > hi ? value - hi : 0) / Scale * Weight;
}

/// Scenarios listed best first on one metric (spear > axe > knife > hands): each one that beats the
/// one before it by more than Margin costs the excess.
public sealed record Order(string Name, string Metric, IReadOnlyList<string> Scenarios, double Margin = 0, double Scale = 1,
    double Weight = 1, bool Scored = true, string? Note = null)
{
    public double Distance(IReadOnlyDictionary<string, double> values)
    {
        double d = 0;
        for (int i = 1; i < Scenarios.Count; i++)
            d += Math.Max(0, values[Scenarios[i]] - values[Scenarios[i - 1]] + Margin);
        return d / Scale * Weight;
    }
}

/// Fights should not all end one way: a scenario whose commonest ending (the person dies, the animal
/// is killed, it leaves, the person gets away) is more than MaxShare of its fights costs the excess,
/// weighted. Scenarios that should be lopsided are listed in Exempt.
public sealed record OutcomeSpread(double MaxShare, double Weight = 1, IReadOnlyList<string>? Exempt = null)
{
    public static readonly string[] Endings = ["aDies", "bKilled", "bLeaves", "aFled"];

    public static double Commonest(IReadOnlyDictionary<string, double> means) => Endings.Max(e => means[e]);

    public double Distance(string scenario, IReadOnlyDictionary<string, double> means) =>
        Exempt?.Contains(scenario) == true ? 0 : Math.Max(0, Commonest(means) - MaxShare) * Weight;
}

/// What a candidate is scored on: the scenarios, the bands, the orderings, the "dies stupidly"
/// penalties (bands by another name, reported apart) and the spread, and how many fights each
/// scenario gets.
public sealed record BandSpec(int Fights, int ReportFights, IReadOnlyDictionary<string, Scenario> Scenarios,
    IReadOnlyList<Band> Bands, IReadOnlyList<Order> Orders, IReadOnlyList<Band> Penalties, OutcomeSpread? Spread = null)
{
    public IEnumerable<Band> AllBands => Bands.Concat(Penalties);

    /// Only the scenarios something scored or reported reads, in spec order.
    public IReadOnlyList<string> Used =>
        [.. Scenarios.Keys.Where(k => AllBands.Any(b => b.Scenario == k) || Orders.Any(o => o.Scenarios.Contains(k)))];

    /// Fitness: summed weighted distance outside every scored band, ordering and penalty, and every
    /// measured scenario's excess over the spread.
    public double Fitness(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> means) =>
        AllBands.Where(b => b.Scored).Sum(b => b.Distance(means[b.Scenario][b.Metric]))
        + Orders.Where(o => o.Scored).Sum(o => o.Distance(o.Scenarios.ToDictionary(s => s, s => means[s][o.Metric])))
        + SpreadDistance(means);

    public double SpreadDistance(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> means) =>
        Spread is { } sp ? Used.Sum(s => sp.Distance(s, means[s])) : 0;
}

public static class Specs
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        WriteIndented = true,
    };

    sealed record ParamRow(string Name, double Min, double Max, string? Path = null, IReadOnlyList<string>? Paths = null,
        bool Log = false, bool Integer = false, string? Note = null);
    sealed record ParamFile(IReadOnlyList<ParamRow> Parameters);

    /// Reads a parameter spec and checks it against the content as embedded: every path names a
    /// number, the range is sound, and today's value lies inside it (it is where the search starts).
    public static IReadOnlyList<Parameter> LoadParameters(string json, IReadOnlyDictionary<string, string> contentFiles)
    {
        var rows = JsonSerializer.Deserialize<ParamFile>(json, Options)?.Parameters ?? throw new InvalidDataException("empty parameter spec");
        var problems = new List<string>();
        var result = new List<Parameter>();
        foreach (var r in rows)
        {
            var paths = r.Paths ?? (r.Path != null ? [r.Path] : []);
            if (paths.Count == 0 || r.Path != null && r.Paths != null) { problems.Add($"{r.Name}: give path or paths, not both or neither"); continue; }
            if (!(r.Min < r.Max) || r.Log && r.Min <= 0) { problems.Add($"{r.Name}: range [{r.Min}, {r.Max}]{(r.Log ? " on a log scale" : "")}"); continue; }
            var p = new Parameter(r.Name, paths, r.Min, r.Max, r.Log, r.Integer, r.Note);
            foreach (var path in paths)
            {
                try
                {
                    double v = Current(contentFiles, path);
                    if (v < r.Min || v > r.Max) problems.Add($"{r.Name}: today's {path} = {v} is outside [{r.Min}, {r.Max}]");
                }
                catch (InvalidDataException e) { problems.Add($"{r.Name}: {e.Message}"); }
            }
            result.Add(p);
        }
        foreach (var dup in result.GroupBy(p => p.Name).Where(g => g.Count() > 1)) problems.Add($"parameter {dup.Key} is listed twice");
        foreach (var dup in result.SelectMany(p => p.Paths).GroupBy(x => x).Where(g => g.Count() > 1)) problems.Add($"path {dup.Key} is tuned twice");
        if (problems.Count > 0) throw new InvalidDataException("Parameter spec is broken:\n" + string.Join("\n", problems));
        return result;
    }

    /// Every content file as embedded in Sim, untouched by any override.
    public static IReadOnlyDictionary<string, string> EmbeddedContent() => Content.EmbeddedFiles();

    /// Today's value at a content path (the first path of a tied parameter is its starting value).
    public static double Current(IReadOnlyDictionary<string, string> files, string path)
    {
        int colon = path.IndexOf(':');
        if (colon <= 0 || !files.TryGetValue(path[..colon], out var text)) throw new InvalidDataException($"no content file for {path}");
        JsonNode? node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var key in path[(colon + 1)..].Split('.'))
            node = node switch
            {
                JsonObject o => o[key],
                JsonArray a when int.TryParse(key, out int i) && i >= 0 && i < a.Count => a[i],
                _ => null,
            } ?? throw new InvalidDataException($"{path}: no '{key}'");
        return node is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? v.GetValue<double>() : throw new InvalidDataException($"{path}: not a number");
    }

    sealed record BandFile(int Fights, int ReportFights, Dictionary<string, Scenario> Scenarios, IReadOnlyList<Band> Bands,
        IReadOnlyList<Order>? Orders = null, IReadOnlyList<Band>? Penalties = null, OutcomeSpread? Spread = null);

    /// Reads a band spec and checks every scenario runs and every reference resolves.
    public static BandSpec LoadBands(string json)
    {
        var f = JsonSerializer.Deserialize<BandFile>(json, Options) ?? throw new InvalidDataException("empty band spec");
        var spec = new BandSpec(f.Fights, f.ReportFights, f.Scenarios, f.Bands, f.Orders ?? [], f.Penalties ?? [], f.Spread);
        var problems = new List<string>();
        if (spec.Fights < 1 || spec.ReportFights < 1) problems.Add("fights and reportFights must be at least 1");
        foreach (var (name, s) in spec.Scenarios)
            try { Fights.Check(s); } catch (ArgumentException e) { problems.Add($"scenario {name}: {e.Message}"); }
        foreach (var b in spec.AllBands)
        {
            if (!spec.Scenarios.ContainsKey(b.Scenario)) problems.Add($"{b.Name}: no scenario {b.Scenario}");
            if (!FightOutcome.Metrics.Contains(b.Metric)) problems.Add($"{b.Name}: no metric {b.Metric}");
            if (b.Min == null && b.Max == null || b.Min > b.Max) problems.Add($"{b.Name}: band [{b.Min}, {b.Max}]");
            if (b.Scale <= 0 || b.Weight < 0) problems.Add($"{b.Name}: scale above 0, weight 0 or more");
            if (!b.Scored && string.IsNullOrWhiteSpace(b.Note)) problems.Add($"{b.Name}: an unscored band says why in its note");
        }
        foreach (var o in spec.Orders)
        {
            if (o.Scenarios.Count < 2) problems.Add($"{o.Name}: an order needs two scenarios");
            foreach (var s in o.Scenarios) if (!spec.Scenarios.ContainsKey(s)) problems.Add($"{o.Name}: no scenario {s}");
            if (!FightOutcome.Metrics.Contains(o.Metric)) problems.Add($"{o.Name}: no metric {o.Metric}");
        }
        if (spec.Spread is { } sp)
        {
            if (sp.MaxShare is <= 0 or > 1 || sp.Weight < 0) problems.Add($"spread: maxShare in (0, 1], weight 0 or more");
            foreach (var s in sp.Exempt ?? []) if (!spec.Used.Contains(s)) problems.Add($"spread: exempt {s} is not a measured scenario");
        }
        foreach (var dup in spec.AllBands.Select(b => b.Name).Concat(spec.Orders.Select(o => o.Name)).GroupBy(n => n).Where(g => g.Count() > 1))
            problems.Add($"{dup.Key} is listed twice");
        if (problems.Count > 0) throw new InvalidDataException("Band spec is broken:\n" + string.Join("\n", problems));
        return spec;
    }

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("empty json");
}
