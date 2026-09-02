using text_survival.Bodies;
using text_survival.Survival;

namespace text_survival.Tests.Survival;

/// <summary>
/// Wetness is described as a major survival pressure, but it never worked: the Wet effect
/// stores the wetness, and it was only emitted once severity passed 5%, so it was never
/// created, CurrentWetnessPct read 0 every tick, and accumulation restarted from zero every
/// minute. Nobody in this game had ever got wet. This is the guard.
/// </summary>
public class WetnessTests
{
    [Fact]
    public void StandingInRainForSixHours_MakesYouWet()
    {
        var body = new Body(Body.BaselineHumanStats);
        double wetness = 0;

        // Six hours of steady rain, fully exposed, no waterproofing, minute by minute.
        for (int i = 0; i < 360; i++)
        {
            var ctx = new SurvivalContext
            {
                LocationTemperature = 40,
                IsRaining = true,
                PrecipitationPct = 1.0,
                OverheadCoverLevel = 0,
                WaterproofingLevel = 0,
                WindSpeedLevel = 0.2,
                ActivityLevel = 1.0,
                CurrentWetnessPct = wetness,
            };
            var r = SurvivalProcessor.Process(body, ctx, 1);
            var wet = r.Effects.FirstOrDefault(e => e.EffectKind == "Wet");
            wetness = wet?.Severity ?? 0;
        }

        Assert.True(wetness > 0.5,
            $"After six hours of rain with no shelter and no waterproofing, wetness was {wetness:F3}.");
    }
}
