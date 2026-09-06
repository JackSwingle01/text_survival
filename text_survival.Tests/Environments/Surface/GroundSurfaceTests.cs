using text_survival.Environments.Surface;

namespace text_survival.Tests.Environments.Surface;

/// <summary>
/// Physical claims: what the surface conserves, what it never produces, and what a player
/// would notice.
/// </summary>
public class GroundSurfaceTests
{
    private const double Tolerance = 1e-6;

    private static GroundSurface Loam(double frozen = 0) =>
        new(SubstrateProfile.Loam, frozen);

    private static SurfaceWeather Snowing(double temperatureF = 15, double weMPerMinute = 5e-3 / 60) =>
        new(temperatureF, 0.2, 0, weMPerMinute, 0);

    private static SurfaceWeather Raining(double temperatureF = 40, double mPerMinute = 10e-3 / 60) =>
        new(temperatureF, 0.1, 0, 0, mPerMinute);

    /// <summary>Water in equals water held plus water gone.</summary>
    private static void AssertConserved(GroundSurface surface)
    {
        double accounted = surface.StoredWaterM
            + surface.WaterDrainedM + surface.WaterEvaporatedM + surface.WaterOverflowedM;
        Assert.Equal(surface.WaterAddedM, accounted, 6);
    }

    private static void AssertSane(GroundSurface surface)
    {
        Assert.True(surface.SnowDepthM >= 0);
        Assert.True(surface.StandingWaterDepthM >= 0);
        Assert.InRange(surface.WetnessPct, 0, 1);
        Assert.True(double.IsFinite(surface.SurfaceHeightM));
        Assert.True(double.IsFinite(surface.TraversalFactor));
        Assert.InRange(surface.HazardDelta, 0, 0.5);
        foreach (var layer in surface.Layers)
        {
            Assert.True(layer.ThicknessM >= 0);
            Assert.InRange(layer.CompactionPct, 0, 1);
            Assert.InRange(layer.WaterSaturationPct, 0, 1);
            Assert.InRange(layer.FrozenFractionPct, 0, 1);
        }
    }

    [Fact]
    public void Snowfall_AccumulatesAndDeepensTheGround()
    {
        var surface = Loam();
        surface.Advance(60 * 6, Snowing());

        // 3 cm of water-equivalent falls as about 30 cm of snow, less settling.
        Assert.InRange(surface.SnowDepthM, 0.18, 0.31);
        Assert.True(surface.SurfaceHeightM > 0.15);
        AssertConserved(surface);
        AssertSane(surface);
    }

    [Fact]
    public void WhiteoutWithoutSnowfall_LeavesNothingOnTheGround()
    {
        var surface = Loam();
        surface.Advance(60 * 12, new SurfaceWeather(10, 1.0, 0, 0, 0));

        Assert.Equal(0, surface.SnowDepthM, 9);
        AssertConserved(surface);
    }

    [Fact]
    public void Compaction_ShrinksSnowWithoutLosingAnyOfIt()
    {
        var surface = Loam();
        surface.Advance(60 * 4, Snowing());

        double before = surface.SnowDepthM;
        double water = surface.StoredWaterM;

        surface.Compact(0.6);

        Assert.True(surface.SnowDepthM < before * 0.6, "packed snow should be markedly shallower");
        Assert.Equal(water, surface.StoredWaterM, 6);
        AssertConserved(surface);
        AssertSane(surface);
    }

    [Fact]
    public void Thaw_TurnsSnowIntoWetGroundAndThenPuddles()
    {
        var surface = Loam();
        surface.Advance(60 * 10, Snowing());
        double snow = surface.SnowDepthM;
        double dryness = surface.WetnessPct;

        surface.Advance(60 * 48, new SurfaceWeather(50, 0.2, 0.6, 0, 0));

        Assert.True(surface.SnowDepthM < snow, "snow should melt back");
        Assert.True(surface.WetnessPct > dryness, "meltwater should soak the ground");
        AssertConserved(surface);
        AssertSane(surface);
    }

