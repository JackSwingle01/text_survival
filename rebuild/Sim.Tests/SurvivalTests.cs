namespace Survival.Tests;

/// Ported from text_survival.Tests/Survival (SurvivalProcessor, Warmth, Wetness, SleepByFire)
/// plus the effect registry's rules.
public class SurvivalTests
{
    static BodyState Baseline() => BodyState.BaselineHuman();
    static Rng TestRng() => Rng.ForSystem(1, "test");
    static SurvivalContext Room() => new() { AirTempF = 70, ActivityFactor = 1.0 };
    static SurvivalContext Asleep() => Room() with { ActivityFactor = 0.5, IsSleeping = true };
    static SurvivalContext StillAir(double clo) => new() { ClothingClo = clo, AirTempF = 60, ActivityFactor = 1.0 };

    static void Live(BodyState b, SurvivalContext ctx, int minutes)
    {
        var rng = TestRng();
        for (int i = 0; i < minutes; i++) Body.Update(b, ctx, rng);
    }

    // ---- needs ----

    [Fact]
    public void AnHourAwake_SpendsEnergyWaterAndFood()
    {
        var b = Baseline();
        var d = Body.Process(b, Room(), 60, TestRng());
        Assert.True(d.EnergyDeltaMinutes < 0 && d.HydrationDeltaMl < 0 && d.CaloriesDeltaKcal < 0);

        Body.Apply(b, d, TestRng());
        Assert.True(b.EnergyMinutes < 900 && b.HydrationMl < 3000 && b.CaloriesKcal < 1500);
    }

    [Fact]
    public void Sleep_RestoresEnergy_BurnsLess_AndLosesSeventyPercentWater()
    {
        var b = Baseline();
        Assert.True(Body.Process(b, Asleep(), 60, TestRng()).EnergyDeltaMinutes > 0);

        var asleep = Body.Process(b, Asleep(), 480, TestRng());
        var awake = Body.Process(b, Room(), 480, TestRng());
        Assert.True(Math.Abs(asleep.CaloriesDeltaKcal) < Math.Abs(awake.CaloriesDeltaKcal));

        double expectedMl = Body.BaseWaterLossMlPerDay / (24.0 * 60.0) * 0.7 * 480;
        Assert.Equal(expectedMl, -asleep.HydrationDeltaMl, 0);
    }

    [Fact]
    public void Metabolism_IsKatchMcArdleOnLeanMass()
    {
        double bmr = Body.MetabolismKcalPerDay(Baseline(), 1.0);
        Assert.InRange(bmr, 1650, 1850); // 370 + 21.6 * 63.75
        var muscular = BodyState.Create(BodyKind.Human, 90, 10.0 / 90, 40.0 / 90);
        Assert.True(Body.MetabolismKcalPerDay(muscular, 1.0) > bmr);
    }

    [Fact]
    public void LowStats_RaiseHungryThirstyTired()
    {
        var b = Baseline();
        b.CaloriesKcal = 100;
        b.HydrationMl = 100;
        b.EnergyMinutes = 10;
        Live(b, Room(), 1);
        Assert.True(Deprivations.Has(b, Deprivation.Hungry));
        Assert.True(Deprivations.Has(b, Deprivation.Thirsty));
        Assert.True(Deprivations.Has(b, Deprivation.Tired));
        Assert.True(Body.Perception(b) < 0.5);
    }

    // ---- hunger follows reserves ----

    /// An empty stomach on a body with fat to burn is the pang at full: a tenth off consciousness, the limbs whole.
    [Fact]
    public void Hunger_AnEmptyStomachWithFatToSpare_IsAPang()
    {
        var fed = Baseline();
        var b = Baseline();
        b.CaloriesKcal = 0;
        Live(fed, Room(), 1);
        Live(b, Room(), 1);
        Assert.Equal(1, Body.Speed(b) / Body.Speed(fed), 3);
        Assert.Equal(1, Body.Strength(b) / Body.Strength(fed), 3);
        Assert.InRange(Body.Dexterity(b) / Body.Dexterity(fed), 0.94, 0.96);
        Assert.InRange(Body.Perception(b) / Body.Perception(fed), 0.89, 0.91);
    }

    /// Lives the minutes eating back each hour what the hour burned, and drinking, so the store never
    /// runs low enough for the pang: eating at about maintenance.
    static void LiveAtMaintenance(BodyState b, int minutes, Action<int>? eachMinute = null)
    {
        var rng = TestRng();
        for (int minute = 1; minute <= minutes; minute++)
        {
            if (minute % 60 == 0)
            {
                Body.Eat(b, Body.MetabolismKcalPerDay(b, 1.0) / 24);
                Body.Drink(b, Body.MaxHydrationMl - b.HydrationMl);
            }
            Body.Update(b, Room(), rng);
            eachMinute?.Invoke(minute);
        }
    }

    /// Well fed comes a whole day after the last of the empty-stomach pang, not a minute sooner.
    [Fact]
    public void WellFed_StartsADayAfterTheLastHungry_NotBefore()
    {
        var b = Baseline();
        b.CaloriesKcal = 0;
        Live(b, Room(), 1);
        Assert.True(Deprivations.Has(b, Deprivation.Hungry));
        Body.Eat(b, Body.MaxCaloriesKcal);
        int lastHungry = 0, firstWellFed = 0;
        LiveAtMaintenance(b, 3 * 1440, minute =>
        {
            if (Deprivations.Has(b, Deprivation.Hungry)) lastHungry = minute;
            if (firstWellFed == 0 && Effects.Has(b, EffectKind.WellFed)) firstWellFed = minute;
        });
        Assert.Equal(0, lastHungry); // eating lifts the pang at once: it no longer fades over an hour
        Assert.Equal(lastHungry + 24 * 60, firstWellFed);
    }

