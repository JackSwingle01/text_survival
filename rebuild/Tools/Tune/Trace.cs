namespace Survival.Tune;

/// One fighter at the end of a round, as a replay shows it. Numbers the body no longer has (a
/// herd member removed mid-fight) are null.
public sealed record TraceUnit(int Index, int Team, string Species, int Member, int X, int Y, string State, string? Action,
    int? Target, double? Resolve, double? Boldness, double? Vitality, double? Blood, double? Reserve, double? Wounds);

/// The grid after one round (round 0 is the opening placement). Fire is the burning fire's cell,
/// if the fight has one. Events are short plain lines: who was hurt, who died.
public sealed record TraceFrame(int Round, IReadOnlyList<TraceUnit> Units, TraceCell? Fire, IReadOnlyList<string> Events);

public sealed record TraceCell(int X, int Y);

/// One fight to run again: the content overrides it was fought with, its scenario and its seed.
public sealed record ReplayJob(IReadOnlyDictionary<string, double> Overrides, Scenario Scenario, int Seed);

/// What the fight came to, and its rounds when something could watch them. TraceNote says why
/// there are none.
public sealed record ReplayResult(string Outcome, int GridSize, IReadOnlyList<TraceFrame>? Frames, string? TraceNote);

public static class Replays
{
    /// Why a headless fight has no frames: the fight loop inside Sim has no round observer yet.
    public const string NoHook = "This fight is fought headless inside Sim (Combat.ResolveHeadless), which has no round observer yet. "
        + "Round-by-round playback needs the combat trace hook (rebuild/Tools/TuneViewer/README.md).";

    /// Which fight of a run to fight again: a generation, a candidate of it (null for the
    /// population's mean, which the search never fights), a scenario and a fight index. The seed is
    /// the one the tuner used (SeedRule), so a candidate's replay is the fight it was scored on.
    public static ReplayJob Resolve(RunHeader run, GenerationRecord gen, int? candidate, string scenario, int fight)
    {
        var pop = gen.Populations.Single(p => p.Id == ProgressLog.Population);
        var info = run.Populations.Single(p => p.Id == ProgressLog.Population);
        if (!run.Scenarios.TryGetValue(scenario, out var sc)) throw new ArgumentException($"The run has no scenario '{scenario}'");
        if (candidate is int k && (k < 0 || k >= pop.Candidates.Count)) throw new ArgumentException($"Generation {gen.Generation} has candidates 0-{pop.Candidates.Count - 1}, not {k}");
        if (fight < 0 || fight >= run.Seeds.FightsPerScenario) throw new ArgumentException($"Fights run 0-{run.Seeds.FightsPerScenario - 1}, not {fight}");
        var values = candidate is int c ? pop.Candidates[c].Values : pop.Mean.Values;
        var ps = info.Parameters.Select(p => new Parameter(p.Name, p.Paths, p.Min, p.Max, p.Log, p.Integer)).ToList();
        return new ReplayJob(Tuner.Overrides(ps, values), new Scenario(sc.Setup, sc.Weapon, sc.Cond, sc.Policy),
            Seeds.Fight(ulong.Parse(gen.GenerationSeed, System.Globalization.CultureInfo.InvariantCulture), scenario, fight));
    }

    /// What the log says that fight came to: recorded for the generation's best candidate only.
    public static string? Recorded(GenerationRecord gen, int? candidate, string scenario, int fight)
    {
        var best = gen.Populations.Single(p => p.Id == ProgressLog.Population).Best;
        if (candidate != best.Candidate || !best.Scenarios.TryGetValue(scenario, out var s) || s.Fights.Length == 0) return null;
        var codes = s.Fights.Split(' ');
        return fight < codes.Length ? codes[fight] : null;
    }

    /// The worker's side: overrides first, then the one fight, watched where it can be.
    public static ReplayResult RunInProcess(ReplayJob job)
    {
        ContentOverrides.Set(job.Overrides);
        var frames = new List<TraceFrame>();
        var outcome = new Fights().Run(job.Scenario, job.Seed, frames.Add);
        bool watched = Fights.Watchable(job.Scenario);
        return new ReplayResult(outcome.Code(), Combat.MapSize, watched ? frames : null, watched ? null : NoHook);
    }
}