    [Fact]
    public void ColdNight_GlazesStandingWaterWithoutDeletingIt()
    {
        // Peat, so the puddle is still there in the morning.
        var surface = new GroundSurface(SubstrateProfile.Peat, 0);
        surface.AddWater(0.05);

        surface.Advance(60 * 10, new SurfaceWeather(15, 0.1, 0, 0, 0));

        Assert.True(surface.HazardDelta > 0.1, "an iced-over surface should be slippery");
        Assert.True(surface.StandingWaterDepthM > 0.01, "the water is still there, it is just ice");
        Assert.True(surface.StandingLiquidDepthM < surface.StandingWaterDepthM,
            "a night of hard frost should have skinned it over");
        AssertConserved(surface);

        double glazed = surface.HazardDelta;
        surface.Advance(60 * 24, new SurfaceWeather(45, 0.1, 0.3, 0, 0));
        Assert.True(surface.HazardDelta < glazed * 0.5, "the ice should melt back to water");
        AssertConserved(surface);
    }

    [Fact]
    public void Peat_PondsAtOnceWhilePermeableGroundSoaksItUp()
    {
        var marsh = new GroundSurface(SubstrateProfile.Peat, 0);
        var loam = new GroundSurface(SubstrateProfile.Loam, 0);

        marsh.Advance(120, Raining());
        loam.Advance(120, Raining());

        Assert.True(marsh.StandingWaterDepthM > 0.005,
            $"rain should stand on peat, got {marsh.StandingWaterDepthM}");
        Assert.True(loam.StandingWaterDepthM < marsh.StandingWaterDepthM * 0.2,
            $"loam should drink it, got {loam.StandingWaterDepthM}");

        AssertConserved(marsh);
        AssertConserved(loam);
    }

    [Fact]
    public void InfiltrationFillsTheGroundBeforeAnythingOverflows()
    {
        var surface = Loam();
        surface.Advance(60 * 24, Raining());

        Assert.True(surface.WetnessPct > 0.5, "a day of rain should wet permeable ground through");
        AssertConserved(surface);
        AssertSane(surface);
    }

    [Fact]
    public void StandingLiquidNeverExceedsAFoot_AndTheOverflowIsAccountedFor()
    {
        var marsh = new GroundSurface(SubstrateProfile.Peat, 0);
        marsh.AddWater(2.0);
        marsh.Advance(30, new SurfaceWeather(40, 0, 0, 0, 0));

        Assert.True(marsh.StandingLiquidDepthM <= 0.3048 + Tolerance,
            $"got {marsh.StandingLiquidDepthM}");
        Assert.True(marsh.WaterOverflowedM > 1.0, "the rest must be recorded leaving, not vanish");
        AssertConserved(marsh);
    }

    [Fact]
    public void FrozenGroundCannotAbsorbAnExtraFullCapacity()
    {
        var surface = Loam(frozen: 1.0);
        double capacityBefore = surface.WetnessPct;

        surface.Advance(60 * 6, Raining(temperatureF: 20));

        Assert.InRange(surface.WetnessPct, capacityBefore, 1.0);
        AssertConserved(surface);
        AssertSane(surface);
    }

    [Fact]
    public void RepeatedDepositsAndPhaseCyclesKeepTheStackBounded()
    {
        var surface = Loam();

        for (int cycle = 0; cycle < 40; cycle++)
        {
            surface.Advance(120, Snowing(temperatureF: 12));
            surface.Advance(120, new SurfaceWeather(42, 0.3, 0.5, 0, 0));
            surface.Advance(60, new SurfaceWeather(18, 0.3, 0, 0, 0));
        }

        Assert.True(surface.Layers.Count <= 5, $"layer stack grew to {surface.Layers.Count}");
        AssertConserved(surface);
        AssertSane(surface);
    }

