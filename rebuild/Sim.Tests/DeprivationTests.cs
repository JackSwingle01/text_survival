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

    /// Each store's line (spec "Thirst", "Hunger and wasting"): thirst from half the water store, the pang from
    /// 30% of the calorie store, tiredness from 30% of the energy store, each rising in a straight line to full at empty.
    [Theory]
    [InlineData(Deprivation.Thirsty, 1.0, 0)]
    [InlineData(Deprivation.Thirsty, 0.5, 0)]
    [InlineData(Deprivation.Thirsty, 0.25, 0.5)]
    [InlineData(Deprivation.Thirsty, 0.0, 1)]
    [InlineData(Deprivation.Hungry, 1.0, 0)]
    [InlineData(Deprivation.Hungry, 0.30, 0)]
    [InlineData(Deprivation.Hungry, 0.15, 0.5)]
    [InlineData(Deprivation.Hungry, 0.0, 1)]
    [InlineData(Deprivation.Tired, 0.30, 0)]
    [InlineData(Deprivation.Tired, 0.15, 0.5)]
    [InlineData(Deprivation.Tired, 0.0, 1)]
    public void EachDeprivation_IsZeroAboveItsLine_AndFullAtAnEmptyStore(Deprivation d, double storePct, double severity)
    {
        var b = BodyState.BaselineHuman();
        switch (d)
        {
            case Deprivation.Thirsty: b.HydrationMl = storePct * Body.MaxHydrationMl; break;
            case Deprivation.Tired: b.EnergyMinutes = storePct * Body.MaxEnergyMinutes; break;
            case Deprivation.Hungry: b.CaloriesKcal = storePct * Body.CaloriesCapacityKcal(b); break;
        }
        Assert.Equal(severity, Deprivations.Severity(b, d), 9);
    }

    /// Just above each line nothing is felt; just below, something is.
    [Fact]
    public void JustAboveItsLine_ADeprivationIsUnfelt_AndJustBelowItIsFelt()
    {
        var b = BodyState.BaselineHuman();
        b.HydrationMl = 0.5 * Body.MaxHydrationMl + 1;
        b.CaloriesKcal = 0.3 * Body.CaloriesCapacityKcal(b) + 1;
        Assert.Empty(Deprivations.Of(b));
        b.HydrationMl -= 2;
        b.CaloriesKcal -= 2;
        Assert.True(Deprivations.Has(b, Deprivation.Thirsty));
        Assert.True(Deprivations.Has(b, Deprivation.Hungry));
    }

    /// Starving on its reserves with an empty stomach, a body feels the pang at full and nothing more: its dexterity
    /// and perception are those of a fresh body with an empty stomach, a tenth of perception below a fed one's.
    [Fact]
    public void StarvingOnReserves_WithAnEmptyStomach_IsThePangAtFull_AndNoFurtherHunger()
    {
        var starving = BodyState.BaselineHuman();
        Deprive.Starved(starving);
        var emptyStomach = BodyState.BaselineHuman();
        emptyStomach.CaloriesKcal = 0;
        var fed = BodyState.BaselineHuman();
        Assert.Equal(1, Deprivations.Severity(starving, Deprivation.Hungry), 9);
        Assert.Equal(Body.Perception(emptyStomach), Body.Perception(starving), 9);
        Assert.Equal(Body.Dexterity(emptyStomach), Body.Dexterity(starving), 9);
        Assert.Equal(0.9 * Body.Perception(fed), Body.Perception(starving), 9);
    }

    /// Tiredness alone, at any severity, dulls the mind: strength and speed are a rested body's, dexterity and
    /// perception fall, perception (which is consciousness on a whole body) by half at full.
    [Theory]
    [InlineData(0.2)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void Tiredness_LeavesStrengthAndSpeedWhole_AndLowersDexterityAndPerception(double severity)
    {
        var rested = BodyState.BaselineHuman();
        var tired = BodyState.BaselineHuman();
        Deprive.Tired(tired, severity);
        Assert.Equal(Body.Strength(rested), Body.Strength(tired), 12);
        Assert.Equal(Body.Speed(rested), Body.Speed(tired), 12);
        Assert.True(Body.Dexterity(tired) < Body.Dexterity(rested));
        Assert.Equal(1 - 0.5 * severity, Body.Perception(tired) / Body.Perception(rested), 9);
    }

    /// Thirst at full thins the blood the body reads, as if 15% were lost: limbs at 0.7 through the lost-blood scale.
    /// What the body has stored, and what keeps it alive, are an unthirsty body's.
    [Fact]
    public void ThirstAtFull_LowersSpeedAndStrengthThroughBlood_AndLeavesStoredBloodAndAliveAlone()
    {
        var watered = BodyState.BaselineHuman();
        var thirsty = BodyState.BaselineHuman();
        Deprive.Thirsty(thirsty, 1);
        Assert.Equal(0.7 * Body.Speed(watered), Body.Speed(thirsty), 9);
        Assert.True(Body.Strength(thirsty) < Body.Strength(watered));
        Assert.Equal(watered.BloodPct, thirsty.BloodPct);
        Assert.Equal(Body.BloodVolumeMl(watered), Body.BloodVolumeMl(thirsty));
        Assert.Equal(Body.TissueVitality(watered), Body.TissueVitality(thirsty));
        Assert.True(Body.IsAlive(thirsty));
    }

    /// All three at their worst leave alive and tissue vitality as a body with full stores has them.
    [Fact]
    public void AllThreeAtFull_LeaveTissueVitalityAlone()
    {
        var full = BodyState.BaselineHuman();
        var deprived = BodyState.BaselineHuman();
        Deprive.Thirsty(deprived, 1);
        Deprive.Tired(deprived, 1);
        Deprive.Hungry(deprived, 1);
        Assert.Equal(Body.TissueVitality(full), Body.TissueVitality(deprived));
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
                (rng.NextDouble() * 0.6 * Body.MaxHydrationMl, rng.NextDouble() * 0.4 * Body.MaxEnergyMinutes, rng.NextDouble() * 0.4 * Body.CaloriesCapacityKcal(b));
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

    /// The factors multiply (spec "The three routes"): on a whole body, perception is consciousness, which is
    /// tired's (1 - 0.5 s) times thirst's (1 - 0.3 t) times hunger's (1 - 0.1 h), times thirst's lost-blood scale
    /// (1 - 0.3 t). Sight and hearing lose nothing.
    [Theory]
    [InlineData(0.2, 0.2, 0.2)]
    [InlineData(0.6, 0.9, 0.0)]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(0.0, 0.7, 0.5)]
    public void TheFactors_Multiply(double thirst, double tired, double hunger)
    {
        var fresh = BodyState.BaselineHuman();
        var b = BodyState.BaselineHuman();
        Deprive.Thirsty(b, thirst);
        Deprive.Tired(b, tired);
        Deprive.Hungry(b, hunger);
        double consciousness = (1 - 0.5 * tired) * (1 - 0.3 * thirst) * (1 - 0.1 * hunger) * (1 - 0.3 * thirst);
        Assert.Equal(consciousness, Body.Perception(b) / Body.Perception(fresh), 9);
        Assert.Equal(consciousness, Body.Vision(b) / Body.Vision(fresh), 9);
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
    internal static void Thirsty(BodyState b, double severity) => b.HydrationMl = Body.MaxHydrationMl * 0.50 * (1 - severity);

    /// The pang alone: the calorie store below its line, the reserves untouched.
    internal static void Hungry(BodyState b, double severity) => b.CaloriesKcal = Body.CaloriesCapacityKcal(b) * 0.30 * (1 - severity);

    internal static void Tired(BodyState b, double severity) => b.EnergyMinutes = Body.MaxEnergyMinutes * 0.30 * (1 - severity);

    /// The stomach empty, fat nearly gone and muscle wasted to where hunger begins to harm the heart: hunger at
    /// full, and the body wasted.
    internal static void Starved(BodyState b)
    {
        double muscleKg = Body.LethalMuscleOfFreshPct * SpeciesTable.FreshMuscleKg(Species.Human, b);
        (b.CaloriesKcal, b.FatKg, b.MuscleKg) = (0, 0.05 * (b.StructuralKg + muscleKg), muscleKg);
    }

    /// All three at full.
    internal static void Wholly(BodyState b)
    {
        Thirsty(b, 1);
        Tired(b, 1);
        Starved(b);
    }
}