    /// Hunger ends it the minute it comes, and the day starts over from the last of the pang.
    [Fact]
    public void WellFed_EndsTheMinuteHungerReturns_AndTheDayStartsOver()
    {
        var b = Baseline();
        LiveAtMaintenance(b, 2000);
        Assert.True(Effects.Has(b, EffectKind.WellFed));
        b.CaloriesKcal = 0;
        Live(b, Room(), 1);
        Assert.True(Deprivations.Has(b, Deprivation.Hungry));
        Assert.False(Effects.Has(b, EffectKind.WellFed));

        Body.Eat(b, Body.MaxCaloriesKcal);
        int lastHungry = 0, firstWellFed = 0;
        LiveAtMaintenance(b, 2 * 1440, minute =>
        {
            if (Deprivations.Has(b, Deprivation.Hungry)) lastHungry = minute;
            if (firstWellFed == 0 && Effects.Has(b, EffectKind.WellFed)) firstWellFed = minute;
        });
        Assert.Equal(lastHungry + 24 * 60, firstWellFed);
    }

    /// The minute hunger ends Well fed, the lines read in that order: no longer well fed, then hungry.
    [Fact]
    public void WellFedEnding_IsToldBeforeTheHungerThatEndsIt()
    {
        var w = TestWorlds.Bare(1);
        var p = w.Agent(AgentId.Player);
        p.Body.MinutesWithoutHunger = 2 * 1440;
        Effects.Set(p.Body, EffectKind.WellFed, 1);
        p.Body.CaloriesKcal = 0.30 * Body.CaloriesCapacityKcal(p.Body) + 0.1; // the minute's burn takes it below the line
        var events = Sim.Step(w, new Wait(AgentId.Player), 1).Events;
        int ended = events.FindIndex(e => e is EffectEnded { Kind: EffectKind.WellFed });
        int started = events.FindIndex(e => e is DeprivationStarted { Kind: Deprivation.Hungry });
        Assert.True(ended >= 0 && started >= 0, "hunger should start and Well fed end this minute");
        Assert.True(ended < started, "Well fed's end should be told before the hunger that ended it");
    }

    /// Eating what you burn and never going hungry: Well fed starts once, after a day, and holds.
    [Fact]
    public void WellFed_AtMaintenance_StartsOnceAndHolds()
    {
        var b = Baseline();
        int starts = 0, ends = 0, firstStart = 0;
        bool was = false;
        LiveAtMaintenance(b, 4 * 1440, minute =>
        {
            Assert.False(Deprivations.Has(b, Deprivation.Hungry), $"hungry at minute {minute}");
            bool now = Effects.Has(b, EffectKind.WellFed);
            if (now && !was) { starts++; if (firstStart == 0) firstStart = minute; }
            if (!now && was) ends++;
            was = now;
        });
        Assert.Equal((1, 0, 24 * 60), (starts, ends, firstStart));
    }

    /// Well fed makes 5% more of the body's own heat, paid in calories, and heals 5% faster, and only
    /// while it holds. The faculties are untouched: the capacities under them stay capped at whole.
    [Fact]
    public void WellFed_WarmsAndHealsFivePercentMore_OnlyWhileItHolds()
    {
        var b = Baseline();
        LiveAtMaintenance(b, 1439);
        b.Regions.Single(r => r.Name == "Left Arm").Muscle.Condition = 0.5;
        b.EnergyMinutes = Body.MaxEnergyMinutes; // a day awake: rested again, or nothing heals
        Assert.False(Effects.Has(b, EffectKind.WellFed));
        Assert.Equal((1.0, 1.0), (Effects.MetabolicHeatFactor(b), Effects.HealingFactor(b)));

        LiveAtMaintenance(b, 1);
        Assert.True(Effects.Has(b, EffectKind.WellFed));
        double heat = Body.CalculateHeatBalance(b, Room()).MetabolismKcalPerHour;
        var fed = Body.Process(b, Room(), 1, TestRng());
        var faculties = TestKit.FacultiesOf(b);
        Effects.Set(b, EffectKind.WellFed, 0); // the same body, only Well fed taken off
        var unfed = Body.Process(b, Room(), 1, TestRng());
        Assert.Equal(1.05, heat / Body.CalculateHeatBalance(b, Room()).MetabolismKcalPerHour, 9);
        Assert.Equal(1.05, fed.HealAmount / unfed.HealAmount, 4);
        // The heat is paid for: the thermic effect of food burns the calories it makes it from.
        Assert.Equal(1.05, fed.CaloriesDeltaKcal / unfed.CaloriesDeltaKcal, 9);
        Assert.Equal(faculties, TestKit.FacultiesOf(b));
    }

    // ---- exhaustion ----

    /// At zero energy a body does not drop at once: each minute awake there is about a 1% chance it falls
    /// asleep where it is, for 15 to 90 minutes, rolled on its own stream. Over 400 bodies the wait
    /// averages about a hundred minutes, few drop in the first minute, and every sleep lasts 15-90.
    [Fact]
    public void AtZeroEnergy_FallsAsleepAboutOnceInAHundredMinutes_ForAQuarterHourToAnHourAndAHalf()
    {
        var waits = new List<int>();
        var sleeps = new List<int>();
        for (int seed = 1; seed <= 400; seed++)
        {
            var b = Baseline();
            b.EnergyMinutes = 0;
            var rng = Rng.ForSystem(seed, "body");
            int minute = 0;
            while (!Effects.Has(b, EffectKind.Asleep) && minute < 2000) { Body.Update(b, Room(), rng); minute++; }
            Assert.True(Effects.Has(b, EffectKind.Asleep), $"seed {seed}: awake for 2000 minutes at zero energy");
            waits.Add(minute);
            int slept = 0;
            while (Effects.Has(b, EffectKind.Asleep)) { Body.Update(b, Room(), rng); slept++; }
            sleeps.Add(slept);
        }
        Assert.InRange(waits.Average(), 80, 125);
        Assert.True(waits.Count(w => w == 1) < 15, $"{waits.Count(w => w == 1)} of 400 dropped in the first minute");
        Assert.All(sleeps, s => Assert.InRange(s, 15, 90));
        Assert.True(sleeps.Min() <= 20 && sleeps.Max() >= 85, $"sleeps {sleeps.Min()}-{sleeps.Max()}");
    }

