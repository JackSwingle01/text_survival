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
    /// Each layer must buy real cold tolerance, and more of it must always be warmer.
    /// </summary>
    /// <remarks>
    /// Absolute comfort temperatures currently run ~10-25F warmer than the textbook figures
    /// for these clo values (1 clo should be comfortable at 21C/70F; here it is ~78F). That
    /// is not the thermal model - it is that this body produces about half a real human's
    /// heat. GetCurrentMetabolism uses the Katch-McArdle formula, 370 + 21.6 x lean body
    /// mass, but passes MuscleKG (22.5) where the formula expects lean body mass (63.8, i.e.
    /// everything that is not fat), giving 925 kcal/day against a real adult's ~1800. Less
    /// heat produced means comfort at a warmer air temperature for the same clothing.
    /// Correcting it doubles calorie burn, so it is a food-balance decision, not a thermal
    /// one, and it is deliberately not bundled here. When it is fixed, these numbers should
    /// converge on the textbook ones and CloPerInsulationPoint should be re-checked.
    /// </remarks>
    [Fact]
    public void MoreClothing_IsAlwaysWarmer_AndBuysRealTolerance()
    {
        var body = new Body(Body.BaselineHumanStats);

        double nude = SurvivalProcessor.ComfortTemperatureF(body, StillAir(0));
        double one = SurvivalProcessor.ComfortTemperatureF(body, StillAir(1));
        double four = SurvivalProcessor.ComfortTemperatureF(body, StillAir(4));

        Assert.True(one < nude && four < one, $"Comfort must fall with clothing: {nude:F0} / {one:F0} / {four:F0}F.");
        Assert.True(nude - four > 15,
            $"Four clo should buy well over 15F of tolerance, bought {nude - four:F1}F.");
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
