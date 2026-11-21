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
        Assert.True(Effects.Has(b, EffectKind.Hungry));
        Assert.True(Effects.Has(b, EffectKind.Thirsty));
        Assert.True(Effects.Has(b, EffectKind.Tired));
        Assert.True(Body.GetCapacities(b).Moving < 0.5);
    }

    // ---- starvation, dehydration, regeneration ----

    [Fact]
    public void Starvation_BurnsFatAtSevenThousandSevenHundredKcalPerKg()
    {
        var b = Baseline();
        b.CaloriesKcal = 0;
        double fatBefore = b.FatKg;
        var d = Body.Process(b, Room(), 60, TestRng());
        double burnedKcal = Body.MetabolismKcalPerDay(b, 1.0) / 24;
        Assert.Equal(burnedKcal / Body.KcalPerKgFat, d.FatBurnedKg, 9);
        Assert.Equal(0, d.MuscleBurnedKg);
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

    [Fact]
    public void Dehydration_KillsWithinAnHourOrSo()
    {
        var b = Baseline();
        b.HydrationMl = 0;
        int minutes = 0;
        var rng = TestRng();
        while (Body.IsAlive(b) && minutes < 24 * 60) { Body.Update(b, Room(), rng); minutes++; }
        Assert.False(Body.IsAlive(b));
        // The old comment promised ~5 hours, but 1.0/hr internal damage divided by the brain's
        // toughness of 0.25, accelerating as protection falls, kills in well under an hour.
        Assert.InRange(minutes, 15, 120);
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

    [Fact]
    public void SleepHeals_FasterThanRegenerationAlone()
    {
        var asleep = Baseline();
        var awake = Baseline();
        asleep.Regions[3].Skin.Condition = awake.Regions[3].Skin.Condition = 0.5;
        Live(asleep, Asleep(), 60);
        Live(awake, Room(), 60);
        Assert.True(asleep.Regions[3].Skin.Condition > awake.Regions[3].Skin.Condition);
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
            AirTempF = 45, Precipitation = Precipitation.Rain, PrecipitationPct = 1.0,
            GroundWettingPctPerMinute = 0.004, WindLevel = 0.2,
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

    [Fact]
    public void Shock_NeedsTreatmentOnlyWhenSevere()
    {
        var b = Baseline();
        Effects.Add(b, EffectKind.Shock, 0.3);
        Assert.False(b.Effects.Single().RequiresTreatment);
        var c = Baseline();
        Effects.Add(c, EffectKind.Shock, 0.7);
        Assert.True(c.Effects.Single().RequiresTreatment);
    }

    [Fact]
    public void EveryEffectKind_HasADefinition()
    {
        var b = Baseline();
        foreach (var kind in Enum.GetValues<EffectKind>()) Effects.Add(b, kind, 0.5);
        Effects.Decay(b, 1);
        Effects.CapacityDeltas(b);
    }
}