    /// Asleep, a body is not rolled again, and awake with energy left it is never rolled.
    [Fact]
    public void WithEnergyLeft_NoBodyFallsAsleep()
    {
        var b = Baseline();
        b.EnergyMinutes = 5000 / 6.0; // most of a day
        Live(b, Room(), 600);
        Assert.False(Effects.Has(b, EffectKind.Asleep));
    }

    /// A blow wakes a sleeper: whatever hurts enough to give pain ends the sleep.
    [Fact]
    public void ABlow_WakesASleeper()
    {
        var b = Baseline();
        Effects.Set(b, EffectKind.Asleep, 60 / 90.0);
        Body.Damage(b, new DamageInfo(0.3, DamageType.Blunt, BodyTarget.LeftArm), TestRng());
        Assert.False(Effects.Has(b, EffectKind.Asleep));
    }

    // ---- starvation, dehydration, regeneration ----

    [Fact]
    public void Starvation_BurnsFatAtSevenThousandSevenHundredKcalPerKg_AndMuscleAtThirteenHundredTwenty()
    {
        var b = Baseline();
        b.CaloriesKcal = 0;
        double fatBefore = b.FatKg;
        var d = Body.Process(b, Room(), 60, TestRng());
        double burnedKcal = Body.MetabolismKcalPerDay(b, 1.0) / 24;
        Assert.Equal(burnedKcal, d.FatBurnedKg * Body.KcalPerKgFat + d.MuscleBurnedKg * 1320, 9);
        Assert.Equal(Body.ForbesConstantKg / (Body.ForbesConstantKg + fatBefore), d.MuscleBurnedKg / (d.FatBurnedKg + d.MuscleBurnedKg), 9);
        Body.Apply(b, d, TestRng());
        Assert.Equal(0, b.CaloriesKcal);
        Assert.True(b.FatKg < fatBefore);
    }

    /// Old game: food never became fat - eating past a full store threw the surplus away (bug, fixed).
    [Fact]
    public void Eat_PastAFullStore_BecomesFat_NothingLost()
    {
        var b = Baseline();
        b.CaloriesKcal = 1500;
        double fatBefore = b.FatKg;
        Body.Eat(b, 3000);
        Assert.Equal(Body.MaxCaloriesKcal, b.CaloriesKcal, 9);
        Assert.Equal(2500 / Body.KcalPerKgFat, b.FatKg - fatBefore, 9); // kcal in = kcal stored + kcal laid down
    }

    /// A lean person eating more than they burn puts fat back on over days.
    [Fact]
    public void WellFed_RegainsFatOverDays()
    {
        var b = Baseline();
        b.FatKg = 7.5;
        var rng = TestRng();
        for (int minute = 0; minute < 3 * 1440; minute++)
        {
            if (minute % 1440 == 720) Body.Eat(b, 4500); // one big meal a day, well over a day's burn
            Body.Update(b, Room(), rng);
        }
        Assert.True(b.FatKg > 7.5 + 0.2, $"fat {b.FatKg:0.000} kg after three well-fed days");
    }

    [Fact]
    public void Starvation_ThenMuscle_ThenOrgans()
    {
        var b = Baseline();
        b.CaloriesKcal = 0;
        b.FatKg = 0; // nothing left to burn
        var muscle = Body.Process(b, Room(), 60, TestRng());
        Assert.True(muscle.MuscleBurnedKg > 0);
        Assert.Empty(muscle.Damage);

        b.MuscleKg = 0;
        var organs = Body.Process(b, Room(), 60, TestRng());
        var hit = Assert.Single(organs.Damage);
        Assert.Equal(DamageType.Internal, hit.Type);
    }

    /// Lives a body minute by minute until it dies or the minutes run out. each runs before every minute.
    static int MinutesToDeath(BodyState b, SurvivalContext ctx, int maxMinutes, Action<BodyState>? each = null)
    {
        var rng = TestRng();
        for (int m = 0; m < maxMinutes; m++)
        {
            each?.Invoke(b);
            Body.Update(b, ctx, rng);
            if (!Body.IsAlive(b)) return m + 1;
        }
        return -1;
    }

    /// Body review B4 (decided 2026-11-21): about three days without water at rest. Old (pinned here as
    /// 15-120 minutes from dry): thirst's harm was divided by the brain's toughness and ran away as it
    /// failed, so a dry body died within the hour, about 29 hours after its last drink.
    [Fact]
    public void Dehydration_KillsInAboutThreeDays_AtRest()
    {
        var b = Baseline();
        b.HydrationMl = Body.MaxHydrationMl;
        int minutes = MinutesToDeath(b, Room(), 10 * 1440);
        Assert.InRange(minutes / 1440.0, 2.5, 3.5);
        Assert.Equal("thirst", Body.CauseOfDeath(b).What);

        var dry = Baseline();
        dry.HydrationMl = 0;
        Assert.InRange(MinutesToDeath(dry, Room(), 10 * 1440) / 60.0, 24, 48); // from dry: a day and a half
    }

