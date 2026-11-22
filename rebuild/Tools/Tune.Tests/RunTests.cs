namespace Survival.Tune.Tests;

/// Fights, workers and whole runs: overrides reach the fights, and a seed fixes everything.
public class RunTests
{
    static readonly Dictionary<string, Scenario> Wolf = new() { ["wolf_spear"] = new("wolf1_aware", "wooden_spear", "healthy", "aggressive") };
    static string Scratch() => Path.Combine(Path.GetTempPath(), "tune-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Fights_AreAFunctionOfScenarioAndSeed_WhateverTheThreads()
    {
        var job = new Job(new Dictionary<string, double>(), Wolf, 60, 5, Threads: 1);
        var one = Workers.Fight(job);
        var many = Workers.Fight(job with { Threads = 6 });
        Assert.Equal(one.Means["wolf_spear"], many.Means["wolf_spear"]);
        Assert.Equal(one.Results["wolf_spear"], many.Results["wolf_spear"]);
        Assert.NotEqual(one.Means["wolf_spear"], Workers.Fight(job with { SeedBase = 6 }).Means["wolf_spear"]);
    }

    [Fact]
    public void Overrides_LoadInAWorker_AndChangeTheFights()
    {
        var plain = new Job(new Dictionary<string, double>(), Wolf, 100, 9, 2);
        var blunt = plain with { Overrides = new Dictionary<string, double> { ["items.json:gear.wooden_spear.weapon.damage"] = 0.1 } };
        var r = Workers.Run([plain, blunt, plain], 3, Scratch());
        // The same job twice in two processes: the same fights.
        Assert.Equal(r[0].Means["wolf_spear"], r[2].Means["wolf_spear"]);
        // And the in-process fights agree with the worker's, with no overrides.
        Assert.Equal(Workers.Fight(plain).Means["wolf_spear"], r[0].Means["wolf_spear"]);
        // A spear that barely scratches: the wolf dies far less and the person far more.
        Assert.True(r[1].Means["wolf_spear"]["bKilled"] < r[0].Means["wolf_spear"]["bKilled"] - 0.3, $"{r[1].Means["wolf_spear"]["bKilled"]} vs {r[0].Means["wolf_spear"]["bKilled"]}");
        Assert.True(r[1].Means["wolf_spear"]["aDies"] > r[0].Means["wolf_spear"]["aDies"] + 0.3);
    }

    /// The survey's stand-your-ground rows: a weapon in hand must not make standing up to an animal deadlier
    /// than standing bare-handed.
    [Theory]
    [Trait("Category", "Slow")]
    [InlineData("bear_close")]
    [InlineData("wolf1_aware")]
    public void StandingYourGround_WithASpear_IsNoWorseThanBareHanded(string setup)
    {
        var scenarios = new Dictionary<string, Scenario>
        {
            ["spear"] = new(setup, "wooden_spear", "healthy", "stand_intimidate"),
            ["hands"] = new(setup, "none", "healthy", "stand_intimidate"),
        };
        var r = Workers.Fight(new Job(new Dictionary<string, double>(), scenarios, 200, 11, Threads: 6));
        double spear = r.Means["spear"]["aDies"], hands = r.Means["hands"]["aDies"];
        Assert.True(spear <= hands + 0.08, $"{setup}: {spear:P0} die standing with a spear, {hands:P0} bare-handed");
    }

    [Fact]
    public void AWorkerGivenABadOverride_FailsTheRun()
    {
        var bad = new Job(new Dictionary<string, double> { ["species.json:species.Wolf.bravery"] = 1 }, Wolf, 5, 1, 1);
        var e = Assert.Throws<InvalidOperationException>(() => Workers.Run([bad], 1, Scratch()));
        Assert.Contains("bravery", e.Message);
    }

    [Fact]
    public void Seeds_DependOnEveryInput()
    {
        int s = Seeds.Fight(1, "a", 0);
        Assert.Equal(s, Seeds.Fight(1, "a", 0));
        Assert.NotEqual(s, Seeds.Fight(2, "a", 0));
        Assert.NotEqual(s, Seeds.Fight(1, "b", 0));
        Assert.NotEqual(s, Seeds.Fight(1, "a", 1));
        Assert.True(s >= 0);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void ATuningRun_IsAFunctionOfItsSeed_AndImprovesOnAToySpec()
    {
        // Two parameters, one scenario, a band the spear's damage can reach: deliberately easy.
        var ps = new[]
        {
            new Parameter("spear.damage", ["items.json:gear.wooden_spear.weapon.damage"], 0.1, 15, Log: true),
            new Parameter("wolf.boldness", ["species.json:species.Wolf.boldness"], 0.2, 4, Log: true),
        };
        var spec = new BandSpec(40, 80, Wolf, [new Band("dies", "wolf_spear", "aDies", 0.3, 0.5)], [], []);
        double[] today = [8, 1.3];
        var opt = new TuneOptions(Budget: 24, Seed: 3, Workers: 6, Sigma: 0.3, Lambda: 6);
        var a = Tuner.Run(ps, spec, today, opt, Scratch());
        var b = Tuner.Run(ps, spec, today, opt, Scratch());
        Assert.Equal(a.After, b.After);
        Assert.Equal(a.Log.Select(g => (g.Best, g.Median, g.Sigma)), b.Log.Select(g => (g.Best, g.Median, g.Sigma)));
        Assert.Equal(a.FitnessAfter, b.FitnessAfter);
        Assert.Equal(4, a.Log.Count);
        Assert.True(a.FitnessAfter < a.FitnessBefore, $"{a.FitnessBefore} -> {a.FitnessAfter}");
        var c = Tuner.Run(ps, spec, today, opt with { Seed = 4 }, Scratch());
        Assert.NotEqual(a.Log.Select(g => g.Best), c.Log.Select(g => g.Best));
    }
}
