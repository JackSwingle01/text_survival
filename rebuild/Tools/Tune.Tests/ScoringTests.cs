namespace Survival.Tune.Tests;

public class ScoringTests
{
    internal static string SpecDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "Tune", "specs"))) return Path.Combine(d.FullName, "Tune", "specs");
        throw new DirectoryNotFoundException("rebuild/Tools/Tune/specs");
    }

    [Theory]
    [InlineData(0.6, 0)]
    [InlineData(0.5, 0)] // edges are inside
    [InlineData(0.7, 0)]
    [InlineData(0.4, 0.1)]
    [InlineData(0.85, 0.15)]
    public void Band_CountsOnlyTheDistanceOutside(double value, double expected) =>
        Assert.Equal(expected, new Band("b", "s", "bKilled", 0.5, 0.7).Distance(value), 12);

    [Fact]
    public void Band_OneSided_ScaledAndWeighted()
    {
        Assert.Equal(0, new Band("b", "s", "aDies", Max: 0.05).Distance(0));
        Assert.Equal(0.3, new Band("b", "s", "aDies", Max: 0.05, Weight: 2).Distance(0.2), 12);
        Assert.Equal(0.2, new Band("b", "s", "rounds", 3, 8, Scale: 10).Distance(10), 12);
        Assert.Equal(0.1, new Band("b", "s", "aDies", Min: 0.25).Distance(0.15), 12);
    }

    [Fact]
    public void Order_CostsEachInversion_WithItsMargin()
    {
        var o = new Order("o", "aWinsOrDrivesOff", ["spear", "axe", "knife", "hands"]);
        var values = new Dictionary<string, double> { ["spear"] = 0.8, ["axe"] = 0.9, ["knife"] = 0.5, ["hands"] = 0.6 };
        Assert.Equal(0.1 + 0 + 0.1, o.Distance(values), 12);
        Assert.Equal(0.0, o.Distance(new Dictionary<string, double> { ["spear"] = 0.9, ["axe"] = 0.8, ["knife"] = 0.7, ["hands"] = 0.1 }), 12);
        // A margin asks each to beat the next by that much.
        Assert.Equal(3 * 0.05, (o with { Margin = 0.15 }).Distance(new Dictionary<string, double> { ["spear"] = 0.9, ["axe"] = 0.8, ["knife"] = 0.7, ["hands"] = 0.6 }), 12);
    }

    [Fact]
    public void Fitness_SumsScoredBandsOrdersAndPenalties_AndSkipsUnscored()
    {
        var spec = new BandSpec(10, 10, new Dictionary<string, Scenario>(),
            [new Band("a", "s1", "bKilled", 0.5, 0.7), new Band("skip", "s1", "aDies", Max: 0, Scored: false, Note: "cannot")],
            [new Order("o", "bKilled", ["s1", "s2"])],
            [new Band("p", "s2", "aDies", Max: 0.02, Weight: 2)]);
        IReadOnlyDictionary<string, double> M(double kill, double dies) => FightOutcome.Metrics.ToDictionary(m => m, m => m == "bKilled" ? kill : m == "aDies" ? dies : 0);
        var means = new Dictionary<string, IReadOnlyDictionary<string, double>> { ["s1"] = M(0.3, 0.9), ["s2"] = M(0.4, 0.07) };
        // band a: 0.2 under; skip: not counted; order: s2 beats s1 by 0.1; penalty: 0.05 over, times 2
        Assert.Equal(0.2 + 0.1 + 0.1, spec.Fitness(means), 12);
    }

    /// The spread costs a measured scenario's commonest ending above its share, and nothing for one exempt.
    [Fact]
    public void Spread_CostsAScenarioThatEndsOneWay_UnlessExempt()
    {
        var wolf = new Scenario("wolf1_aware", "wooden_spear", "healthy", "aggressive");
        var spec = new BandSpec(10, 10, new Dictionary<string, Scenario> { ["s1"] = wolf, ["s2"] = wolf, ["s3"] = wolf },
            [new Band("a", "s1", "rounds", Min: 0), new Band("b", "s2", "rounds", Min: 0), new Band("c", "s3", "rounds", Min: 0)], [], [],
            new OutcomeSpread(0.95, Weight: 2, Exempt: ["s3"]));
        IReadOnlyDictionary<string, double> M(double dies, double kill, double leaves) =>
            FightOutcome.Metrics.ToDictionary(m => m, m => m switch { "aDies" => dies, "bKilled" => kill, "bLeaves" => leaves, _ => 0.0 });
        var means = new Dictionary<string, IReadOnlyDictionary<string, double>> { ["s1"] = M(0.01, 0.98, 0.01), ["s2"] = M(0.3, 0.4, 0.3), ["s3"] = M(1, 0, 0) };
        Assert.Equal((0.98 - 0.95) * 2, spec.Fitness(means), 12);
    }

    [Fact]
    public void FightOutcome_Metrics()
    {
        var won = new FightOutcome("Victory", false, true, false, 5);
        var drove = new FightOutcome("AnimalFled", false, false, false, 7);
        var died = new FightOutcome("Defeat", true, false, false, 9);
        var broke = new FightOutcome("AnimalDisengaged", false, false, false, 20);
        Assert.Equal([1, 1, 0, 0], new[] { won, drove, died, broke }.Select(o => o.Metric("aWinsOrDrivesOff")));
        Assert.Equal([0, 1, 0, 1], new[] { won, drove, died, broke }.Select(o => o.Metric("bLeaves")));
        Assert.Equal([0, 0, 1, 0], new[] { won, drove, died, broke }.Select(o => o.Metric("aDies")));
        Assert.Equal(9, died.Metric("rounds"));
        Assert.Throws<ArgumentException>(() => won.Metric("wins"));
    }

    [Theory]
    [InlineData(0.1, 15, true, false)]
    [InlineData(2, 10, false, true)]
    [InlineData(0.05, 3, false, false)]
    public void Parameter_MapsToTheUnitBoxAndBack(double min, double max, bool log, bool integer)
    {
        var p = new Parameter("p", ["f.json:x"], min, max, log, integer);
        Assert.Equal(min, p.FromUnit(0), 9);
        Assert.Equal(max, p.FromUnit(1), 9);
        Assert.Equal(min, p.FromUnit(-3), 9); // clamped
        Assert.Equal(max, p.FromUnit(7), 9);
        double mid = integer ? Math.Round((min + max) / 2) : (min + max) / 2;
        Assert.Equal(mid, p.FromUnit(p.ToUnit(mid)), 9);
        if (log) Assert.Equal(Math.Sqrt(min * max), p.FromUnit(0.5), 9);
        if (integer) Assert.Equal(Math.Round(p.FromUnit(0.33)), p.FromUnit(0.33));
    }

    [Fact]
    public void TheShippedSpecs_Load_AndStartAtTodaysValues()
    {
        var files = Specs.EmbeddedContent();
        var ps = Specs.LoadParameters(File.ReadAllText(Path.Combine(SpecDir(), "combat.json")), files);
        Assert.Contains(ps, p => p.Name == "wolf.boldness");
        Assert.Equal(SpeciesTable.Of(Species.Wolf).Boldness, Specs.Current(files, "species.json:species.Wolf.boldness"));
        Assert.Equal(4.84, Specs.Current(files, "items.json:gear.wooden_spear.weapon.damage"));
        var bands = Specs.LoadBands(File.ReadAllText(Path.Combine(SpecDir(), "combat-bands.json")));
        Assert.All(bands.AllBands.Where(b => !b.Scored), b => Assert.False(string.IsNullOrWhiteSpace(b.Note)));
        Assert.All(bands.Used, s => Assert.True(bands.Scenarios.ContainsKey(s)));
    }

    [Theory]
    [InlineData("""{ "parameters": [ { "name": "x", "path": "species.json:species.Wolf.bravery", "min": 0, "max": 1 } ] }""", "no 'bravery'")]
    [InlineData("""{ "parameters": [ { "name": "x", "path": "species.json:species.Wolf.boldness", "min": 2, "max": 3 } ] }""", "outside")]
    [InlineData("""{ "parameters": [ { "name": "x", "path": "species.json:species.Wolf.boldness", "min": 0, "max": 3, "log": true } ] }""", "log scale")]
    [InlineData("""{ "parameters": [ { "name": "x", "path": "species.json:species.Wolf.boldness", "min": 0, "max": 3 }, { "name": "y", "path": "species.json:species.Wolf.boldness", "min": 0, "max": 3 } ] }""", "tuned twice")]
    [InlineData("""{ "parameters": [ { "name": "x", "min": 0, "max": 3 } ] }""", "path or paths")]
    public void ABrokenParameterSpec_SaysWhat(string json, string expected)
    {
        var e = Assert.Throws<InvalidDataException>(() => Specs.LoadParameters(json, Specs.EmbeddedContent()));
        Assert.Contains(expected, e.Message);
    }

    [Theory]
    [InlineData("""{ "fights": 1, "reportFights": 1, "scenarios": { "s": { "setup": "wolf1_aware" } }, "bands": [ { "name": "b", "scenario": "t", "metric": "aDies", "max": 1 } ] }""", "no scenario t")]
    [InlineData("""{ "fights": 1, "reportFights": 1, "scenarios": { "s": { "setup": "wolf1_aware" } }, "bands": [ { "name": "b", "scenario": "s", "metric": "wins", "max": 1 } ] }""", "no metric wins")]
    [InlineData("""{ "fights": 1, "reportFights": 1, "scenarios": { "s": { "setup": "wolf1_aware" } }, "bands": [ { "name": "b", "scenario": "s", "metric": "aDies", "max": 1, "scored": false } ] }""", "says why")]
    [InlineData("""{ "fights": 1, "reportFights": 1, "scenarios": { "s": { "setup": "duel" } }, "bands": [] }""", "no setup 'duel'")]
    [InlineData("""{ "fights": 1, "reportFights": 1, "scenarios": { "s": { "setup": "wolf1_aware", "policy": "quick_melee" } }, "bands": [] }""", "hunts take")]
    [InlineData("""{ "fights": 1, "reportFights": 1, "scenarios": {}, "bands": [ { "name": "b", "scenario": "s", "metric": "aDies" } ] }""", "band [")]
    public void ABrokenBandSpec_SaysWhat(string json, string expected)
    {
        var e = Assert.Throws<InvalidDataException>(() => Specs.LoadBands(json));
        Assert.Contains(expected, e.Message);
    }
}
