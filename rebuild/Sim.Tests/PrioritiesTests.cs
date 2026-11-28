namespace Survival.Tests;

/// The player-facing reader of the stat priorities (Priorities.Of): the same priority functions the NPC ranks by, with no
/// hysteresis, read off the person as they are. The UI's alerts show its Critical stats.
public class PrioritiesTests
{
    [Fact]
    public void Of_ReadsEachStatsPriority_OffThePersonAsTheyAre()
    {
        var w = TestWorlds.Bare(17);
        var p = w.Agent(AgentId.Player);
        TestKit.Remove(Items.Pack(w, AgentId.Player), Resource.Water);
        var b = p.Body;
        (b.CoreTempF, b.HydrationMl, b.SleepDebtMinutes) = (98.6, 0.95 * Body.MaxHydrationMl, 0);
        var fine = Priorities.Of(w, AgentId.Player);
        Assert.Equal(Priority.None, fine.Warmth);
        Assert.Equal(Priority.None, fine.Rest);

        (b.CoreTempF, b.HydrationMl, b.SleepDebtMinutes) = (Priorities.HypothermiaCoreF - 0.5, 0.4 * Body.MaxHydrationMl, 20 * 60);
        var bad = Priorities.Of(w, AgentId.Player);
        Assert.Equal(Priority.Critical, bad.Warmth);
        Assert.Equal(Priority.Need, bad.Water);
        Assert.Equal(Priority.Need, bad.Rest);
        Assert.Equal(Priority.Critical, bad[Stat.Warmth]);
    }

    [Theory]
    [InlineData(98.6, false, 30.0, Priority.None)]
    [InlineData(98.6, false, 20.0, Priority.Want)]   // tonight's 16 h in, not a spare half-day
    [InlineData(98.6, false, 4.0, Priority.Need)]    // tonight's fuel short is warmth, Need
    [InlineData(97.4, false, 30.0, Priority.Need)]
    [InlineData(97.6, true, 30.0, Priority.Need)]    // seeing to it: held to the satisfied line
    [InlineData(97.6, false, 30.0, Priority.None)]
    [InlineData(95.0, false, 30.0, Priority.Critical)]
    public void Warmth(double coreF, bool seeing, double fuelHours, Priority priority) =>
        Assert.Equal(priority, Priorities.WarmthPriority(coreF, seeing, fuelHours, tonightHours: 16));

    [Theory]
    [InlineData(0.15, false, 3.0, 2.0, Priority.Critical)]
    [InlineData(0.40, false, 3.0, 2.0, Priority.Need)]
    [InlineData(0.80, true, 3.0, 2.0, Priority.Need)]
    [InlineData(0.80, false, 3.0, 2.0, Priority.None)]
    [InlineData(0.80, false, 1.0, 2.0, Priority.Want)]
    [InlineData(0.55, false, 0.0, 18.0, Priority.Need)] // nothing to hand and the evening's loss takes it to the line
    public void Water(double hydrationPct, bool seeing, double toHandL, double hoursLeft, Priority priority) =>
        Assert.Equal(priority, Priorities.WaterPriority(hydrationPct, seeing, toHandL, hoursLeft));

    /// A Want from a waking day of sleep debt (16 h), a Need once tiredness begins (18 h), Critical where a body may
    /// collapse (36 h); seen to, a Need above a waking day and a Want until repaid.
    [Theory]
    [InlineData(15.9, false, Priority.None)]
    [InlineData(16, false, Priority.Want)]
    [InlineData(17.9, false, Priority.Want)]
    [InlineData(18, false, Priority.Need)]
    [InlineData(35.9, false, Priority.Need)]
    [InlineData(36, false, Priority.Critical)]
    [InlineData(4, true, Priority.Want)]
    [InlineData(9, true, Priority.Want)]
    [InlineData(16.1, true, Priority.Need)]
    [InlineData(0, true, Priority.None)]
    public void Rest(double debtHours, bool seeing, Priority priority) =>
        Assert.Equal(priority, Priorities.RestPriority(SpeciesTable.Of(Species.Human), debtHours * 60, seeing));

    /// A 4 h sleeper's waking day (20 h) is longer than its onset (18 h): seeing to rest must still hold Need below the onset.
    [Theory]
    [InlineData(17, true, Priority.Need)]
    [InlineData(15.9, true, Priority.Want)]
    [InlineData(17, false, Priority.None)]
    [InlineData(18, false, Priority.Need)]
    public void Rest_ForAFourHourSleeper_HoldsNeedBelowTheOnsetWhileSeeingTo(double debtHours, bool seeing, Priority priority) =>
        Assert.Equal(priority, Priorities.RestPriority(SpeciesTable.Of(Species.Caribou), debtHours * 60, seeing));

    [Fact]
    public void Food_UnderTheFloorIsNeed_StarvingIsCritical()
    {
        var b = BodyState.BaselineHuman();
        double target = b.FatKg;
        Assert.Equal(Priority.Want, Priorities.FoodPriority(b, target, false, foodToHand: true));
        b.CaloriesKcal = 0;
        Assert.Equal(Priority.Need, Priorities.FoodPriority(b, target, false, foodToHand: false)); // stomach empty, nothing for today
        b.CaloriesKcal = 1500;
        b.FatKg = 0.09 * b.WeightKg; // under the 10% floor: weeks of reserve, Need
        Assert.Equal(Priority.Need, Priorities.FoodPriority(b, b.FatKg, false, foodToHand: true));
        b.MuscleKg = (Priorities.StarvingMuscleOfFreshPct - 0.01) * SpeciesTable.FreshMuscleKg(Species.Human, b); // wasted, but fed today
        Assert.Equal(Priority.Need, Priorities.FoodPriority(b, b.FatKg + 1, false, foodToHand: true));
        b.CaloriesKcal = 0;
        Assert.True(Priorities.Starving(b, Species.Human));
        Assert.Equal(Priority.Critical, Priorities.FoodPriority(b, b.FatKg, false, foodToHand: true));
    }

    /// As an NPC asks it (Npc passes no food to hand), with fat at its target: Need exactly while the hunger pang is felt.
    [Fact]
    public void Food_IsANeed_ExactlyWhenTheHungerPangIsFelt()
    {
        var b = BodyState.BaselineHuman();
        double capacity = Body.CaloriesCapacityKcal(b);
        for (double kcal = capacity; kcal >= 0; kcal -= capacity / 1000)
        {
            b.CaloriesKcal = kcal;
            bool need = Priorities.FoodPriority(b, b.FatKg, seeingTo: false, foodToHand: false) == Priority.Need;
            Assert.True(need == Deprivations.Has(b, Deprivation.Hungry), $"{kcal:0} kcal: need {need}, pang {Deprivations.Severity(b, Deprivation.Hungry):0.000}");
        }
        b.CaloriesKcal = 0.30 * capacity - 1;
        Assert.Equal(Priority.Need, Priorities.FoodPriority(b, b.FatKg, false, false));
    }
}