    /// Body review B4: starving takes weeks, through fat and muscle together, then the heart. Old:
    /// once both were spent an organ drawn at random failed within a couple of hours.
    [Fact]
    public void Starvation_TakesWeeks_ThenTheHeart_OnceHalfTheMuscleIsGone()
    {
        var b = Baseline();
        b.CaloriesKcal = 0;
        double halfOfFresh = b.MuscleKg / 2;
        double? halfGoneDay = null;
        int minute = 0;
        int minutes = MinutesToDeath(b, Room(), 100 * 1440, body =>
        {
            if (minute++ % 60 == 0) Body.Drink(body, 200);
            if (halfGoneDay == null && body.MuscleKg < halfOfFresh) halfGoneDay = minute / 1440.0;
        });
        Assert.InRange(minutes / 1440.0, 40, 75);
        Assert.Equal("starvation", Body.CauseOfDeath(b).What);
        Assert.NotNull(halfGoneDay);
        Assert.InRange(minutes / 1440.0 - halfGoneDay!.Value, 0.5, 2); // the heart lasts about a day
    }

    /// Body review B4: the cold harms from 95 °F core and kills around 82 °F, within hours. Old: mild
    /// cold killed at 94 °F through the brain, 5.5 hours after crossing 95.
    [Theory]
    [InlineData(-40, 2.5, false)]
    [InlineData(-40, 0, true)]
    [InlineData(-10, 0.5, false)]
    public void Cold_KillsAroundEightyTwo_WithinHoursOfNinetyFive(double airF, double wind, bool asleep)
    {
        var b = Baseline();
        var ctx = Room() with { AirTempF = airF, WindLevel = wind, IsSleeping = asleep, ActivityFactor = asleep ? 0.5 : 1.0 };
        int? crossed = null, minute = 0;
        int minutes = MinutesToDeath(b, ctx, 2 * 1440, body => { if (crossed == null && body.CoreTempF < 95) crossed = minute; minute++; });
        Assert.True(minutes > 0, "lived two days naked in the cold");
        Assert.Equal("the cold", Body.CauseOfDeath(b).What);
        Assert.InRange(b.CoreTempF, 74, 86);
        Assert.InRange((minutes - crossed!.Value) / 60.0, 0.5, 8);
    }

    /// The harm is the core's, not the clock's: held at 90 °F a body lasts days, at 84 °F hours.
    [Theory]
    [InlineData(94, 7 * 24, double.MaxValue)]
    [InlineData(90, 24, 96)]
    [InlineData(86, 3, 10)]
    [InlineData(84, 1, 5)]
    [InlineData(80, 0.25, 1.5)]
    public void Cold_HeldAtACore_KillsInItsTime(double coreF, double minHours, double maxHours)
    {
        var b = Baseline();
        int minutes = MinutesToDeath(b, Room(), 7 * 1440, body => { body.CoreTempF = coreF; body.HydrationMl = 3000; body.CaloriesKcal = 1500; });
        double hours = minutes < 0 ? double.MaxValue : minutes / 60.0;
        Assert.InRange(hours, minHours, maxHours);
    }

    /// Body review A13: the cold's harm rises continuously as the core falls, with no jump (old: from
    /// 0.30 to 0.50 an hour at 87.5 °F), and is nothing at or above 95 °F.
    [Fact]
    public void ColdHarm_IsContinuous_AndRisesAsTheCoreFalls()
    {
        var b = Baseline();
        double previous = 0;
        for (double core = 99; core > 70; core -= 0.01)
        {
            b.CoreTempF = core;
            double rate = Body.LethalRates(b, Species.Human).ColdPerHour;
            if (core >= 95) Assert.Equal(0, rate);
            Assert.True(rate >= previous, $"harm fell from {previous} to {rate} at {core:0.00} °F");
            Assert.True(rate - previous < 0.02, $"harm jumped from {previous} to {rate} at {core:0.00} °F");
            previous = rate;
        }
    }

    /// Body review A8: what LethalRates says a body is suffering is exactly what the next minute charges,
    /// including the minute the core crosses 95 °F. Old: the cold was charged off the core the minute
    /// would end at.
    [Fact]
    public void LethalRates_AreWhatTheNextMinuteCharges()
    {
        var crossing = Baseline();
        crossing.CoreTempF = 95.001;
        var bitter = Room() with { AirTempF = -40, WindLevel = 2.5 };
        crossing.ClothingHeatBufferPct = 0;
        Assert.True(crossing.CoreTempF + Body.Process(crossing, bitter, 1, TestRng()).CoreTempDeltaF < 95);
        Assert.Empty(Body.Process(crossing, bitter, 1, TestRng()).Damage);

        foreach (var (core, ml, kcal, fatKg) in new[] { (93.0, 3000.0, 1500.0, 11.25), (98.6, 0.0, 1500.0, 11.25), (85.0, 0.0, 0.0, 0.0) })
        {
            var b = Baseline();
            (b.CoreTempF, b.HydrationMl, b.CaloriesKcal, b.FatKg) = (core, ml, kcal, fatKg);
            if (fatKg == 0) b.MuscleKg = 0;
            var (cold, thirst, hunger) = Body.LethalRates(b, Species.Human);
            var d = Body.Process(b, Room(), 1, TestRng());
            double Charged(string what) => d.Damage.Where(x => x.By?.What == what).Sum(x => x.Amount) * 60;
            Assert.Equal(cold, Charged("the cold"), 12);
            Assert.Equal(thirst, Charged("thirst"), 12);
            Assert.Equal(hunger, Charged("starvation"), 12);
        }
    }

