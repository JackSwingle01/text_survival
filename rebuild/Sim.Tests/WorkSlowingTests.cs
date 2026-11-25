using CsCheck;
using Survival.Tests.Verification;

namespace Survival.Tests;

/// Work slows by how far the faculty it reads is below whole (#203): the whole-body time over
/// (a half plus half the faculty's share of a fresh body), the lower share setting it where two slow a verb.
public class WorkSlowingTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);
    static readonly TreePool Trees = new("Trees", Resource.BirchFirewood, 100);
    static readonly ForagePool Forage = new("forage", 1.0, Pools.MixedForest);

    /// The player in a fresh body of their kind, so whole is exactly the whole-body time.
    static World With(BodyState body, params PoolDef[] pools)
    {
        var w = TestWorlds.Bare(17);
        w.FixedWeather = Mild;
        w.PoolCache = new() { [Map.Of(w).Camp] = pools };
        w.Agent(P).Members[0] = body;
        return w;
    }

    static BodyState Whole() => SpeciesTable.Average(Species.Human);

    static BodyState Wounded(BodyState body, string region, double condition)
    {
        foreach (var t in body.Regions.Single(r => r.Name == region).Layers) t.Condition = condition;
        return body;
    }

    static int ButcherMinutes(BodyState body)
    {
        var w = With(body);
        var hare = BodyState.Create(BodyKind.Quadruped, 2.062641801423291, 0.1, 0.5);
        var c = Carcasses.Create(w, Map.Of(w).Camp, "Hare", hare, 0, false, false);
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var r = Sim.Step(w, new Butcher(P, c.Id), 240);
        Assert.Equal("butchered", r.InterruptedReason);
        return r.MinutesElapsed;
    }

    [Fact]
    public void OneArmDestroyed_ButchersSlower_AndAHalfArm_InBetween()
    {
        int whole = ButcherMinutes(Whole());
        int halfArm = ButcherMinutes(Wounded(Whole(), "Left Arm", 0.5));
        int noArm = ButcherMinutes(Wounded(Whole(), "Left Arm", 0));
        Assert.True(whole < halfArm && halfArm < noArm, $"{whole} {halfArm} {noArm}");
    }

    [Fact]
    public void AShareAtAHalf_TakesFourThirds_AndNearZero_CloseToTwice()
    {
        Assert.Equal(1.0, Work.TimeFactor(1.0), 12);
        Assert.Equal(4.0 / 3, Work.TimeFactor(0.5), 12);
        Assert.InRange(Work.TimeFactor(0.001), 1.99, 2.0);
    }

    static double ChopPct(BodyState body)
    {
        var w = With(body, Forage, Trees);
        Items.Pack(w, P).Gear.Add(Items.Make("ground_stone_axe"));
        Sim.Step(w, new Gather(P, 1), 20);
        return Pools.ChopProgressPct(w, new PoolId(Map.Of(w).Camp, 1));
    }

    [Fact]
    public void FellingWithABadLegAndAWorseArm_TakesTheArmsTime()
    {
        var both = Wounded(Wounded(Whole(), "Left Arm", 0.1), "Left Leg", 0.7);
        double speed = Body.Share(both, Species.Human, Faculty.Speed), dexterity = Body.Share(both, Species.Human, Faculty.Dexterity);
        Assert.True(dexterity < speed && speed < 1, $"{speed} {dexterity}"); // the leg is bad, the arm worse
        Assert.Equal(ChopPct(Whole()) / Work.TimeFactor(dexterity), ChopPct(both), 12);
    }

    [Fact]
    public void ABodyStrongerThanFresh_TakesTheWholeBodyTime_NotLess()
    {
        var strong = Whole();
        strong.MuscleKg *= 1.4;
        Assert.True(Body.Speed(strong) > Body.Speed(Whole()));
        Assert.Equal(ChopPct(Whole()), ChopPct(strong), 12);
    }

    [Fact]
    public void ForagingWithADamagedLungAndWholeLegs_TakesTheWholeBodyTime()
    {
        var body = Whole();
        body.Regions.SelectMany(r => r.Organs).First(o => o.Name.Contains("Lung")).Condition = 0;
        var w = With(body, Forage);
        Assert.Equal(1.0, Work.BodyTimeFactor(w, w.Agent(P), Map.Of(w).Camp, new Gather(P, 0)), 12);
    }

    [Fact]
    public void TheTreeForecast_AgreesWithTheTimeSteppingTakes_ForAWoundedBody()
    {
        Assert.True(FelledMinute_AsForecast(Wounded(Whole(), "Left Arm", 0.2)) > FelledMinute_AsForecast(Whole()));
    }

    /// The minute the forecast first says the tree is down is the minute stepping fells it.
    static int FelledMinute_AsForecast(BodyState body)
    {
        var w = With(body, Forage, Trees);
        Items.Pack(w, P).Gear.Add(Items.Make("ground_stone_axe"));
        int said = Enumerable.Range(1, 120).First(m => Work.ExpectedGather(w, w.Agent(P), new Gather(P, 1), m).ContainsKey(Resource.Stick));
        int felled = 0;
        while (felled < 120 && !Sim.Step(w, new Gather(P, 1), 1).Events.OfType<TreeFelled>().Any()) felled++;
        Assert.Equal(said, felled + 1);
        return said;
    }

    [Fact]
    public void TheMakingForecast_IsTheRateSteppingCredits_ForAWoundedBody()
    {
        var w = With(Wounded(Whole(), "Left Arm", 0.2));
        var r = Recipes.Get("stone_club");
        double rate = Making.WorkPerMinute(w, w.Agent(P), Items.Pack(w, P), r);
        Assert.Equal(1 / Work.TimeFactor(Body.Share(w.Agent(P).Body, Species.Human, Faculty.Dexterity)), rate, 12);
    }

    // ---- properties over any body ----

    static readonly (string Verb, Activity Activity, PoolDef? Pool)[] Verbs =
    [
        ("forage", new Gather(P, 0), Forage),
        ("fell", new Gather(P, 0), Trees),
        ("salvage", new Gather(P, 0), new StockPool("Wreck", StockKind.Salvage, 5, [new StockItem(Resource.Stick, 5, 0.4, 168)], RevealAtHours: 0)),
        ("butcher", new Butcher(P, 0), null),
        ("improve", new ImproveShelter(P, ShelterStat.Insulation, Resource.Stick), null),
    ];

    [Fact]
    public void AWorkVerbsTime_IsBetweenOnceAndTwiceTheWholeBodyTime_ForAnyBody()
    {
        var worlds = Verbs.Select(v => With(Whole(), v.Pool is { } p ? [p] : [])).ToArray();
        FacultiesTests.AnyBody.Where(t => t.Body.Kind == BodyKind.Human).Check(t =>
        {
            for (int i = 0; i < Verbs.Length; i++)
            {
                var w = worlds[i];
                w.Agent(P).Members[0] = t.Body;
                double f = Work.BodyTimeFactor(w, w.Agent(P), Map.Of(w).Camp, Verbs[i].Activity);
                Assert.InRange(f, 1, 2);
            }
            foreach (var r in new[] { Recipes.Get("stone_club"), Recipes.All.First(x => x.Result.Placed) })
            {
                double rate = Making.WorkPerMinute(worlds[0], worlds[0].Agent(P), Items.Pack(worlds[0], P), r with { Faster = [] });
                Assert.InRange(1 / rate, 1, 2);
            }
        }, iter: 200);
    }

    [Fact]
    public void AMakingOddsTable_SumsToOne_ForAnyDexterityAboveZero()
    {
        Gen.Select(Gen.Int[0, Recipes.All.Count - 1], Gen.Int[0, Skills.MaxLevel], Gen.Double[1e-9, 1]).Check(t =>
        {
            var (recipe, level, dexterity) = t;
            var odds = Making.Odds(Recipes.All[recipe], level, dexterity);
            Assert.All(odds.Outcomes, o => Assert.True(o.Odds.TotalPct >= 0, $"{o.Outcome} {o.Odds.TotalPct}"));
            Assert.Equal(1.0, odds.Outcomes.Sum(o => o.Odds.TotalPct) + odds.PlainPct, 9);
        });
    }
}
