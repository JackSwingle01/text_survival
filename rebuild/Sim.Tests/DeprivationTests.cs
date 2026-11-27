namespace Survival.Tests;

/// Hungry, thirsty and tired are read from the body's stores each minute, never stored (#332).
public class DeprivationTests
{
    static Rng TestRng() => Rng.ForSystem(1, "test");
    static SurvivalContext Room() => new() { AirTempF = 70, ActivityFactor = 1.0 };

    static BodyState Emptied(Deprivation d)
    {
        var b = BodyState.BaselineHuman();
        switch (d)
        {
            case Deprivation.Thirsty: b.HydrationMl = 0; break;
            case Deprivation.Tired: b.EnergyMinutes = 0; break;
            case Deprivation.Hungry: b.CaloriesKcal = 0; break;
        }
        return b;
    }

    static void Refill(BodyState b, Deprivation d)
    {
        switch (d)
        {
            case Deprivation.Thirsty: Body.Drink(b, Body.MaxHydrationMl); break;
            case Deprivation.Tired: b.EnergyMinutes = Body.MaxEnergyMinutes; break;
            case Deprivation.Hungry: Body.Eat(b, Body.CaloriesCapacityKcal(b)); break;
        }
    }

    [Theory]
    [InlineData(Deprivation.Hungry)]
    [InlineData(Deprivation.Thirsty)]
    [InlineData(Deprivation.Tired)]
    public void ARefilledStore_LiftsItsDeprivation_OnTheVeryNextRead(Deprivation d)
    {
        var b = Emptied(d);
        Body.Update(b, Room(), TestRng());
        Assert.True(Deprivations.Severity(b, d) > 0.1, $"{d} {Deprivations.Severity(b, d):0.000} on an empty store");
        Refill(b, d);
        Assert.Equal(0, Deprivations.Severity(b, d));
    }

    /// The rule as the effects carried it before #332, written out here: the shared 30% line, the pang
    /// (5% off moving, of Hungry's 35%) and the ramp over the last 20 days the reserves can feed.
    static double OldRule(BodyState b, Deprivation d)
    {
        static double Below(double pct) => Math.Max(0, (0.30 - Math.Clamp(pct, 0, 1)) / 0.30);
        switch (d)
        {
            case Deprivation.Thirsty: return Below(b.HydrationMl / 4000);
            case Deprivation.Tired: return Below(b.EnergyMinutes / 960);
            default:
                double reserves = Math.Max(0, b.FatKg - 0.03 * b.WeightKg) * 7700 + Math.Max(0, b.MuscleKg - 0.15 * b.WeightKg) * 1320;
                double ramp = Math.Clamp(1 - reserves / (20 * Body.MetabolismKcalPerDay(b, 1.0)), 0, 1);
                return Math.Max(0.05 / 0.35 * Below(b.CaloriesKcal / Body.CaloriesCapacityKcal(b)), ramp);
        }
    }

    /// From full stores, with no food, water or sleep, every minute for two days: each severity is the old rule's.
    [Fact]
    public void FromFullToEmpty_EachDeprivation_IsTheOldRule_EveryMinute()
    {
        var b = BodyState.BaselineHuman();
        (b.CaloriesKcal, b.HydrationMl, b.EnergyMinutes) = (Body.CaloriesCapacityKcal(b), Body.MaxHydrationMl, Body.MaxEnergyMinutes);
        var rng = TestRng();
        var seen = new HashSet<Deprivation>();
        for (int minute = 0; minute < 2 * 1440; minute++)
        {
            Body.Update(b, Room(), rng);
            foreach (var d in Enum.GetValues<Deprivation>())
            {
                double severity = Deprivations.Severity(b, d);
                Assert.True(Math.Abs(OldRule(b, d) - severity) < 1e-12, $"minute {minute}: {d} {severity} against {OldRule(b, d)}");
                if (severity > 0) seen.Add(d);
            }
        }
        Assert.Equal(3, seen.Count);
        Assert.Equal(1, Deprivations.Severity(b, Deprivation.Thirsty));
        Assert.Equal(1, Deprivations.Severity(b, Deprivation.Tired));
    }

