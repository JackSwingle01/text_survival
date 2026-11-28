using Survival;

/// Starving past an empty calorie store, by Forbes' rule: each kilogram lost is part muscle, part fat, the muscle
/// share F₀ / (F₀ + fat kg). Spec 2026-11-27-deprivation-rethink, "Hunger and wasting". Bodies are stepped the
/// way the game steps them and read through what a caller reads: the stores, fat and muscle, strength and speed.
public class WastingTests
{
    static Rng TestRng() => Rng.ForSystem(1, "wasting");
    static readonly SurvivalContext Awake = new() { AirTempF = 70, ActivityFactor = 1.0 };
    // A sleeper burns at Sim's Sleep hold.
    static readonly SurvivalContext Sleeping = new() { AirTempF = 70, ActivityFactor = 0.5, IsSleeping = true };

    /// A body of the species-average weight, its fat set and the difference made up in muscle.
    static BodyState WithFat(double fatKg)
    {
        var b = BodyState.BaselineHuman();
        double weight = b.WeightKg;
        b.FatKg = fatKg;
        b.MuscleKg = weight - b.StructuralKg - fatKg;
        b.CaloriesKcal = 0;
        return b;
    }

    static double MuscleShareOfAnHourLost(BodyState b)
    {
        double fat = b.FatKg, muscle = b.MuscleKg;
        var rng = TestRng();
        for (int m = 0; m < 60; m++) Body.Update(b, Awake, rng);
        double muscleLost = muscle - b.MuscleKg, fatLost = fat - b.FatKg;
        return muscleLost / (muscleLost + fatLost);
    }

    [Fact]
    public void FromTheSameWeight_ALeanerBody_LosesALargerShareOfEachKilogramFromMuscle()
    {
        double previous = 0;
        foreach (double fatKg in new[] { 20, 14, 10, 8, 5, 3, 1.5, 0.5 })
        {
            double share = MuscleShareOfAnHourLost(WithFat(fatKg));
            Assert.True(share > previous, $"{fatKg} kg of fat: muscle share {share:0.000}, not above {previous:0.000}");
            Assert.InRange(share, 10.4 / (10.4 + fatKg) - 0.01, 10.4 / (10.4 + fatKg) + 0.01);
            previous = share;
        }
    }

    /// Asleep 8 hours a day, awake 16, watered every hour, no food. each runs once a minute before it is lived.
    static int DaysToDeath(BodyState b, Action<BodyState, int>? each = null, int maxDays = 120)
    {
        var rng = TestRng();
        for (int minute = 0; minute < maxDays * 1440; minute++)
        {
            if (minute % 60 == 0) Body.Drink(b, Body.MaxHydrationMlOf(b));
            each?.Invoke(b, minute);
            bool asleep = minute % 1440 >= 16 * 60;
            Body.Update(b, asleep ? Sleeping : Awake, rng);
            if (!Body.IsAlive(b)) return minute / 1440;
        }
        return -1;
    }

    static BodyState FedFull()
    {
        var b = BodyState.BaselineHuman();
        (b.CaloriesKcal, b.HydrationMl, b.EnergyMinutes) = (Body.CaloriesCapacityKcal(b), Body.MaxHydrationMlOf(b), Body.MaxEnergyMinutes);
        return b;
    }

    [Fact]
    public void ASpeciesAverageBody_StarvingAWeek_HasLostMuscle_AndStrengthAndSpeedWithIt()
    {
        var b = FedFull();
        double muscle = b.MuscleKg, strength = SpeciesTable.StrengthPct(Species.Human, b), speed = SpeciesTable.SpeedPct(Species.Human, b);
        Assert.Equal(-1, DaysToDeath(b, maxDays: 7));
        Assert.True(muscle - b.MuscleKg > 0.5, $"lost {muscle - b.MuscleKg:0.00} kg of muscle in a week");
        Assert.True(SpeciesTable.StrengthPct(Species.Human, b) < strength - 0.005, $"strength {SpeciesTable.StrengthPct(Species.Human, b):0.000}");
        Assert.True(SpeciesTable.SpeedPct(Species.Human, b) < speed - 0.005, $"speed {SpeciesTable.SpeedPct(Species.Human, b):0.000}");
    }

