namespace Survival.Tune.Tests;

/// The run log and replays: the log is written and parses, writing it changes nothing, and a fight
/// fought again from the log is the fight the tuner scored.
public class ProgressTests
{
    static string Scratch() => Path.Combine(Path.GetTempPath(), "tune-tests-" + Guid.NewGuid().ToString("N"));

    static readonly Dictionary<string, Scenario> Two = new()
    {
        ["wolf_spear"] = new("wolf1_aware", "wooden_spear", "healthy", "aggressive"),
        ["caribou_spear"] = new("hunt_caribou_resting", "wooden_spear", "healthy", "careful_melee"),
        ["pack_vs_caribou"] = new("wolves4_vs_caribou1_noticed"),
    };

    static readonly Parameter[] Ps =
    [
        new("spear.damage", ["items.json:gear.wooden_spear.weapon.damage"], 0.1, 15, Log: true),
        new("wolf.boldness", ["species.json:species.Wolf.boldness"], 0.2, 4, Log: true),
    ];

    static BandSpec Spec() => new(30, 40, Two,
        [new Band("dies", "wolf_spear", "aDies", 0.3, 0.5), new Band("meat", "caribou_spear", "bKilled", 0.5, 0.7)], [],
        [new Band("wolf lost", "pack_vs_caribou", "aDies", Max: 0.02)]);

    static readonly TuneOptions Opt = new(Budget: 12, Seed: 3, Workers: 6, Sigma: 0.3, Lambda: 4);
    static readonly double[] Today = [8, 1.3];

    static Provenance AtCommit(string c, string hash = "h") => new(c, false, hash, Provenance.ToolVersion);

    [Fact]
    public void FightCodes_RoundTrip()
    {
        foreach (var o in new[] { new FightOutcome("Defeat", true, false, false, 12), new FightOutcome("Victory", false, true, false, 3),
                     new FightOutcome("Fled", false, false, true, 0), new FightOutcome("AnimalDisengaged", false, false, false, 300) })
            Assert.Equal(o, FightOutcome.Parse(o.Code()));
        Assert.Equal("D1.12", new FightOutcome("Defeat", true, false, false, 12).Code());
    }

    [Fact]
    public void WatchingAFight_DoesNotChangeIt_AndSeesEveryRound()
    {
        var fights = new Fights();
        foreach (var name in new[] { "wolf_spear", "caribou_spear" })
            for (int seed = 1; seed <= 6; seed++)
            {
                var frames = new List<TraceFrame>();
                var watched = fights.Run(Two[name], seed, frames.Add);
                Assert.Equal(fights.Run(Two[name], seed), watched);
                Assert.True(Fights.Watchable(Two[name]));
                Assert.Equal(Enumerable.Range(0, frames.Count), frames.Select(f => f.Round));
                Assert.True(frames.Count >= 2, $"{name} seed {seed}: {frames.Count} frames");
                Assert.All(frames, f => Assert.Contains(f.Units, u => u.Team == 0));
            }
        Assert.False(Fights.Watchable(Two["pack_vs_caribou"]));
    }

    [Fact]
    public void Refusal_WhenTheCodeIsNotTheRunsCode()
    {
        Assert.Null(Provenance.ReplayRefusal(AtCommit("abc"), AtCommit("abc")));
        Assert.Contains("commit abc", Provenance.ReplayRefusal(AtCommit("abc"), AtCommit("def")));
        Assert.Contains("has changed", Provenance.ReplayRefusal(AtCommit("abc", "h1"), AtCommit("abc", "h2") with { Dirty = true }));
        Assert.NotNull(Provenance.ReplayRefusal(AtCommit("abc") with { Commit = null }, AtCommit("abc")));
    }

