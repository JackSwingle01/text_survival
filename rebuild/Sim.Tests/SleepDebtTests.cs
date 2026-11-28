namespace Survival.Tests;

/// A body stepped minute by minute and read through its faculties, its tiredness and its debt (#334).
public class SleepDebtTests
{
    static readonly SpeciesStats Human = SpeciesTable.Of(Species.Human);
    static SurvivalContext Room() => new() { AirTempF = 70, ActivityFactor = 1.0 };
    static SurvivalContext Asleep() => Room() with { ActivityFactor = 0.5, IsSleeping = true };

    /// Fed and watered each hour, so the debt is the only store that moves the faculties.
    static BodyState Rested()
    {
        var b = BodyState.BaselineHuman();
        b.SleepDebtMinutes = 0;
        return b;
    }

    static void TopUp(BodyState b)
    {
        b.HydrationMl = Body.MaxHydrationMl;
        b.CaloriesKcal = Body.CaloriesCapacityKcal(b);
    }

    static void Live(BodyState b, SurvivalContext ctx, int minutes, Rng rng)
    {
        for (int m = 0; m < minutes; m++)
        {
            if (m % 60 == 0) TopUp(b);
            Body.Update(b, ctx, rng);
        }
    }

    static double Tired(BodyState b) => Deprivations.Severity(b, Deprivation.Tired);

    [Fact]
    public void AwakeSixteenHours_FromNoDebt_IsNotTired_AndEveryFacultyIsARestedBodys()
    {
        var awake = Rested();
        Live(awake, Room(), 16 * 60, Rng.ForSystem(1, "body"));
        var rested = Rested();
        TopUp(rested);
        TopUp(awake);

        Assert.Equal(16 * 60, awake.SleepDebtMinutes, 6);
        Assert.Equal(0, Tired(awake));
        Assert.Equal(Body.Strength(rested), Body.Strength(awake), 6);
        Assert.Equal(Body.Speed(rested), Body.Speed(awake), 6);
        Assert.Equal(Body.Dexterity(rested), Body.Dexterity(awake), 6);
        Assert.Equal(Body.Perception(rested), Body.Perception(awake), 6);
    }

    [Theory]
    [InlineData(18, 0)]
    [InlineData(33, 0.5)]
    [InlineData(48, 1)]
    [InlineData(60, 1)]
    public void Tiredness_IsZeroAtEighteenHours_HalfAtThirtyThree_FullAtFortyEight(double hoursAwake, double tired)
    {
        var b = Rested();
        b.SleepDebtMinutes = hoursAwake * 60;
        Assert.Equal(tired, Tired(b), 9);
    }

    [Fact]
    public void Awake_TheDebtRisesAMinuteAMinute()
    {
        var b = Rested();
        b.SleepDebtMinutes = 100;
        Body.Update(b, Room(), Rng.ForSystem(1, "body"));
        Assert.Equal(101, b.SleepDebtMinutes, 9);
    }

    static double HoursToRepay(double debtHours, Func<BodyState, int, bool> step)
    {
        var b = Rested();
        b.SleepDebtMinutes = debtHours * 60;
        int minutes = 0;
        while (b.SleepDebtMinutes > 0 && minutes < 48 * 60) step(b, minutes++);
        return minutes / 60.0;
    }

    [Theory]
    [InlineData(16, 8.0, 5)]
    [InlineData(32, 11.0, 15)]
    [InlineData(40, 11.6, 15)]
    [InlineData(48, 12.1, 15)]
    public void Asleep_AnyDebtClearsInOneNight(double debtHours, double clearsInHours, double withinMinutes)
    {
        var rng = Rng.ForSystem(1, "body");
        double hours = HoursToRepay(debtHours, (b, m) =>
        {
            if (m % 60 == 0) TopUp(b);
            Body.Update(b, Asleep(), rng);
            return true;
        });
        Assert.InRange(hours, clearsInHours - withinMinutes / 60, clearsInHours + withinMinutes / 60);
    }