    [Fact]
    public void FedWateredAndRested_Heals_AndRegrowsBlood()
    {
        var b = Baseline();
        b.CaloriesKcal = 2000;
        b.Regions.Single(r => r.Name == "Left Arm").Muscle.Condition = 0.5;
        b.BloodPct = 0.9;
        var d = Body.Process(b, Room(), 60, TestRng());
        Assert.True(d.HealAmount > 0);
        Assert.True(d.BloodHealedPct > 0);

        b.EnergyMinutes = 100; // exhausted bodies do not heal
        Assert.Equal(0, Body.Process(b, Room(), 60, TestRng()).HealAmount);
    }

    /// Body review A1. Sleep heals faster than waking rest, but through the same gates: fed, watered and
    /// warm. Old (pinned here only as "sleep heals faster"): sleep healed on a channel of its own that
    /// checked nothing, organs included, so a cold, dry or starving sleeper mended anyway.
    [Fact]
    public void SleepHeals_FasterThanWaking_ButOnlyFedWateredAndWarm()
    {
        var asleep = Baseline();
        var awake = Baseline();
        asleep.Regions[3].Skin.Condition = awake.Regions[3].Skin.Condition = 0.5;
        Live(asleep, Asleep(), 60);
        Live(awake, Room(), 60);
        Assert.True(asleep.Regions[3].Skin.Condition > awake.Regions[3].Skin.Condition);

        foreach (var (why, spoil) in new (string, Action<BodyState>)[]
        {
            ("cold", b => b.CoreTempF = 93),
            ("dry", b => b.HydrationMl = 100),
            ("hungry", b => b.CaloriesKcal = 100),
        })
        {
            var b = Baseline();
            b.Regions[3].Skin.Condition = 0.5;
            spoil(b);
            Assert.Equal(0, Body.Process(b, Asleep(), 60, TestRng()).HealAmount);
        }
    }

    /// Body review A1: no healing outpaces the harm of the cold, thirst or hunger, asleep or awake. Old: a
    /// naked sleeper at -40 °F in a gale was alive after 24 hours at a core of -3 °F, brain whole.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoHealing_OutpacesTheColdThirstOrHunger(bool asleep)
    {
        var naked = Baseline();
        var bitter = Room() with { AirTempF = -40, WindLevel = 2.5, IsSleeping = asleep, ActivityFactor = asleep ? 0.5 : 1.0 };
        Assert.InRange(MinutesToDeath(naked, bitter, 24 * 60), 1, 6 * 60);

        var ctx = Room() with { IsSleeping = asleep, ActivityFactor = asleep ? 0.5 : 1.0 };
        foreach (var (why, spoil) in new (string, Action<BodyState>)[]
        {
            ("cold", b => b.CoreTempF = 90),
            ("dry", b => b.HydrationMl = 0),
            ("starving", b => { b.CaloriesKcal = 0; b.FatKg = 0; b.MuscleKg = 0; }),
        })
        {
            var b = Baseline();
            b.Regions[3].Skin.Condition = 0.5;
            var rng = TestRng();
            double vital() => b.Regions.SelectMany(r => r.Organs).Where(o => o.Name is "Heart" or "Brain").Sum(o => o.Condition);
            double previous = vital();
            for (int m = 0; m < 6 * 60 && Body.IsAlive(b); m++)
            {
                spoil(b);
                Body.Update(b, ctx, rng);
                Assert.True(vital() <= previous + 1e-12, $"{why}: the heart and brain mended at minute {m}");
                previous = vital();
            }
            Assert.True(vital() < 2, $"{why}: nothing harmed");
        }
    }

    /// Body review A9: food counts once in healing, tissue and blood alike. Old: tissue healed by
    /// nutrition squared.
    [Fact]
    public void Healing_IsLinearInFood()
    {
        BodyState Hurt(double kcal)
        {
            var b = Baseline();
            b.CaloriesKcal = kcal;
            b.Regions[3].Skin.Condition = 0.5;
            b.BloodPct = 0.9;
            return b;
        }
        // Nutrition is the store as the step leaves it.
        var lean = Body.Process(Hurt(500), Room(), 60, TestRng());
        var fed = Body.Process(Hurt(1000), Room(), 60, TestRng());
        double ratio = (1000 + fed.CaloriesDeltaKcal) / (500 + lean.CaloriesDeltaKcal);
        Assert.True(lean.HealAmount > 0 && lean.BloodHealedPct > 0);
        Assert.Equal(ratio * lean.HealAmount, fed.HealAmount, 12);
        Assert.Equal(ratio * lean.BloodHealedPct, fed.BloodHealedPct, 12);
    }

    /// Body review A7: once a person has mended, whoever hurt them no longer counts, and a death with no
    /// mortal wound is never theirs. Old: a person bitten once and frozen days later was killed by the
    /// wolf pack.
    [Fact]
    public void APersonWhoMends_ForgetsWhoHurtThem_AndFreezesOfTheCold()
    {
        var wolf = new Cause(new AgentId(999), "a wolf pack");
        var mended = Baseline();
        mended.WoundedBy = wolf;
        mended.Regions[3].Skin.Condition = 0.99;
        Live(mended, Asleep(), 60);
        Assert.Null(mended.WoundedBy);

        var bitten = Baseline();
        bitten.WoundedBy = wolf;
        var leg = bitten.Regions.Single(r => r.Name == "Left Leg");
        leg.Skin.Condition = leg.Muscle.Condition = leg.Bone.Condition = 0; // a lost leg, not bleeding
        Assert.True(MinutesToDeath(bitten, Room() with { AirTempF = -40, WindLevel = 2.5 }, 24 * 60) > 0);
        Assert.Equal(wolf, bitten.WoundedBy);
        Assert.Equal("the cold", Body.CauseOfDeath(bitten).What);
    }