    [Fact]
    public void SourceHash_FollowsTheFightCode()
    {
        string repo = Provenance.RepoRoot(AppContext.BaseDirectory);
        Assert.Equal(Provenance.SourceHashOf(repo), Provenance.SourceHashOf(repo));
        var now = Provenance.Current(repo);
        Assert.Matches("^[0-9a-f]{64}$", now.SourceHash);
        Assert.NotNull(now.Commit);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void TheLog_IsWrittenAndParses_ChangesNothing_AndReplaysTheFightsItRecorded()
    {
        string dir = Scratch();
        Directory.CreateDirectory(dir);
        var log = new ProgressLog(Path.Combine(dir, ProgressLog.FileName));
        string repo = Provenance.RepoRoot(AppContext.BaseDirectory);
        log.Write(ProgressLog.Header(Ps, Today, Spec(), Opt, Tuner.Lambda(Ps.Length, Opt), "test", Provenance.Current(repo), "{}", "{}"));
        var logged = Tuner.Run(Ps, Spec(), Today, Opt, Scratch(), progress: log);
        var plain = Tuner.Run(Ps, Spec(), Today, Opt, Scratch());

        // The log changes nothing.
        Assert.Equal(plain.After, logged.After);
        Assert.Equal(plain.FitnessAfter, logged.FitnessAfter);
        Assert.Equal(plain.Log.Select(g => (g.Best, g.Median, g.Sigma)), logged.Log.Select(g => (g.Best, g.Median, g.Sigma)));

        // It parses, one line a generation, and agrees with the run's own log.
        var (header, gens) = ProgressLog.Read(log.Path);
        Assert.Equal(["spear.damage", "wolf.boldness"], header.Populations[0].Parameters.Select(p => p.Name));
        Assert.Equal(3, header.Scenarios.Count);
        Assert.Equal(3, gens.Count);
        for (int i = 0; i < gens.Count; i++)
        {
            var pop = gens[i].Populations[0];
            Assert.Equal(i + 1, gens[i].Generation);
            Assert.Equal(4, pop.Candidates.Count);
            Assert.Equal(logged.Log[i].Best, pop.Fitness.Best);
            Assert.Equal(logged.Log[i].Median, pop.Fitness.Median);
            Assert.Equal(pop.Candidates.Max(c => c.Fitness), pop.Fitness.Worst);
            Assert.Equal(pop.Fitness.Best, pop.Candidates[pop.Best.Candidate].Fitness);
            Assert.True(pop.Fitness.Best <= pop.Fitness.Median && pop.Fitness.Median <= pop.Fitness.Worst);
            Assert.Equal(logged.Log[i].Sigma, pop.Sigma);
            foreach (var (name, s) in pop.Best.Scenarios)
            {
                var codes = s.Fights.Split(' ');
                Assert.Equal(30, codes.Length);
                Assert.Equal(s.Results.Values.Sum(), codes.Length);
                Assert.Equal(s.Metrics["rounds"], codes.Average(c => FightOutcome.Parse(c).Rounds), 9);
            }
            Assert.Equal(["dies"], pop.Best.Scenarios["wolf_spear"].Bands.Select(b => b.Name));
        }

        // Fights fought again from the log, in their own processes: the fights the tuner recorded.
        foreach (var (g, scenario, fight) in new[] { (0, "wolf_spear", 0), (0, "wolf_spear", 17), (1, "caribou_spear", 3), (2, "caribou_spear", 29), (2, "pack_vs_caribou", 5), (1, "pack_vs_caribou", 11) })
        {
            int best = gens[g].Populations[0].Best.Candidate;
            var replay = Workers.Replay(Replays.Resolve(header, gens[g], best, scenario, fight), Scratch());
            Assert.Equal(Replays.Recorded(gens[g], best, scenario, fight), replay.Outcome);
            Assert.Equal(Fights.Watchable(Two[scenario]), replay.Frames != null);
            if (replay.Frames == null) Assert.Equal(Replays.NoHook, replay.TraceNote);
        }
        Assert.Null(Replays.Recorded(gens[0], null, "wolf_spear", 0));
    }
}
