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
    public void FixedWeather_CannotChangeOnceTheClockMoves()
    {
        var world = World.New(1);
        var sky = Sky(0, -10, 0.15, 0, 0.3, WeatherCondition.Clear);
        world.FixedWeather = sky;
        world.FixedWeather = sky with { TemperatureC = 5 }; // still minute 0: fine
        world.Minute = 1;
        world.FixedWeather = sky with { TemperatureC = 5 }; // the same pin again: fine
        Assert.Throws<InvalidOperationException>(() => world.FixedWeather = sky);
        Assert.Throws<InvalidOperationException>(() => world.FixedWeather = null);
        // and a save made mid-run still loads its pin
        Assert.Equal(5, Weather.At(Save.FromJson(Save.ToJson(world)), 9).TemperatureC);
    }

    /// The old cold snap's last state could be skipped, and AdvanceFront then indexed past the end
    /// (ArgumentOutOfRange; the next minute moved on). The port ends the front after its second state.
    [Fact]
    public void ColdSnapThatSkipsItsLastState_EndsEarly()
    {
        var spells = new WeatherTimeline(6).SpellsUntil(40000).ToList();
        var front = spells.Where(s => s.FrontIndex == 55).ToList();
        Assert.All(front, s => Assert.Equal(FrontType.ColdSnap, s.Front));
        Assert.Equal([(37128L, WeatherCondition.Clear), (37688L, WeatherCondition.Clear)], front.Select(s => (s.Start, s.Condition)));
        Assert.Equal(38688, spells.First(s => s.FrontIndex == 56).Start); // straight after the second state's 1000 minutes
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
    public void BeforeThePreRoll_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Weather.At(World.New(1), -Weather.PreRollMinutes - 1));

    // ---- the pre-roll: turn 1 opens a month into the fronts, as old CreateNewGame + Settle did ----

    [Fact]
    public void Seed17_DoesNotOpenInTheInitialFront()
    {
        var opening = new WeatherTimeline(17).SpellsUntil(0).Last();
        Assert.NotEqual(0, opening.FrontIndex);
        Assert.True(opening.Start < 0 || opening.FrontIndex > 1, $"opening spell {opening}");
    }

    // Old: 2000 seeds of new Weather(-10, StartTime - 30 d) ticked hourly to StartTime, as Settle did
    // (tmp harness p2fix/oldopen). Initial front still active 0; Clear+Cloudy 0.788; ClearSpell 0.529,
    // StormSystem 0.276, Warming 0.195; mean 5.75 °C.
    [Fact]
    public void Opening_DistributionMatchesOldSettledStart()
    {
        const int Seeds = 200;
        int dry = 0, clearSpell = 0, storm = 0, initial = 0;
        double sumC = 0;
        for (int seed = 0; seed < Seeds; seed++)
        {
            var timeline = new WeatherTimeline(seed);
            var spell = timeline.SpellsUntil(0).Last();
            var s = timeline.At(0);
            if (spell.FrontIndex == 0) initial++;
            if (s.Condition is WeatherCondition.Clear or WeatherCondition.Cloudy) dry++;
            if (spell.Front == FrontType.ClearSpell) clearSpell++;
            if (spell.Front == FrontType.StormSystem) storm++;
            sumC += s.TemperatureC;
        }
        Assert.Equal(0, initial);
        Assert.InRange(dry / (double)Seeds, 0.788 - 0.08, 0.788 + 0.08);
        Assert.InRange(clearSpell / (double)Seeds, 0.529 - 0.09, 0.529 + 0.09);
        Assert.InRange(storm / (double)Seeds, 0.276 - 0.08, 0.276 + 0.08);
        Assert.InRange(sumC / Seeds, 5.75 - 1.5, 5.75 + 1.5);
    }

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
                if (sp.FrontIndex == lastFront || sp.Start < 0) continue; // the pre-roll is not the year
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
        world.FixedWeather = Sky(0, -10, 0.5, 0, 0.3, WeatherCondition.Clear);
        world.Minute = 960; // Jul 2 01:00: no sun
        // Camp lets 0.4 of the wind through; open plain 1.2 (old Location.WindFactor inside the chill).
        var ctx = Sim.Surroundings(world, world.Agent(P), new Wait(P));
        Assert.Equal(Weather.WindChillF(14, 22.5 * 0.4), ctx.AirTempF, Digits);
        world.Agent(P).Tile = TileOf(Map.Of(world), Terrain.Plain);
        ctx = Sim.Surroundings(world, world.Agent(P), new Wait(P));
        Assert.Equal(Weather.WindChillF(14, 22.5 * 1.2), ctx.AirTempF, Digits);
        Assert.True(ctx.AirTempF < 14 - 15);
        Assert.Equal(0.5, ctx.WindLevel, Digits); // old: the body's convective wind is regional, not the tile's
    }

    /// An ordinary tile of this terrain: not camp, a cave, a pass stage or beside a river.
    static TileId TileOf(GeneratedMap m, Terrain terrain) =>
        (from x in Enumerable.Range(0, m.Width) from y in Enumerable.Range(0, m.Height)
         let t = new TileId(x, y)
         where m.TerrainAt(t) == terrain && t != m.Camp && m.CaveAt(t) == Cave.None && m.StageAt(t) == null && !m.BesideRiver(t)
         select t).First();

    // ---- the tile reaches the body. old game: Location.GetTemperatureBreakdown applied it; the port had every tile open ----

    [Fact]
    public void ThePassProper_FeelsMuchColderThanCamp_UnderTheSameSky()
    {
        var world = World.New(17);
        world.FixedWeather = Sky(0, -10, 0.5, 0.3, 0.8, WeatherCondition.Cloudy);
        var m = Map.Of(world);
        var pass = m.PassStageIndex.Single(kv => Map.PassStages[kv.Value].Name == "The Pass Proper").Key;
        double campF = Sim.Surroundings(world, world.Agent(P), new Wait(P)).AirTempF;
        world.Agent(P).Tile = pass;
        double passF = Sim.Surroundings(world, world.Agent(P), new Wait(P)).AirTempF;
        // -15 °F of its own, and twice the wind through the chill against camp's 0.4.
        Assert.Equal(Weather.ExposedAirF(Weather.At(world, 0), 0, 2.0, 0, -15), passF, Digits);
        Assert.True(passF < campF - 25, $"pass {passF:F1} F vs camp {campF:F1} F");
    }

    [Fact]
    public void ForestCover_WetsLessThanOpenPlain_AndKeepsTheSunOff()
    {
        var world = World.New(17);
        world.FixedWeather = Sky(0, 3, 0.2, 0.8, 1, WeatherCondition.Rainy);
        var m = Map.Of(world);
        var forest = TileOf(m, Terrain.Forest);
        var plain = TileOf(m, Terrain.Plain);
        double Wetness(TileId at)
        {
            var w = World.New(17);
            w.FixedWeather = world.FixedWeather;
            w.Agent(P).Tile = at;
            Assert.Equal(Map.OverheadCover(m, at), Sim.Surroundings(w, w.Agent(P), new Wait(P)).OverheadCoverLevel);
            Sim.Step(w, new Wait(P), 60);
            return Effects.Severity(w.Agent(P).Body, EffectKind.Wet);
        }
        double underTrees = Wetness(forest), inTheOpen = Wetness(plain);
        Assert.True(underTrees < inTheOpen - 0.1, $"forest {underTrees:F3} vs plain {inTheOpen:F3}");
        // The canopy (0.5) halves the precipitation's cooling and the sun's warming; forest wind 0.3.
        var sky = Weather.At(world, 0);
        Assert.Equal(Weather.ExposedAirF(sky, 0, 0.3, 0.5, 0), Weather.AirAtF(world, forest, sky, 0), Digits);
        Assert.NotEqual(Weather.ExposedAirF(sky, 0), Weather.AirAtF(world, forest, sky, 0));
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
        world.FixedWeather = Sky(0, 0, 0.2, 0.6, 1, cond); // 0 °C: either phase can fall, so the condition decides
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
        // Not zero for the dry control: thawed ground under it is damp, and that wets too (Ground),
        // but only as far as your boots.
        double wetInRain = Effects.Severity(rain.Agent(P).Body, EffectKind.Wet), wetDry = Effects.Severity(dry.Agent(P).Body, EffectKind.Wet);
        Assert.True(wetDry <= 0.1 + 1e-9, $"dry {wetDry}");
        Assert.True(wetInRain > wetDry + 0.1, $"rain {wetInRain} vs dry {wetDry}");
    }

    // ---- precipitation phase follows temperature. old game: July fronts snowed at +15 °C (bug, fixed) ----

    [Theory]
    [InlineData(WeatherCondition.LightSnow, 10, WeatherCondition.Rainy)]
    [InlineData(WeatherCondition.HeavySnow, 1.5, WeatherCondition.Rainy)]
    [InlineData(WeatherCondition.Blizzard, 8, WeatherCondition.Stormy)]
    [InlineData(WeatherCondition.Whiteout, 8, WeatherCondition.Misty)]
    [InlineData(WeatherCondition.Stormy, -10, WeatherCondition.Blizzard)]
    [InlineData(WeatherCondition.LightSnow, 0.5, WeatherCondition.LightSnow)] // the band near 0: the front decides
    [InlineData(WeatherCondition.Stormy, -0.5, WeatherCondition.Stormy)]
    [InlineData(WeatherCondition.FreezingRain, -1, WeatherCondition.FreezingRain)]
    public void Phase_FollowsTemperature(WeatherCondition rolled, double c, WeatherCondition falls)
    {
        var s = Sky(0, c, 0.5, 0.6, 1, rolled);
        Assert.Equal(falls, s.Condition);
        bool rain = falls is WeatherCondition.Rainy or WeatherCondition.Stormy or WeatherCondition.FreezingRain;
        bool snow = falls is WeatherCondition.LightSnow or WeatherCondition.HeavySnow or WeatherCondition.Blizzard;
        Assert.Equal(rain, s.Precipitation == Precipitation.Rain);  // the body
        Assert.Equal(rain, s.RainfallMPerMinute > 0);                // the ground
        Assert.Equal(snow, s.SnowfallWeMPerMinute > 0);
        if (rain) Assert.Equal(1.0, s.VisibilityFactor);              // and how far you see
        if (falls == WeatherCondition.Misty) Assert.Equal(0.5, s.VisibilityFactor);
    }

    [Fact]
    public void SeededYear_NeverSnowsAboveFreezingOrRainsInHardFrost()
    {
        var world = World.New(17);
        int rainMinutes = 0;
        for (long m = 0; m < 365 * 1440L; m += 30)
        {
            var s = Weather.At(world, m);
            if (s.TemperatureC > Weather.RainAboveC)
                Assert.False(s.Condition is WeatherCondition.LightSnow or WeatherCondition.HeavySnow or WeatherCondition.Blizzard or WeatherCondition.Whiteout,
                    $"minute {m}: {s.Condition} at {s.TemperatureC:F1} C");
            if (s.TemperatureC < Weather.SnowBelowC)
                Assert.False(s.Condition is WeatherCondition.Rainy or WeatherCondition.Stormy, $"minute {m}: {s.Condition} at {s.TemperatureC:F1} C");
            if (s.Precipitation == Precipitation.Rain) rainMinutes += 30;
        }
        Assert.True(rainMinutes > 0, "a year with no rain at all");
        // Erosion reads the same sky: a pinned rainy sky and the same sky rolled as snow erode alike.
        Assert.Equal(Tracks.ErosionPerMinute(Sky(0, 10, 0.5, 0.6, 1, WeatherCondition.LightSnow)),
            Tracks.ErosionPerMinute(Sky(0, 10, 0.5, 0.6, 1, WeatherCondition.Rainy)));
    }

    [Fact]
    public void SurfaceRates_AsOldGetSurfaceWeather()
    {
        Assert.Equal(0.5 * 0.005 / 60, Sky(0, -5, 0, 0.5, 1, WeatherCondition.HeavySnow).SnowfallWeMPerMinute, 15);
        Assert.Equal(0, Sky(0, -5, 0, 0.5, 1, WeatherCondition.Whiteout).SnowfallWeMPerMinute);
        Assert.Equal(0.5 * 0.010 / 60, Sky(0, 1, 0, 0.5, 1, WeatherCondition.FreezingRain).RainfallMPerMinute, 15);
    }
}
