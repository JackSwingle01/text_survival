namespace Survival.Tests;

/// Old Weather, WeatherFront, FrontPatterns and Location wind chill, ported. Formula values are
/// exact outputs of the old code; front sequences cannot match (the old RNG is System.Random), so
/// the generator is pinned statistically against the old generator's measured behaviour.
public class WeatherTests
{
    const int Digits = 9;
    static readonly AgentId P = AgentId.Player;

    static WeatherSample Sky(long minute, double c, double wind, double precip, double cloud, WeatherCondition cond) =>
        new(minute, cond, c, wind, precip, cloud, WindDirection.North);

    // ---- exact: old Location.CalculateWindChillNWS ----

    [Theory]
    [InlineData(14, 6.7, 4.087811573877508)]
    [InlineData(0, 20, -21.99522271347127)]
    [InlineData(-20, 45, -58.144996326070746)]
    [InlineData(32, 3, 29.316734684752944)]
    [InlineData(40, 2.9, 40)]   // below 3 mph: no chill
    [InlineData(51, 30, 51)]    // above 50 °F: no chill
    [InlineData(23, 13.5, 10.729616789111176)]
    public void WindChill_MatchesOld(double tempF, double mph, double expected) =>
        Assert.Equal(expected, Weather.WindChillF(tempF, mph), Digits);

    // ---- exact: old Location.GetTemperatureBreakdown(Idle) on an exposed tile, no features ----
    // Minutes are counted from the old GameContext.StartTime (July 1, 09:00).

    public static readonly TheoryData<long, double, double, double, double, WeatherCondition, double> ExposedCases = new()
    {
        { 1080, -10, 3 / 20.1, 0, 0.3, WeatherCondition.Clear, 4.072006577865272 },         // Jul 2 03:00, night
        { 180, -10, 0.05, 0, 0, WeatherCondition.Clear, 29 },                                // noon, calm: sun only
        { 278100, -20, 0.8, 0.9, 1, WeatherCondition.Blizzard, -37.55842345773918 },        // Jan 10 noon
        { 390, 2, 0.3, 0.5, 0.8, WeatherCondition.Rainy, 25.220989880105794 },              // 15:30 rain
        { 0, 5, 0.2, 0, 0.1, WeatherCondition.Clear, 40.14827926927666 },                   // 09:00
        { 220380, -5, 0.4, 0.2, 0.7, WeatherCondition.LightSnow, 11.242672325782394 },      // Dec 1 10:00
    };

    [Theory, MemberData(nameof(ExposedCases))]
    public void ExposedAir_MatchesOld(long minute, double c, double wind, double precip, double cloud, WeatherCondition cond, double expectedF) =>
        Assert.Equal(expectedF, Weather.ExposedAirF(Sky(minute, c, wind, precip, cloud, cond), 0), Digits);

    // ---- exact: old Weather.SunlightIntensity on a clear, cloudless sky at hh:30 ----

    static readonly double[] SineAtHalfHours =
        [0.13052619222005157, 0.3826834323650898, 0.6087614290087207, 0.7933533402912352, 0.9238795325112867, 0.9914448613738104,
         0.9914448613738105, 0.9238795325112867, 0.7933533402912352, 0.6087614290087209, 0.3826834323650899, 0.13052619222005157];

    [Theory]
    [InlineData(900, 4, 20)]                    // Jul 2: summer, sunrise 4, sunset 20, but the sine runs 06-18
    [InlineData(198 * 1440 - 540, 8, 16)]       // Jan 15: winter
    [InlineData(288 * 1440 - 540, 6, 18)]       // Apr 15: spring
    public void SunCurve_MatchesOld(long midnight, int sunrise, int sunset)
    {
        for (int h = 0; h < 24; h++)
        {
            double expected = h >= sunrise && h < sunset && h >= 6 && h < 18 ? SineAtHalfHours[h - 6] : 0;
            Assert.Equal(expected, Weather.Sunlight(midnight + h * 60 + 30, WeatherCondition.Clear, 0), Digits);
        }
        Assert.Equal(sunrise, Weather.SunriseHour(midnight));
        Assert.Equal(sunset, Weather.SunsetHour(midnight));
    }

