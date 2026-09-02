using text_survival.Bodies;
using text_survival.Survival;

namespace text_survival.Tests.Survival;

/// <summary>
/// Anchors the thermal model to the real definition of its unit, so gear values can be
/// checked against reality instead of invented. 1 clo is defined as the insulation that
/// keeps a resting person comfortable at 21C - if this drifts, every garment in the game
/// silently means something different.
/// </summary>
public class WarmthTests
{
    private static SurvivalContext StillAir(double clo) => new()
    {
        ClothingClo = clo,
        WindSpeedLevel = 0,
        ActivityLevel = 1.0,
        LocationTemperature = 60,
    };

    /// <summary>
    /// The textbook anchor, and the whole point of using a real unit: clo values can be
    /// checked against reality instead of invented. If this drifts, either the thermal model
    /// or the body's metabolism has moved, and every garment silently means something else.
    /// </summary>
    /// <remarks>
    /// The model reads a few degrees colder than the published figures because those assume
    /// a nude reference with no subcutaneous fat, while this body carries some - which is
    /// itself insulation. Hence the tolerance.
    /// </remarks>
    [Theory]
    [InlineData(1.0, 70.0)]   // 1 clo: comfortable at 21C/70F - the definition of the unit
    [InlineData(2.0, 58.0)]   // heavy winter outfit
    [InlineData(4.0, 34.0)]   // arctic expedition clothing, comfortable near freezing
    public void ComfortTemperature_MatchesTheDefinitionOfClo(double clo, double expectedF)
    {
        var body = new Body(Body.BaselineHumanStats);
        double comfort = SurvivalProcessor.ComfortTemperatureF(body, StillAir(clo));

        Assert.True(Math.Abs(comfort - expectedF) <= 8.0,
            $"{clo} clo should be comfortable near {expectedF}F, got {comfort:F1}F.");
    }

    /// <summary>
    /// Metabolism is the body's only heat source, so a wrong BMR silently moves every
    /// temperature in the game. It is also what makes the clo anchor above meaningful.
    /// </summary>
    [Fact]
    public void BasalMetabolism_MatchesARealAdult()
    {
        var body = new Body(Body.BaselineHumanStats);
        double bmr = SurvivalProcessor.GetCurrentMetabolism(body, 1.0);

        Assert.True(bmr > 1500 && bmr < 2000,
            $"A {body.WeightKG:F0}kg adult's BMR should be ~1800 kcal/day, got {bmr:F0}. " +
            "Katch-McArdle takes lean body mass, not muscle mass.");
    }

    /// <summary>
    /// The design-relevant consequence, and the reason fire is infrastructure rather than a
    /// convenience: no clothing the game can make lets you rest through an Ice Age night.
    /// You need a fire, a shelter, or to keep moving.
    /// </summary>
    [Fact]
    public void NoCraftableClothing_MakesRestingComfortableAtIceAgeTemperatures()
    {
        var body = new Body(Body.BaselineHumanStats);
        double bestCraftable = SurvivalProcessor.ComfortTemperatureF(body, StillAir(4.5));

        Assert.True(bestCraftable > 0,
            $"Best craftable clothing is comfortable down to {bestCraftable:F0}F. If that ever " +
            "drops below 0F, clothing alone solves the cold and fire stops mattering.");
    }

    /// <summary>
    /// The property the old model could not have: resistances add in series, so each extra
    /// layer buys less than the one before and nothing ever blocks all heat. The old form
    /// multiplied by (1 - insulation) and needed a clamp at 0.95, where heavy gear piled up
    /// and could no longer vent metabolism at all.
    /// </summary>
    [Fact]
    public void Insulation_HasDiminishingReturns_AndNeverBlocksEverything()
    {
        var body = new Body(Body.BaselineHumanStats);

        double r0 = SurvivalProcessor.TotalThermalResistance(body, StillAir(0));
        double r2 = SurvivalProcessor.TotalThermalResistance(body, StillAir(2));
        double r4 = SurvivalProcessor.TotalThermalResistance(body, StillAir(4));
        double r8 = SurvivalProcessor.TotalThermalResistance(body, StillAir(8));

        // Heat loss is proportional to 1/R, so compare the reductions each step buys.
        double firstStep = 1 / r0 - 1 / r2;
        double secondStep = 1 / r2 - 1 / r4;
        double thirdStep = 1 / r4 - 1 / r8;

        Assert.True(secondStep < firstStep, "The second 2 clo should buy less than the first.");
        Assert.True(thirdStep < secondStep, "Returns should keep diminishing.");
        Assert.True(1 / r8 > 0, "No amount of clothing may block heat loss entirely.");
    }

