using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Survival;
using text_survival.Tests.TestHelpers;

namespace text_survival.Tests.Survival;

public class SleepByFireTests
{
    /// <summary>
    /// A sleeper shifts back from a fire before they sweat. The old gate waited for
    /// Hyperthermia, a degree past where sweating starts, so a night by a good fire cost
    /// litres of water.
    /// </summary>
    [Fact]
    public void ANightBesideABigFire_DoesNotSweatAwayYourWater()
    {
        var ctx = SmallGame.CreateWithStartingEquipment(TerrainType.Forest);
        // Use explicit summer weather instead of relying on a generated world's fronts.
        ctx.Weather.BaseTemperature = 15;
        var here = ctx.CurrentLocation;
        foreach (var existing in here.Features.OfType<HeatSourceFeature>().ToList())
            here.RemoveFeature(existing);
        // Logs cannot ignite from cold. Start kindling before adding the overnight fuel.
        var fire = TestFixtures.CreateTestFire(1, FuelType.Kindling, FirePitType.Stone);
        fire.AddFuel(8, FuelType.PineWood);
        for (int minute = 0; minute < 20; minute++) fire.Update(1);
        Assert.True(fire.IsActive);
        here.AddFeature(fire);

        var comfortable = SurvivalContext.GetSurvivalContext(
            ctx.player, ctx.Inventory, ActivityType.Sleeping, ctx.GetTimeOfDay());
        Assert.True(comfortable.FireProximityBonus > 0, "the sleeper should receive radiant heat while comfortable");
        double startingTemperature = ctx.player.Body.BodyTemperature;
        ctx.player.Body.BodyTemperature = SurvivalProcessor.SweatingThreshold + 0.1;
        var hot = SurvivalContext.GetSurvivalContext(
            ctx.player, ctx.Inventory, ActivityType.Sleeping, ctx.GetTimeOfDay());
        Assert.Equal(0, hot.FireProximityBonus); // The old hyperthermia gate still heated this sleeper.
        ctx.player.Body.BodyTemperature = startingTemperature;

        double peakF = 0;
        for (int i = 0; i < 8 * 60; i++)
        {
            if (fire.TotalMassKg < 4) fire.AddFuel(4, FuelType.PineWood);
            Assert.True(fire.IsActive, "the overnight regression requires a burning fire");
            ctx.UpdateWithoutEvents(1, ActivityType.Sleeping);
            peakF = Math.Max(peakF, ctx.player.Body.BodyTemperature);
        }

        Assert.True(peakF < SurvivalProcessor.SweatingThreshold + 0.25,
            $"body peaked at {peakF:F2}F by the fire");
        Assert.False(ctx.player.EffectRegistry.HasEffect("Hyperthermia"));
    }
}