    /// A wolf sleeps twelve hours, so its base rate is one minute a minute: a waking day's debt clears in its own night.
    [Fact]
    public void AnotherSpecies_RepaysAtItsOwnBaseRate()
    {
        var wolf = SpeciesTable.Of(Species.Wolf);
        Assert.Equal(12, wolf.SleepHoursPerDay);
        Assert.Equal(1, SleepDebt.RepayPerMinute(wolf, debtMinutes: 6 * 60), 9);
        Assert.Equal(2, SleepDebt.RepayPerMinute(Human, debtMinutes: 6 * 60), 9);

        var body = SpeciesTable.Average(Species.Wolf);
        body.SleepDebtMinutes = 12 * 60;
        int minutes = 0;
        var rng = Rng.ForSystem(1, "body");
        while (body.SleepDebtMinutes > 0) { Body.LiveNeeds(body, sleeping: true, wolf, sleepFactor: 1, rng); minutes++; }
        Assert.Equal(12 * 60, minutes);
    }

    [Fact]
    public void AnAnimalsSleepDepth_MultipliesItsRepayRate()
    {
        var wolf = SpeciesTable.Of(Species.Wolf);
        var body = SpeciesTable.Average(Species.Wolf);
        body.SleepDebtMinutes = 100;
        Body.LiveNeeds(body, sleeping: true, wolf, sleepFactor: 0.75, Rng.ForSystem(1, "body"));
        Assert.Equal(99.25, body.SleepDebtMinutes, 9);
    }

    [Fact]
    public void EverySpecies_StartsOnTheHumanOnsets()
    {
        foreach (var species in Enum.GetValues<Species>())
        {
            var row = SpeciesTable.Of(species);
            Assert.Equal((18.0, 48.0), (row.TiredOnsetHours, row.TiredFullHours));
        }
    }

    [Fact]
    public void UnderThirtySixHours_NoBodyCollapses()
    {
        for (int seed = 1; seed <= 50; seed++)
        {
            var b = Rested();
            b.SleepDebtMinutes = 20 * 60;
            var rng = Rng.ForSystem(seed, "body");
            for (int m = 0; m < 16 * 60 - 1; m++)
            {
                if (m % 60 == 0) TopUp(b);
                Body.Update(b, Room(), rng);
            }
            Assert.True(b.SleepDebtMinutes < 36 * 60);
            Assert.False(Body.IsAsleep(b), $"seed {seed} collapsed under 36 hours");
        }
    }

    /// Held at 48 hours or more, each minute awake is about a 1% chance of dropping where it stands.
    [Fact]
    public void HeldAtFortyEightHours_ABodyCollapsesAboutOnceInAHundredMinutes()
    {
        int minutes = 0, collapses = 0;
        var rng = Rng.ForSystem(7, "body");
        var b = Rested();
        while (minutes < 40_000)
        {
            b.SleepDebtMinutes = (48 + minutes % 5) * 60;
            TopUp(b);
            Body.Update(b, Room(), rng);
            minutes++;
            if (Body.IsAsleep(b)) { collapses++; BodyAsleepWakes(b); }
        }
        Assert.InRange(collapses / (double)minutes, 0.008, 0.012);
    }

    static void BodyAsleepWakes(BodyState b) => Effects.Set(b, EffectKind.Asleep, 0);

    [Theory]
    [InlineData(35.9, 0)]
    [InlineData(36, 0)]
    [InlineData(42, 0.005)]
    [InlineData(48, 0.01)]
    [InlineData(70, 0.01)]
    public void TheCollapseOdds_RiseFromThirtySixHoursToOnePercentAtFortyEight(double debtHours, double pct) =>
        Assert.Equal(pct, SleepDebt.CollapsePctPerMinute(Human, debtHours * 60), 9);

    [Fact]
    public void ANewGamesPlayer_StartsAnHourIntoItsDay()
    {
        var world = TestWorlds.New(1);
        var body = world.Agent(AgentId.Player).Body;
        Assert.Equal(60, body.SleepDebtMinutes);
        Assert.Equal(0.75 * Body.MaxHydrationMl, body.HydrationMl, 6);
    }
}