    [Fact]
    public void Calendar_StartsJulyFirstAtNine()
    {
        Assert.Equal(182, Weather.DayOfYear(0));
        Assert.Equal(9 * 60, Weather.MinuteOfDay(0));
        Assert.Equal(Season.Summer, Weather.SeasonAt(0));
        Assert.Equal(1, Weather.DayNumber(0));
        Assert.Equal(2, Weather.DayNumber(900)); // midnight
        Assert.Equal(Season.Fall, Weather.SeasonAt((266 - 182) * 1440L)); // Sep 23
        Assert.Equal(Season.Winter, Weather.SeasonAt((355 - 182) * 1440L)); // Dec 21
        Assert.Equal(Season.Spring, Weather.SeasonAt((365 - 182 + 80) * 1440L)); // Mar 21
    }

    [Fact]
    public void VisibilityAndCalm_AsOld()
    {
        Assert.Equal(0.1, Sky(0, 0, 0, 0, 0, WeatherCondition.Whiteout).VisibilityFactor);
        Assert.Equal(0.3, Sky(0, 0, 0, 0, 0, WeatherCondition.Blizzard).VisibilityFactor);
        Assert.Equal(1.0, Sky(0, 0, 0, 0, 0, WeatherCondition.Stormy).VisibilityFactor);
        Assert.Equal(WindDirection.Calm, Sky(0, 0, 2.9 / 45, 0, 0, WeatherCondition.Clear).WindDirection);
        Assert.Equal(WindDirection.North, Sky(0, 0, 3.0 / 45, 0, 0, WeatherCondition.Clear).WindDirection);
    }

    // ---- purity: the same minute answers the same, however and whenever it is asked ----

    [Fact]
    public void At_IsIndependentOfQueryOrder()
    {
        var forward = World.New(7);
        var backward = World.New(7);
        var minutes = Enumerable.Range(0, 400).Select(i => (long)i * 397).ToList();
        var a = minutes.Select(m => Weather.At(forward, m)).ToList();
        var b = Enumerable.Reverse(minutes).Select(m => Weather.At(backward, m)).Reverse().ToList();
        Assert.Equal(a, b);
        // and a fresh timeline asked one far minute first agrees too
        Assert.Equal(a[^1], new WeatherTimeline(7).At(minutes[^1]));
    }

    [Fact]
    public void At_SurvivesSaveAndLoad()
    {
        var world = World.New(11);
        Sim.Step(world, new Wait(P), 600);
        var before = Enumerable.Range(0, 200).Select(i => Weather.At(world, i * 211L)).ToList();
        string json = Save.ToJson(world);
        Assert.DoesNotContain("WeatherCache", json);
        var loaded = Save.FromJson(json);
        var after = Enumerable.Range(0, 200).Select(i => Weather.At(loaded, i * 211L)).ToList();
        Assert.Equal(before, after);
    }

    [Fact]
    public void FixedWeather_RoundTripsAndFollowsTheClock()
    {
        var world = World.New(1);
        world.FixedWeather = Sky(0, -10, 0.15, 0, 0.3, WeatherCondition.Clear);
        var loaded = Save.FromJson(Save.ToJson(world));
        Assert.Equal(Weather.At(world, 123), Weather.At(loaded, 123));
        Assert.Equal(123, Weather.At(loaded, 123).Minute);
        Assert.Equal(-10, Weather.At(loaded, 99999).TemperatureC);
    }