    /// A wound someone gave bleeds in their name; an unnamed bleed is blood loss.
    [Fact]
    public void BleedingOut_NamesWhoeverGaveTheWound()
    {
        var spear = new Cause(new AgentId(3), "a person's stone spear");
        var named = Baseline();
        named.WoundedBy = spear;
        Effects.Add(named, EffectKind.Bleeding, 1.0);
        Assert.True(MinutesToDeath(named, Room(), 240) > 0);
        Assert.Equal(spear, Body.CauseOfDeath(named));

        var unnamed = Baseline();
        Effects.Add(unnamed, EffectKind.Bleeding, 1.0);
        Assert.True(MinutesToDeath(unnamed, Room(), 240) > 0);
        Assert.Equal("blood loss", Body.CauseOfDeath(unnamed).What);
    }

    [Fact]
    public void Bleeding_DrainsBloodUntilDeath()
    {
        var b = Baseline();
        Effects.Add(b, EffectKind.Bleeding, 1.0); // 3000 ml/hr
        var rng = TestRng();
        int minutes = 0;
        while (Body.IsAlive(b) && minutes < 240) { Body.Update(b, Room(), rng); minutes++; }
        Assert.False(Body.IsAlive(b));
        Assert.InRange(minutes, 45, 70); // 2500ml at ~50ml/min, slowing as it decays
        Assert.True(Effects.Has(b, EffectKind.Bloody));
    }

    // ---- temperature ----

    [Fact]
    public void Cold_CoolsYou_AndInsulationSlowsIt()
    {
        var bare = Body.ProcessTemperature(Baseline(), Room() with { AirTempF = 32 }, 60);
        var dressed = Body.ProcessTemperature(Baseline(), Room() with { AirTempF = 32, ClothingClo = 4.5 }, 60);
        Assert.True(bare.CoreTempDeltaF < 0);
        Assert.True(Math.Abs(dressed.CoreTempDeltaF) < Math.Abs(bare.CoreTempDeltaF));
    }

    [Fact]
    public void Heat_MakesYouSweat()
    {
        var b = Baseline();
        Live(b, Room() with { AirTempF = 110 }, 60);
        Assert.True(b.CoreTempF > Body.SweatingThresholdF, $"Core {b.CoreTempF:F2}F");
        Assert.True(Effects.Has(b, EffectKind.Sweating));
    }

    [Fact]
    public void BitterCold_Naked_ShiversThenHypothermiaThenFrostbite()
    {
        var b = Baseline();
        var ctx = Room() with { AirTempF = -40 };
        Live(b, ctx, 30);
        Assert.True(Effects.Has(b, EffectKind.Shivering), $"Core {b.CoreTempF:F2}F");
        Live(b, ctx, 90);
        Assert.True(Effects.Has(b, EffectKind.Hypothermia), $"Core {b.CoreTempF:F2}F");
        Live(b, ctx, 120);
        Assert.True(b.CoreTempF < Body.SevereHypothermiaThresholdF, $"Core {b.CoreTempF:F2}F");
        Assert.Single(b.Effects, e => e.Kind == EffectKind.Frostbite);
    }

    [Fact]
    public void Shivering_StartsWhereConstrictionRunsOut()
    {
        var b = Baseline();
        b.CoreTempF = 96.5; // below 97, above the 95 hypothermia line
        Live(b, Room(), 1);
        Assert.True(Effects.Has(b, EffectKind.Shivering));
        Assert.False(Effects.Has(b, EffectKind.Hypothermia));
    }

    /// 1 clo is defined as comfortable at rest at 21C. If this drifts, every garment silently means something else.
    [Theory]
    [InlineData(1.0, 70.0)]
    [InlineData(2.0, 58.0)]
    [InlineData(4.0, 34.0)]
    public void ComfortTemperature_MatchesTheDefinitionOfClo(double clo, double expectedF)
    {
        double comfort = Body.ComfortTemperatureF(Baseline(), StillAir(clo));
        Assert.True(Math.Abs(comfort - expectedF) <= 8.0, $"{clo} clo comfortable at {comfort:F1}F, expected ~{expectedF}F");
    }

    /// Fire is infrastructure: no craftable clothing lets you rest through an Ice Age night.
    [Fact]
    public void NoCraftableClothing_MakesRestingComfortableBelowZeroF()
    {
        Assert.True(Body.ComfortTemperatureF(Baseline(), StillAir(4.5)) > 0);
    }

    [Fact]
    public void Insulation_HasDiminishingReturns_AndNeverBlocksEverything()
    {
        var b = Baseline();
        double r0 = Body.TotalThermalResistance(b, StillAir(0)), r2 = Body.TotalThermalResistance(b, StillAir(2));
        double r4 = Body.TotalThermalResistance(b, StillAir(4)), r8 = Body.TotalThermalResistance(b, StillAir(8));
        Assert.True(1 / r2 - 1 / r4 < 1 / r0 - 1 / r2);
        Assert.True(1 / r4 - 1 / r8 < 1 / r2 - 1 / r4);
        Assert.True(1 / r8 > 0);
    }

    /// Regression: the old 0.95 insulation clamp cooked people in the best gear to 111.9F.
    [Fact]
    public void ExtremeInsulation_DoesNotCookYou()
    {
        var b = Baseline();
        var ctx = new SurvivalContext { ClothingClo = 12, AirTempF = 40, ActivityFactor = 1.0 };
        for (int i = 0; i < 600; i++) b.CoreTempF += Body.Process(b, ctx, 1, TestRng()).CoreTempDeltaF;
        Assert.True(b.CoreTempF < 104, $"Core {b.CoreTempF:F1}F");
    }