    /// A starved body at both floors is fully hungry, its stomach empty or not.
    [Fact]
    public void AtBothFloors_HungerIsFull()
    {
        var b = BodyState.BaselineHuman();
        double kg = b.StructuralKg / (1 - 0.03 - 0.15);
        (b.FatKg, b.MuscleKg) = (0.03 * kg, 0.15 * kg);
        Assert.Equal(1, Deprivations.Severity(b, Deprivation.Hungry), 9);
    }

    /// Animals live outside the person's survival model (Fauna): they have stores, and no deprivation yet.
    [Fact]
    public void AnAnimal_WithEmptyStores_ReadsNoDeprivation()
    {
        var wolf = SpeciesTable.Average(Species.Wolf);
        (wolf.CaloriesKcal, wolf.HydrationMl, wolf.EnergyMinutes) = (0, 0, 0);
        Assert.All(Enum.GetValues<Deprivation>(), d => Assert.Equal(0, Deprivations.Severity(wolf, d)));
        Assert.Empty(Deprivations.Of(wolf));
        var fed = SpeciesTable.Average(Species.Wolf);
        Assert.All(Enum.GetValues<Faculty>(), f => Assert.Equal(Body.Share(fed, Species.Wolf, f), Body.Share(wolf, Species.Wolf, f)));
    }

    static double[] Shares(BodyState b) => [.. Enum.GetValues<Faculty>().Select(f => Body.Share(b, Species.Human, f))];

    /// Moved from EffectsPropertyTests. Whatever the stores: emptying any of them further never raises a
    /// faculty, and hunger, thirst and tiredness never leave a whole body helpless: every faculty stays
    /// above a sixth of fresh. Hunger here is the pang, the most a stomach gives with reserves to spare.
    [Fact]
    public void Deprivations_AreMonotone_AndNeverLeaveAWholeBodyHelpless()
    {
        var rng = new Random(7);
        for (int trial = 0; trial < 2000; trial++)
        {
            var b = BodyState.BaselineHuman();
            (b.HydrationMl, b.EnergyMinutes, b.CaloriesKcal) =
                (rng.NextDouble() * 0.4 * Body.MaxHydrationMl, rng.NextDouble() * 0.4 * Body.MaxEnergyMinutes, rng.NextDouble() * 0.4 * Body.CaloriesCapacityKcal(b));
            var before = Shares(b);
            Assert.All(before, share => Assert.True(share > 1 / 6.0, $"trial {trial}: {share:0.000}"));
            double less = 1 - rng.NextDouble() * 0.5;
            var which = (Deprivation)rng.Next(3);
            switch (which)
            {
                case Deprivation.Thirsty: b.HydrationMl *= less; break;
                case Deprivation.Tired: b.EnergyMinutes *= less; break;
                default: b.CaloriesKcal *= less; break;
            }
            var after = Shares(b);
            for (int i = 0; i < before.Length; i++)
                Assert.True(after[i] <= before[i] + 1e-12, $"trial {trial}: a lower {which} store raised {(Faculty)i}");
        }
    }

    /// The losses of the three are summed per capacity and bend toward 0.8, x / (1 + (x/0.8)^4)^(1/4):
    /// read through a whole human's dexterity, manipulation times half of one plus consciousness.
    [Theory]
    [InlineData(0.2, 0.2)]
    [InlineData(0.6, 0.9)]
    [InlineData(1.0, 1.0)]
    [InlineData(0.0, 0.7)]
    public void TheLosses_SumAndSaturate(double thirst, double tired)
    {
        var b = BodyState.BaselineHuman();
        Deprive.Thirsty(b, thirst);
        Deprive.Tired(b, tired);
        static double Bent(double x) => x / Math.Pow(1 + Math.Pow(x / 0.8, 4), 0.25);
        double manipulation = 1 - Bent(0.25 * thirst + 0.30 * tired), consciousness = 1 - Bent(0.45 * thirst + 0.45 * tired);
        Assert.Equal(manipulation * (0.5 + 0.5 * consciousness), Body.Dexterity(b), 9);
        double sight = 1 - Bent(0.25 * tired), hearing = 1 - Bent(0.15 * tired);
        Assert.Equal(sight * consciousness, Body.Vision(b), 9);
        Assert.Equal((sight + hearing) / 2 * consciousness, Body.Perception(b), 9);
    }