    [Fact]
    public void Seeds_GiveDifferentWeather()
    {
        var a = Enumerable.Range(0, 50).Select(i => Weather.At(World.New(1), i * 600L).TemperatureC);
        var b = Enumerable.Range(0, 50).Select(i => Weather.At(World.New(2), i * 600L).TemperatureC);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void FrontStreams_DoNotCollideAcrossSeeds() // regression: seed ^ k once handed seed 1's front 0 to seed 0's front 1
    {
        Assert.NotEqual(Weather.FrontRng(0, 1).NextUlong(), Weather.FrontRng(1, 0).NextUlong());
        Assert.NotEqual(Weather.FrontRng(5, 2).NextUlong(), Weather.FrontRng(7, 0).NextUlong());
    }

    [Fact]
    public void NegativeMinute_Throws() => Assert.Throws<ArgumentOutOfRangeException>(() => Weather.At(World.New(1), -1));

    // ---- ported from old WeatherSeasonTests ----

    const double SeasonToleranceC = 8; // fronts push a few degrees past the plain band; the old bug was 20C+ out

    [Fact]
    public void FirstThreeDays_StayWithinSummerRange()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var world = World.New(seed);
            for (int hour = 0; hour < 72; hour++)
            {
                double c = Weather.At(world, hour * 60L).TemperatureC;
                Assert.True(c >= -5 - SeasonToleranceC && c <= 15 + SeasonToleranceC, $"seed {seed} hour {hour}: {c:F1}C");
            }
        }
    }

    [Fact]
    public void GracePeriod_HasNoSevereWeather()
    {
        for (int seed = 1; seed <= 200; seed++)
            foreach (var s in new WeatherTimeline(seed).SpellsUntil(7 * 1440))
            {
                Assert.True(s.Front is FrontType.ClearSpell or FrontType.StormSystem or FrontType.Warming, $"seed {seed}: {s}");
                Assert.True(s.Condition is not (WeatherCondition.Blizzard or WeatherCondition.Whiteout or WeatherCondition.Stormy), $"seed {seed}: {s}");
            }
    }

    // ---- statistical: a year from game start, against the old generator ----
    // Old values: 200 seeded years of old Weather.Update per minute from GameContext.StartTime
    // (tmp harness), catching the old cold-snap skip crash and carrying on as the old game's next
    // minute would. (season, mean °C, precipitating fraction, blizzard fraction, storm fronts per 30 d, fronts per 30 d)
    public static readonly TheoryData<Season, double, double, double, double, double> OldYear = new()
    {
        { Season.Summer, 2.54, 0.225, 0.0666, 7.44, 24.34 },
        { Season.Fall, -3.77, 0.285, 0.0940, 7.54, 24.66 },
        { Season.Winter, -19.60, 0.216, 0.0988, 7.22, 23.98 },
        { Season.Spring, -6.47, 0.279, 0.0899, 7.32, 24.65 },
    };

    static readonly Lazy<Dictionary<Season, double[]>> NewYear = new(() =>
    {
        // minutes, temp sum, precip minutes, blizzard minutes, storm fronts, fronts
        var acc = Enum.GetValues<Season>().ToDictionary(s => s, _ => new double[6]);
        const long Year = 365 * 1440L, StepMinutes = 15;
        for (int seed = 0; seed < 80; seed++)
        {
            var timeline = new WeatherTimeline(1000 + seed);
            for (long m = 0; m < Year; m += StepMinutes)
            {
                var s = timeline.At(m);
                var a = acc[Weather.SeasonAt(m)];
                a[0] += StepMinutes;
                a[1] += s.TemperatureC * StepMinutes;
                if (s.Condition is WeatherCondition.LightSnow or WeatherCondition.HeavySnow or WeatherCondition.Blizzard
                    or WeatherCondition.Rainy or WeatherCondition.Stormy or WeatherCondition.FreezingRain) a[2] += StepMinutes;
                if (s.Condition == WeatherCondition.Blizzard) a[3] += StepMinutes;
            }
            int lastFront = -1;
            foreach (var sp in timeline.SpellsUntil(Year - 1))
            {
                if (sp.FrontIndex == lastFront) continue;
                lastFront = sp.FrontIndex;
                var a = acc[Weather.SeasonAt(sp.Start)];
                a[5]++;
                if (sp.Front is FrontType.StormSystem or FrontType.ProlongedBlizzard) a[4]++;
            }
        }
        return acc;
    });

    [Theory, MemberData(nameof(OldYear))]
    public void Year_MatchesOldStatistics(Season season, double meanC, double precipFrac, double blizzFrac, double stormsPer30d, double frontsPer30d)
    {
        var a = NewYear.Value[season];
        double per30d = a[0] / (30 * 1440);
        Assert.InRange(a[1] / a[0], meanC - 0.5, meanC + 0.5);
        Assert.InRange(a[2] / a[0], precipFrac - 0.02, precipFrac + 0.02);
        Assert.InRange(a[3] / a[0], blizzFrac - 0.012, blizzFrac + 0.012);
        Assert.InRange(a[4] / per30d, stormsPer30d - 0.6, stormsPer30d + 0.6);
        Assert.InRange(a[5] / per30d, frontsPer30d - 0.8, frontsPer30d + 0.8);
    }

    // ---- wiring into Sim.Surroundings ----

    [Fact]
    public void Surroundings_AppliesWindChill()
    {
        var world = World.New(1);
        world.Minute = 960; // Jul 2 01:00: no sun
        world.FixedWeather = Sky(0, -10, 0.5, 0, 0.3, WeatherCondition.Clear);
        var ctx = Sim.Surroundings(world, world.Agent(P), new Wait(P));
        Assert.Equal(Weather.WindChillF(14, 22.5), ctx.AirTempF, Digits);
        Assert.True(ctx.AirTempF < 14 - 15);
        Assert.Equal(0.5, ctx.WindLevel, Digits);
    }

    [Fact]
    public void WindChill_CoolsTheBody()
    {
        var chilled = World.New(1);
        chilled.FixedWeather = Sky(0, -10, 0.5, 0, 1, WeatherCondition.Cloudy);
        var still = World.New(1);
        still.FixedWeather = Sky(0, -10, 0, 0, 1, WeatherCondition.Cloudy);
        Sim.Step(chilled, new Wait(P), 240);
        Sim.Step(still, new Wait(P), 240);
        Assert.True(chilled.Agent(P).Body.CoreTempF < still.Agent(P).Body.CoreTempF);
    }

    [Theory]
    [InlineData(WeatherCondition.Rainy, Precipitation.Rain)]
    [InlineData(WeatherCondition.Stormy, Precipitation.Rain)]
    [InlineData(WeatherCondition.Blizzard, Precipitation.Blizzard)]
    [InlineData(WeatherCondition.LightSnow, Precipitation.Snow)]
    [InlineData(WeatherCondition.HeavySnow, Precipitation.Snow)]    // old game: None (bug, fixed)
    [InlineData(WeatherCondition.FreezingRain, Precipitation.Rain)] // old game: None (bug, fixed)
    [InlineData(WeatherCondition.Clear, Precipitation.None)]
    public void Precipitation_MapsWhatFallsOnAPerson(WeatherCondition cond, Precipitation expected)
    {
        var world = World.New(1);
        world.FixedWeather = Sky(0, -10, 0.2, 0.6, 1, cond);
        var ctx = Sim.Surroundings(world, world.Agent(P), new Wait(P));
        Assert.Equal(expected, ctx.Precipitation);
        Assert.Equal(0.6, ctx.PrecipitationPct);
    }

    [Theory]
    [InlineData(WeatherCondition.HeavySnow)]
    [InlineData(WeatherCondition.FreezingRain)]
    public void HeavySnowAndFreezingRain_WetAnExposedPerson(WeatherCondition cond)
    {
        var world = World.New(1);
        world.FixedWeather = Sky(0, -1, 0.2, 0.8, 1, cond);
        Sim.Step(world, new Wait(P), 60);
        Assert.True(Effects.Severity(world.Agent(P).Body, EffectKind.Wet) > 0, $"{cond} should wet");
    }

    [Fact]
    public void Rain_WetsTheAgent()
    {
        var rain = World.New(1);
        rain.FixedWeather = Sky(0, 2, 0.2, 0.6, 1, WeatherCondition.Rainy);
        var dry = World.New(1);
        dry.FixedWeather = Sky(0, 2, 0.2, 0, 1, WeatherCondition.Cloudy);
        Sim.Step(rain, new Wait(P), 60);
        Sim.Step(dry, new Wait(P), 60);
        // Not zero for the dry control: thawing ground under it is damp, and that wets too (Ground).
        Assert.True(Effects.Severity(rain.Agent(P).Body, EffectKind.Wet) > 0.1);
        Assert.True(Effects.Severity(dry.Agent(P).Body, EffectKind.Wet) < 0.02);
    }

    [Fact]
    public void SurfaceRates_AsOldGetSurfaceWeather()
    {
        Assert.Equal(0.5 * 0.005 / 60, Sky(0, -5, 0, 0.5, 1, WeatherCondition.HeavySnow).SnowfallWeMPerMinute, 15);
        Assert.Equal(0, Sky(0, -5, 0, 0.5, 1, WeatherCondition.Whiteout).SnowfallWeMPerMinute);
        Assert.Equal(0.5 * 0.010 / 60, Sky(0, 1, 0, 0.5, 1, WeatherCondition.FreezingRain).RainfallMPerMinute, 15);
    }
}