    [Fact]
    public void SkinTemperature_AtSetpoint_IsTheOldFixedGradient()
    {
        Assert.Equal(98.6 - 8.4, Body.SkinTemperatureF(98.6), 3);
    }

    [Fact]
    public void Vasomotor_MakesTheHeatBalanceSelfCorrecting()
    {
        var ctx = StillAir(2.0) with { AirTempF = 40 };
        double previous = double.MaxValue;
        foreach (double coreF in new[] { 97.0, 97.5, 98.0, 98.6, 99.0 })
        {
            var b = Baseline();
            b.CoreTempF = coreF;
            double rate = Body.TemperatureChangeFPerHour(b, ctx);
            Assert.True(rate < previous, $"At {coreF}F gaining {rate:F2}F/hr; a warmer body must shed faster");
            previous = rate;
        }
    }

    [Fact]
    public void MildOverheating_ShedsHeatWithBlood_NotWater()
    {
        var ctx = StillAir(2.0) with { AirTempF = 40 };
        Assert.True(Body.SkinTemperatureF(98.95) > Body.SkinTemperatureF(98.6) + 3);
        var warm = Baseline();
        warm.CoreTempF = 98.95;
        Assert.Equal(0, Body.ProcessTemperature(warm, ctx, 60).HydrationDeltaMl);
    }

    [Fact]
    public void RealOverheating_SpendsWater()
    {
        var hot = Baseline();
        hot.CoreTempF = 101;
        Assert.True(Body.ProcessTemperature(hot, StillAir(2.0) with { AirTempF = 40 }, 60).HydrationDeltaMl < -100);
    }

    [Fact]
    public void ClothingBuffer_DoesNotHoldAnOverheatedBodyUp()
    {
        var hot = Baseline();
        hot.CoreTempF = 100.5;
        hot.ClothingHeatBufferPct = 0.5;
        var d = Body.ProcessTemperature(hot, StillAir(2.0) with { AirTempF = 40, ClothingWeightKg = 3 }, 60);
        Assert.True(d.CoreTempDeltaF < 0);
        Assert.True(d.ClothingHeatBufferDeltaPct >= 0);
    }

    [Fact]
    public void ClothingBuffer_FireFillIsNotOverwrittenByCooling()
    {
        var b = Baseline();
        b.CoreTempF = 98.0;
        b.ClothingHeatBufferPct = 0.5;
        var cold = StillAir(0.5) with { AirTempF = 0, ClothingWeightKg = 3 };
        double away = Body.ProcessTemperature(b, cold, 10).ClothingHeatBufferDeltaPct;
        double byFire = Body.ProcessTemperature(b, cold with { FireBonusF = 20 }, 10).ClothingHeatBufferDeltaPct;
        Assert.True(byFire > away);
    }

    [Fact]
    public void ClothingBuffer_DelaysChilling()
    {
        var buffered = Baseline();
        var empty = Baseline();
        empty.ClothingHeatBufferPct = 0;
        buffered.ClothingHeatBufferPct = 1;
        var ctx = StillAir(1.0) with { AirTempF = 10, ClothingWeightKg = 3 };
        Assert.True(Body.ProcessTemperature(buffered, ctx, 10).CoreTempDeltaF > Body.ProcessTemperature(empty, ctx, 10).CoreTempDeltaF);
    }

    // ---- fire ----

    [Fact]
    public void Fire_Warms_UntilYouAreHotEnoughToSweat()
    {
        var ctx = Room() with { AirTempF = 0, FireBonusF = 40 };
        var cool = Baseline();
        Assert.True(Body.TemperatureChangeFPerHour(cool, ctx) > Body.TemperatureChangeFPerHour(cool, ctx with { FireBonusF = 0 }));

        var hot = Baseline();
        hot.CoreTempF = Body.SweatingThresholdF + 0.1;
        Assert.Equal(Body.TemperatureChangeFPerHour(hot, ctx with { FireBonusF = 0 }), Body.TemperatureChangeFPerHour(hot, ctx), 9);
    }

    /// A sleeper shifts back from a fire before they sweat; gating at hyperthermia instead cost litres a night.
    [Fact]
    public void ANightBesideABigFire_DoesNotSweatAwayYourWater()
    {
        var b = Baseline();
        var ctx = Asleep() with { AirTempF = 59, ClothingClo = 2.0, ClothingWeightKg = 3, FireBonusF = 40 };
        double peakF = 0;
        var rng = TestRng();
        for (int i = 0; i < 8 * 60; i++)
        {
            Body.Update(b, ctx, rng);
            peakF = Math.Max(peakF, b.CoreTempF);
        }
        Assert.True(peakF < Body.SweatingThresholdF + 0.25, $"Peaked at {peakF:F2}F");
        Assert.False(Effects.Has(b, EffectKind.Hyperthermia));
    }

    // ---- wetness ----

    [Fact]
    public void SixHoursInRain_MakesYouWet()
    {
        var b = Baseline();
        Live(b, new SurvivalContext { AirTempF = 40, Precipitation = Precipitation.Rain, PrecipitationPct = 1.0, WindLevel = 0.2 }, 360);
        Assert.True(Effects.Severity(b, EffectKind.Wet) > 0.5);
    }

    [Fact]
    public void Wetness_IsTheSumOfItsTerms()
    {
        var b = Baseline();
        Effects.Set(b, EffectKind.Wet, 0.2);
        var ctx = new SurvivalContext
        {
            AirTempF = 45,
            Precipitation = Precipitation.Rain,
            PrecipitationPct = 1.0,
            GroundWettingPctPerMinute = 0.004,
            WindLevel = 0.2,
        };
        var wet = Body.Process(b, ctx, 1, TestRng()).Effects.Single(e => e.Kind == EffectKind.Wet);
        double drying = (45 - 32) * 10.0 / (2000 * 0.58) / 60; // windless in rain
        Assert.Equal(0.2 + 0.01 + 0.004 - drying, wet.Severity, 9);
    }