    [Fact]
    public void OneBatchAgreesWithManyShortSteps()
    {
        var weather = Snowing(temperatureF: 25);

        var batched = Loam();
        batched.Advance(480, weather);

        var stepped = Loam();
        for (int i = 0; i < 480; i++) stepped.Advance(1, weather);

        // Two percent over eight hours is the price of not running every rate at minute
        // resolution across nine thousand tiles.
        Assert.InRange(batched.SnowDepthM, stepped.SnowDepthM * 0.98, stepped.SnowDepthM * 1.02);
        Assert.InRange(batched.StoredWaterM, stepped.StoredWaterM * 0.98, stepped.StoredWaterM * 1.02);
        AssertConserved(batched);
        AssertConserved(stepped);
    }

    [Fact]
    public void DryingAndDrainageAreAccountedFor()
    {
        var surface = Loam();
        surface.Advance(60 * 6, Raining());
        double wet = surface.WetnessPct;

        surface.Advance(60 * 24 * 10, new SurfaceWeather(45, 0.5, 0.5, 0, 0));

        Assert.True(surface.WetnessPct < wet, "ground should dry out");
        Assert.True(surface.WaterDrainedM + surface.WaterEvaporatedM > 0);
        AssertConserved(surface);
        AssertSane(surface);
    }

    [Fact]
    public void DeepSnowSlowsTravelAndDeepWaterSlowsItFurther()
    {
        var bare = Loam();
        var snowy = Loam();
        snowy.Advance(60 * 20, Snowing());

        Assert.InRange(bare.TraversalFactor, 1, 1.1);
        Assert.True(snowy.TraversalFactor > 1.5, $"got {snowy.TraversalFactor}");

        var flooded = new GroundSurface(SubstrateProfile.Peat, 0);
        flooded.AddWater(0.3);
        flooded.Advance(1, new SurfaceWeather(40, 0, 0, 0, 0));
        Assert.True(flooded.TraversalFactor > 1.5, $"got {flooded.TraversalFactor}");
    }

    [Fact]
    public void CrossingAFootOfWaterSoaksYouFarMoreThanWalkingOnDampGround()
    {
        var flooded = new GroundSurface(SubstrateProfile.Peat, 0);
        flooded.AddWater(0.3);
        flooded.Advance(1, new SurfaceWeather(40, 0, 0, 0, 0));

        var damp = Loam();
        damp.Advance(60 * 6, Raining());

        double crossing = flooded.GetContactWettingRate(SurfaceContact.Walking) * 6;
        double onDamp = damp.GetContactWettingRate(SurfaceContact.Walking) * 6;

        Assert.True(crossing > 0.4, $"a six-minute wade should soak you, got {crossing:F2}");
        Assert.True(crossing > onDamp * 5, $"wading {crossing:F3} vs damp ground {onDamp:F3}");
    }

    [Fact]
    public void FrozenGroundGivesLittleWaterHoweverSlipperyItIs()
    {
        var surface = Loam();
        surface.AddWater(0.1);
        surface.Advance(60 * 72, new SurfaceWeather(0, 0.2, 0, 0, 0));

        Assert.True(surface.HazardDelta > 0.1, "still slippery");
        Assert.True(surface.GetContactWettingRate(SurfaceContact.Walking) < 0.02,
            "there is no liquid left to soak into anything");
    }

    [Fact]
    public void BurialReducesNewSearchProgressToAFloorButNeverToNothing()
    {
        var surface = Loam();
        Assert.Equal(1, surface.GetSearchFactor(0, 0.3), 6);

        surface.Advance(60 * 30, Snowing());
        Assert.True(surface.SurfaceHeightM > 0.3);

        double flat = surface.GetSearchFactor(0, 0.3);
        double tall = surface.GetSearchFactor(0, 1.5);

        Assert.Equal(0.1, flat, 3);
        Assert.True(tall > flat, "a tall thing still shows above the snow");
        Assert.True(tall < 1);
    }
}