    /// <summary>
    /// Even extreme insulation must leave a path for metabolic heat. This is the regression
    /// guard for the bug that made the best gear in the game lethal: at the old 0.95 clamp
    /// only 5% of heat could escape, metabolism outran it, and body temperature ran to 111.9F.
    /// </summary>
    [Fact]
    public void ExtremeInsulation_DoesNotCookYou()
    {
        var body = new Body(Body.BaselineHumanStats);
        var context = new SurvivalContext
        {
            ClothingClo = 12,           // far beyond anything craftable
            LocationTemperature = 40,
            ActivityLevel = 1.0,
            WindSpeedLevel = 0,
        };

        // Ten hours of the worst case: fully insulated, no fire, resting.
        for (int i = 0; i < 600; i++)
        {
            var result = SurvivalProcessor.Process(body, context with
            {
                CurrentWetnessPct = 0,
            }, 1);
            body.BodyTemperature += result.StatsDelta.TemperatureDelta;
        }

        Assert.True(body.BodyTemperature < 104,
            $"Ten hours in extreme insulation drove body temperature to {body.BodyTemperature:F1}F. " +
            "Hyperthermia should be survivable-ish, not runaway.");
    }

    /// <summary>
    /// The neutral anchor. A body at its setpoint sits at mid vasomotor tone, and the gap
    /// between core and skin there is 8.4F - the fixed constant the regulated skin replaced.
    /// Everything calibrated against the old model (the clo anchor above, every gear value)
    /// therefore still means what it meant; only a body with something to correct differs.
    /// </summary>
    [Fact]
    public void SkinTemperature_AtTheSetpoint_MatchesTheOldFixedGradient()
    {
        Assert.Equal(98.6 - 8.4, SurvivalProcessor.SkinTemperatureF(98.6), 3);
    }

    /// <summary>
    /// The property a fixed skin gradient could not have: skin temperature is regulated, so
    /// the heat balance is a negative feedback loop. A body running warm opens its skin and
    /// sheds faster; a body running cold shuts it down and holds on. Without this the only
    /// correction available in either direction was an expensive one - sweat or shivering.
    /// </summary>
    [Fact]
    public void Vasomotor_MakesTheHeatBalance_SelfCorrecting()
    {
        var context = StillAir(2.0) with { LocationTemperature = 40 };
        double previousRate = double.MaxValue;

        foreach (double coreF in new[] { 97.0, 97.5, 98.0, 98.6, 99.0 })
        {
            var body = new Body(Body.BaselineHumanStats) { BodyTemperature = coreF };
            double rate = SurvivalProcessor.CalculateTemperatureChangePerHour(body, context);

            Assert.True(rate < previousRate,
                $"At {coreF}F the body gains heat at {rate:F2}F/hr, no slower than the colder " +
                "body below it. A warmer body must shed faster, or nothing pulls it back.");
            previousRate = rate;
        }
    }

    /// <summary>
    /// Order of defences, and the reason this exists: between the setpoint and the sweating
    /// threshold the body sheds heat by moving blood, which is free. Water only gets spent
    /// once that is exhausted. Previously the body had no such move, so it began sweating -
    /// and dehydrating - at the slightest excess heat.
    /// </summary>
    [Fact]
    public void MildOverheating_ShedsHeatWithBlood_WithoutSpendingWater()
    {
        var context = StillAir(2.0) with { LocationTemperature = 40 };

        var neutral = new Body(Body.BaselineHumanStats) { BodyTemperature = 98.6 };
        var warm = new Body(Body.BaselineHumanStats) { BodyTemperature = 98.95 };

        double neutralSkin = SurvivalProcessor.SkinTemperatureF(neutral.BodyTemperature);
        double warmSkin = SurvivalProcessor.SkinTemperatureF(warm.BodyTemperature);

        Assert.True(warmSkin > neutralSkin + 3,
            $"A body 0.35F over its setpoint should have flushed skin, got {warmSkin:F1}F " +
            $"against a neutral {neutralSkin:F1}F.");

        double waterSpent = SurvivalProcessor.ProcessTemperature(warm, context, 60)
            .StatsDelta.HydrationDelta;

        Assert.Equal(0, waterSpent);
    }

    /// <summary>
    /// The other half: once dilation is maxed out, sweating does open - otherwise the body
    /// would have no answer to real heat at all.
    /// </summary>
    [Fact]
    public void RealOverheating_DoesSpendWater()
    {
        var context = StillAir(2.0) with { LocationTemperature = 40 };
        var hot = new Body(Body.BaselineHumanStats) { BodyTemperature = 101.0 };

        double waterSpent = SurvivalProcessor.ProcessTemperature(hot, context, 60)
            .StatsDelta.HydrationDelta;

        Assert.True(waterSpent < -100,
            $"A body at 101F should be sweating hard, but spent only {-waterSpent:F0}ml in an hour.");
    }

    /// <summary>
    /// Area weighting: a chest wrap covers 40% of the body and handwraps 10%, so the same
    /// garment quality has to be worth four times as much on the chest. The old model summed
    /// slot values, which made these identical.
    /// </summary>
    [Fact]
    public void Coverage_IsAreaWeighted_NotSummed()
    {
        var chestOnly = new Inventory();
        chestOnly.Equip(Gear.FurChestWrap());

        var handsOnly = new Inventory();
        handsOnly.Equip(Gear.HideHandwraps());

        Assert.True(chestOnly.ClothingClo > handsOnly.ClothingClo * 2,
            $"Chest {chestOnly.ClothingClo:F2} clo vs hands {handsOnly.ClothingClo:F2} clo - " +
            "covering the torso must count for far more than covering the hands.");

        Assert.Equal(0.4, chestOnly.CoveragePct, 3);
        Assert.Equal(0.1, handsOnly.CoveragePct, 3);
    }
}
