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

    /// <summary>
    /// The breakdown has to be the arithmetic that produced the severity, not a retelling of
    /// it: a tooltip that can disagree with the number it explains is worse than no tooltip,
    /// because it is believed. So the terms sum to the change the player actually gets.
    /// </summary>
    [Fact]
    public void WetContributions_SumToTheSeverityChange()
    {
        var body = new Body(Body.BaselineHumanStats);
        var ctx = new SurvivalContext
        {
            LocationTemperature = 45,      // above freezing, so drying is a live term
            IsRaining = true,
            PrecipitationPct = 1.0,
            OverheadCoverLevel = 0,
            GroundContactWettingPct = 0.004,
            GroundContactProtectionLevel = 0,
            CurrentWetnessPct = 0.2,
            WindSpeedLevel = 0.2,
        };

        var result = SurvivalProcessor.Process(body, ctx, 1);
        var wet = result.Effects.Single(e => e.EffectKind == "Wet");

        Assert.Contains("Precipitation", wet.Contributions.Keys);
        Assert.Contains("Ground", wet.Contributions.Keys);
        Assert.Contains(wet.Contributions.Keys, k => k.StartsWith("Drying"));

        double netPerMinute = wet.Contributions.Values.Sum() / 60.0;
        Assert.Equal(ctx.CurrentWetnessPct + netPerMinute, wet.Severity, 6);
    }

    /// <summary>
    /// The breakdown travels with the severity through the registry, or the tooltip explains
    /// the weather from whenever the effect was first created.
    /// </summary>
    [Fact]
    public void WetContributions_SurviveTheRegistryMerge()
    {
        var registry = new text_survival.Effects.EffectRegistry();
        var first = text_survival.Effects.EffectFactory.Wet(0.1);
        first.Contributions["Precipitation"] = 0.6;
        registry.SetEffectSeverity(first);

        var second = text_survival.Effects.EffectFactory.Wet(0.2);
        second.Contributions["Ground"] = 2.4;
        registry.SetEffectSeverity(second);

        var live = registry.GetAll().Single(e => e.EffectKind == "Wet");
        Assert.Equal(0.2, live.Severity, 6);
        Assert.Equal(new[] { "Ground" }, live.Contributions.Keys);
    }
}