    /// Till death: fat and muscle stay above zero, the heart is harmed only once the store is empty and muscle is
    /// under half of fresh, and death comes between days 45 and 75 of starvation.
    [Theory]
    [Trait("Category", TestCategories.LongRun)]
    [InlineData(Sex.Male)]
    [InlineData(Sex.Female)]
    public void ASpeciesAverageBody_StarvesToDeath_InTwoMonths_WhenHalfItsMuscleIsGone(Sex sex)
    {
        var b = BodyState.BaselineHuman(sex);
        (b.CaloriesKcal, b.HydrationMl, b.EnergyMinutes) = (Body.CaloriesCapacityKcal(b), Body.MaxHydrationMlOf(b), Body.MaxEnergyMinutes);
        double halfOfFresh = BodyState.BaselineHuman(sex).MuscleKg / 2;
        double heart = Heart(b);
        int day = DaysToDeath(b, (body, minute) =>
        {
            Assert.True(body.FatKg > 0 && body.MuscleKg > 0, $"minute {minute}: fat {body.FatKg}, muscle {body.MuscleKg}");
            bool hungerMayHarm = body.CaloriesKcal <= 0 && body.MuscleKg < halfOfFresh;
            Assert.True(Heart(body) >= heart || hungerMayHarm, $"minute {minute}: the heart fell with muscle {body.MuscleKg:0.00} kg");
            heart = Heart(body);
        });
        Assert.Equal("starvation", Body.CauseOfDeath(b).What);
        Assert.InRange(day, 45, 75);
    }

    static double Heart(BodyState b) => b.Regions.SelectMany(r => r.Organs).Single(o => o.Name == "Heart").Condition;

    [Fact]
    public void HungerHarmsTheHeart_OnlyWithTheStoreEmpty_AndMuscleUnderHalfOfFresh()
    {
        var b = BodyState.BaselineHuman();
        double half = b.MuscleKg / 2;
        foreach (var (calories, muscleKg, harms) in new[] { (0.0, half + 0.01, false), (0.0, half - 0.01, true), (1.0, half - 0.01, false), (0.0, 1.0, true) })
        {
            (b.CaloriesKcal, b.MuscleKg) = (calories, muscleKg);
            Assert.Equal(harms, Body.LethalRates(b, Species.Human).HungerPerHour > 0);
        }
    }

    /// An animal lives its needs step (the burn, then Body.LiveNeeds) and wastes by the same rule.
    [Fact]
    public void AStarvingWolf_WastesMuscle_ByTheSameRule_AndDiesUnderHalfOfFresh()
    {
        var wolf = SpeciesTable.Average(Species.Wolf);
        wolf.CaloriesKcal = 0;
        double fat = wolf.FatKg, muscle = wolf.MuscleKg, fresh = wolf.MuscleKg;
        var rng = TestRng();
        for (int m = 0; m < 60; m++)
        {
            Body.SpendKcal(wolf, Body.BurnKcalPerMinute(wolf, 1.6));
            Body.LiveNeeds(wolf, Species.Wolf, sleeping: false, 10, 1, rng);
        }
        double muscleLost = muscle - wolf.MuscleKg, fatLost = fat - wolf.FatKg;
        Assert.InRange(muscleLost / (muscleLost + fatLost), 10.4 / (10.4 + fat) - 0.01, 10.4 / (10.4 + fat) + 0.01);
        Assert.Equal(0, Body.LethalRates(wolf, Species.Wolf).HungerPerHour);

        wolf.MuscleKg = fresh / 2 - 0.01;
        Assert.True(Body.LethalRates(wolf, Species.Wolf).HungerPerHour > 0);
    }
}