    [Fact]
    public void WetClothes_FreezeBelowZero_OnlyAFireDriesThem()
    {
        var b = Baseline();
        Effects.Set(b, EffectKind.Wet, 0.5);
        Live(b, new SurvivalContext { AirTempF = 20, WindLevel = 0.5 }, 60);
        Assert.Equal(0.5, Effects.Severity(b, EffectKind.Wet), 9);

        Live(b, new SurvivalContext { AirTempF = 20, FireBonusF = 20 }, 60);
        Assert.True(Effects.Severity(b, EffectKind.Wet) < 0.5);
    }

    [Fact]
    public void Drying_ToZero_RemovesWet()
    {
        var b = Baseline();
        Effects.Set(b, EffectKind.Wet, 0.001);
        Live(b, Room(), 1);
        Assert.False(Effects.Has(b, EffectKind.Wet));
    }

    [Fact]
    public void Wetness_RobsClothingOfInsulation()
    {
        var dry = Baseline();
        var soaked = Baseline();
        Effects.Set(soaked, EffectKind.Wet, 1.0);
        Assert.True(Body.TotalThermalResistance(soaked, StillAir(2)) < Body.TotalThermalResistance(dry, StillAir(2)));
    }

    // ---- effect rules ----

    [Fact]
    public void Effects_AddKeepsTheWorse_BleedingStacks()
    {
        var b = Baseline();
        Effects.Add(b, EffectKind.Pain, 0.5);
        Effects.Add(b, EffectKind.Pain, 0.3);
        Assert.Equal(0.5, Effects.Severity(b, EffectKind.Pain));
        Effects.Add(b, EffectKind.Bleeding, 0.4);
        Effects.Add(b, EffectKind.Bleeding, 0.4);
        Assert.Equal(0.8, Effects.Severity(b, EffectKind.Bleeding), 9);
        Assert.Equal(2, b.Effects.Count);
    }

    [Fact]
    public void Effects_DecayAndClear_ButTreatmentOnesStopAtAFloor()
    {
        var b = Baseline();
        Effects.Add(b, EffectKind.Pain, 0.15);         // -0.15/hr
        Effects.Add(b, EffectKind.Frostbite, 0.06);    // always needs treatment
        Effects.Decay(b, 60);
        Assert.False(Effects.Has(b, EffectKind.Pain));
        Effects.Decay(b, 600);
        Assert.Equal(0.05, Effects.Severity(b, EffectKind.Frostbite), 9);
    }

    /// Body review A5 and decisions/2026-11-21-no-effect-needs-a-missing-treatment.md: no treatment
    /// exists, so severe shock fades away on its own. Old (pinned here): shock above 0.5 needed treatment
    /// and stopped at a 5% floor for good.
    [Fact]
    public void Shock_FadesAwayUntreated_HoweverSevere()
    {
        var b = Baseline();
        Effects.Add(b, EffectKind.Shock, 0.7);
        Assert.False(b.Effects.Single().RequiresTreatment);
        Effects.Decay(b, 15 * 60);
        Assert.False(Effects.Has(b, EffectKind.Shock));
    }

    /// Body review A5 and the decision record: blood on you dries away on its own over a few hours, faster
    /// by a fire. Old: it faded at 0.02 an hour to a 5% floor only a missing washing cleared, so one
    /// butchering made a person a scent source for the rest of the run.
    [Fact]
    public void Bloody_DriesAwayOnItsOwn_FasterByAFire()
    {
        BodyState Butchered() { var b = Baseline(); Effects.Set(b, EffectKind.Bloody, 0.5); return b; }
        var open = Butchered();
        var byFire = Butchered();
        Live(open, Room() with { AirTempF = 20 }, 3 * 60);
        Live(byFire, Room() with { AirTempF = 20, FireBonusF = 20 }, 3 * 60);
        Assert.True(Effects.Has(open, EffectKind.Bloody), "three hours in the cold and dry already");
        Assert.False(Effects.Has(byFire, EffectKind.Bloody), "three hours by a fire and still bloody");
        Live(open, Room() with { AirTempF = 20 }, 3 * 60);
        Assert.False(Effects.Has(open, EffectKind.Bloody), "six hours and still bloody");
    }

    /// Body review A6: a level set this minute reads as computed: at 96.9 °F, Shivering is (97 - 96.9)/5.
    /// Old: it was set, then faded a minute, and so absent until 96.83 °F.
    [Fact]
    public void ALevelSetThisMinute_ReadsAsTheStepComputedIt()
    {
        var b = Baseline();
        b.CoreTempF = 96.9;
        Live(b, Room(), 1);
        Assert.Equal((97 - 96.9) / 5, Effects.Severity(b, EffectKind.Shivering), 9);
        var c = Baseline();
        c.CaloriesKcal = 300;
        Live(c, Room(), 1);
        double pct = (300 - Body.MetabolismKcalPerDay(c, 1.0) / 1440) / Body.MaxCaloriesKcal;
        // The pang, below 30% of the store.
        Assert.Equal((0.3 - pct) / 0.3, Deprivations.Severity(c, Deprivation.Hungry), 3);
    }

    [Fact]
    public void EveryEffectKind_HasADefinition()
    {
        var b = Baseline();
        foreach (var kind in Enum.GetValues<EffectKind>()) Effects.Add(b, kind, 0.5);
        Effects.Decay(b, 1);
        Body.Vitality(b, Species.Human); // reads every faculty, so every row's capacity deltas
    }
}