    /// Along the reserves' ramp, fat at its floor and muscle falling to its own, hunger is the old rule's.
    [Fact]
    public void AlongTheReservesRamp_HungerIsTheOldRule()
    {
        var fresh = BodyState.BaselineHuman();
        double fatKg = 0.03 * fresh.WeightKg;
        for (double muscle = fresh.MuscleKg; muscle > 8; muscle -= 0.5)
        {
            var b = BodyState.BaselineHuman();
            (b.FatKg, b.MuscleKg) = (fatKg, muscle);
            Assert.Equal(OldRule(b, Deprivation.Hungry), Deprivations.Severity(b, Deprivation.Hungry), 12);
        }
        var midway = BodyState.BaselineHuman();
        (midway.FatKg, midway.MuscleKg) = (fatKg, 0.15 * midway.WeightKg + 12);
        Assert.InRange(Deprivations.Severity(midway, Deprivation.Hungry), 0.2, 0.8);
    }

    [Fact]
    public void AFedWateredRestedBody_FeelsNone()
    {
        var b = BodyState.BaselineHuman();
        Assert.Empty(Deprivations.Of(b));
        Assert.All(Enum.GetValues<Deprivation>(), d => Assert.False(Deprivations.Has(b, d)));
    }

    /// A store crossing its line within a minute is told that minute: tiredness starting awake, and ending in sleep.
    [Fact]
    public void ACrossing_IsToldTheMinuteItHappens()
    {
        var w = TestWorlds.Bare(1);
        var b = w.Agent(AgentId.Player).Body;
        b.EnergyMinutes = 0.30 * Body.MaxEnergyMinutes + 0.5;
        var started = Sim.Step(w, new Wait(AgentId.Player), 1).Events;
        Assert.Single(started.OfType<DeprivationStarted>(), e => e.Kind == Deprivation.Tired && e.Who == AgentId.Player);
        Assert.Empty(Sim.Step(w, new Wait(AgentId.Player), 1).Events.OfType<DeprivationStarted>());
        b.EnergyMinutes = 0.30 * Body.MaxEnergyMinutes - 0.5;
        var ended = Sim.Step(w, new Sleep(AgentId.Player), 1).Events;
        Assert.Single(ended.OfType<DeprivationEnded>(), e => e.Kind == Deprivation.Tired);
        Assert.Empty(ended.OfType<DeprivationStarted>());
    }

    /// A save holds the stores, not the deprivations: loaded, the body reads the same severities.
    [Fact]
    public void ASavedBody_HoldsNoDeprivation_AndLoadsReadingTheSame()
    {
        var w = TestWorlds.Bare(1);
        var b = w.Agent(AgentId.Player).Body;
        (b.CaloriesKcal, b.HydrationMl, b.EnergyMinutes) = (100, 500, 30);
        string json = Save.ToJson(w);
        foreach (var d in Enum.GetNames<Deprivation>()) Assert.DoesNotContain(d, json);
        var loaded = TestWorlds.Load(json).Agent(AgentId.Player).Body;
        Assert.All(Enum.GetValues<Deprivation>(), d => Assert.Equal(Deprivations.Severity(b, d), Deprivations.Severity(loaded, d)));
        Assert.Equal(3, Deprivations.Of(loaded).Count);
    }

    /// No stored effect is a deprivation: they are read, never kept.
    [Fact]
    public void NoEffectKind_IsADeprivation()
    {
        Assert.Empty(Enum.GetNames<EffectKind>().Intersect(Enum.GetNames<Deprivation>()));
        Assert.Empty(Content.Tables.Effects.Keys.Select(k => k.ToString()).Intersect(Enum.GetNames<Deprivation>()));
    }
}

/// Sets the stores a deprivation is read from, for tests that need one at a given severity.
internal static class Deprive
{
    internal static void Thirsty(BodyState b, double severity) => b.HydrationMl = Body.MaxHydrationMl * 0.30 * (1 - severity);

    internal static void Tired(BodyState b, double severity) => b.EnergyMinutes = Body.MaxEnergyMinutes * 0.30 * (1 - severity);

    /// The stomach empty and fat and muscle at their floors: hunger at full, and the body wasted.
    internal static void Starved(BodyState b)
    {
        double kg = b.StructuralKg / (1 - 0.03 - 0.15);
        (b.CaloriesKcal, b.FatKg, b.MuscleKg) = (0, 0.03 * kg, 0.15 * kg);
    }

    /// All three at full.
    internal static void Wholly(BodyState b)
    {
        Thirsty(b, 1);
        Tired(b, 1);
        Starved(b);
    }
}
