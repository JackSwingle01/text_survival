namespace Survival.Tests;

/// Exact outputs of the old game (HeatSourceFeature, SurvivalProcessor.Process, Location.GetTemperature
/// plus SurvivalContext), captured by running the old code on the same inputs. A port drift shows here first.
public class GoldenTests
{
    const int Digits = 6;

    // Old HeatSourceFeature(Stone): 0.05 kg birch bark + 1 kg kindling lit, 3 kg pine at minute 8,
    // 4 kg birch at 40, 3 kg oak at 90. (minute, temp °F, burning kg, unburned kg, charcoal kg, phase)
    public static readonly TheoryData<int, double, double, double, double, string> FireTrajectory = new()
    {
        { 5, 416.66666666666663, 0.9490683229813667, 0, 0, "Dying" },
        { 30, 756.0231788394849, 3.27360739715723, 0.1958767244131627, 0, "Steady" },
        { 60, 886.2632512310101, 6.281187668023725, 0.6445010260952623, 0, "Roaring" },
        { 120, 940.6311507693083, 8.924865237309938, 0, 0, "Roaring" },
        { 300, 940.6311507693083, 6.1430269918750895, 0, 0, "Roaring" },
        { 600, 726.8513437762837, 1.9768867460954702, 0, 0, "Steady" },
    };

    [Theory, MemberData(nameof(FireTrajectory))]
    public void Fire_MatchesOldTrajectory(int minute, double tempF, double burningKg, double unburnedKg, double charcoalKg, string phase)
    {
        var f = new FireState { Tile = new TileId(0, 0), LastUpdatedMinute = 0, Pit = PitType.Stone };
        Fire.AddFuel(f, Fuel.BirchBark, 0.05);
        Fire.AddFuel(f, Fuel.Kindling, 1.0);
        Fire.IgniteAll(f);
        var events = new List<WorldEvent>();
        for (int m = 1; m <= minute; m++)
        {
            if (m == 8) Fire.AddFuel(f, Fuel.Pine, 3.0);
            if (m == 40) Fire.AddFuel(f, Fuel.Birch, 4.0);
            if (m == 90) Fire.AddFuel(f, Fuel.Oak, 3.0);
            Fire.Tick(f, new FireId(0), m, events);
        }
        Assert.Equal(tempF, Fire.TemperatureF(f), Digits);
        Assert.Equal(burningKg, Fire.BurningKg(f), Digits);
        Assert.Equal(unburnedKg, Fire.UnburnedKg(f), Digits);
        Assert.Equal(charcoalKg, f.CharcoalKg, Digits);
        Assert.Equal(phase, Fire.Phase(f).ToString());
    }

    // Old SurvivalProcessor.Process(body, ctx, 1 minute) on a baseline human. Inputs, then
    // (core °F delta, hydration ml delta, calories kcal delta, clothing buffer delta, wetness after).
    public static readonly TheoryData<string, double[], bool, double[]> ProcessCases = new()
    {
        // inputs: air °F, wind level, fire bonus °F, clo, clothing kg, core °F, wetness, buffer, activity
        { "cold still", [14, 0.15, 0, 2.0, 3.0, 98.6, 0, 0.3, 1.0], false,
            [0, -1.7361111111111112, -1.2131944444444445, -0.002842431173303718, 0] },
        { "cold windy wet", [0, 0.6, 0, 1.0, 3.0, 97.5, 0.6, 0.0, 1.0], false,
            [-0.08163594656564706, -1.7361111111111112, -1.2131944444444445, 0, 0.6] },
        { "fire", [14, 0.1, 50, 2.0, 3.0, 98.0, 0.4, 0.2, 1.0], false,
            [0.006476081815057685, -1.7361111111111112, -1.2131944444444445, 0.05716905191153173, 0.374389367816092] },
        { "asleep by fire", [14, 0.1, 50, 2.0, 3.0, 98.0, 0, 0.5, 0.5], true,
            [0, -1.2152777777777777, -0.6065972222222222, 0.05537288381732882, 0] },
        { "sweating", [80, 0, 0, 3.0, 3.0, 100.2, 0.1, 0.9, 1.8], false,
            [-0.012851099448932833, -6.736111111111123, -2.1837500000000003, 0, 0.09335344827586207] },
        { "hypothermic", [-20, 0.5, 0, 0.5, 1.0, 93.0, 0.2, 0.0, 1.0], false,
            [-0.10163625859982049, -1.7361111111111112, -1.2131944444444445, 0, 0.2] },
    };

    [Theory, MemberData(nameof(ProcessCases))]
    public void BodyProcess_MatchesOld(string name, double[] input, bool sleeping, double[] expected)
    {
        var b = BodyState.BaselineHuman();
        b.CoreTempF = input[5];
        b.ClothingHeatBufferPct = input[7];
        if (input[6] > 0) Effects.Set(b, EffectKind.Wet, input[6]);
        var ctx = new SurvivalContext
        {
            AirTempF = input[0], WindLevel = input[1], FireBonusF = input[2], ClothingClo = input[3],
            ClothingWeightKg = input[4], ActivityFactor = input[8], IsSleeping = sleeping,
        };
        var d = Body.Process(b, ctx, 1, Rng.ForSystem(1, "golden"));
        Assert.True(Math.Abs(expected[0] - d.CoreTempDeltaF) < 1e-6, $"{name}: core {d.CoreTempDeltaF} vs old {expected[0]}");
        Assert.True(Math.Abs(expected[1] - d.HydrationDeltaMl) < 1e-6, $"{name}: hydration {d.HydrationDeltaMl} vs old {expected[1]}");
        Assert.True(Math.Abs(expected[2] - d.CaloriesDeltaKcal) < 1e-6, $"{name}: calories {d.CaloriesDeltaKcal} vs old {expected[2]}");
        Assert.True(Math.Abs(expected[3] - d.ClothingHeatBufferDeltaPct) < 1e-6, $"{name}: buffer {d.ClothingHeatBufferDeltaPct} vs old {expected[3]}");
        double wet = d.Effects.FirstOrDefault(e => e.Kind == EffectKind.Wet).Severity;
        Assert.True(Math.Abs(expected[4] - wet) < 1e-6, $"{name}: wetness {wet} vs old {expected[4]}");
    }

    /// Old effective temperature at a camp fire: Location.GetTemperature added the pit's heat at
    /// 40% (open air), then SurvivalContext added it again at FireProximity 2, measured against that
    /// warmed air. Open pit, bark + kindling + 3 kg birch, 30 minutes, -10°C, captured from the old code.
    [Fact]
    public void EffectiveHeatAtCamp_MatchesOld()
    {
        const double OldWarmedAirF = 25.491729333120148, OldEffectiveF = 82.10284697892465;
        var world = World.New(1);
        var f = new FireState { Tile = world.CampTile, LastUpdatedMinute = 0, Pit = PitType.Open };
        Fire.AddFuel(f, Fuel.BirchBark, 0.05);
        Fire.AddFuel(f, Fuel.Kindling, 1.0);
        Fire.IgniteAll(f);
        Fire.AddFuel(f, Fuel.Birch, 3.0);
        var events = new List<WorldEvent>();
        for (int m = 1; m <= 30; m++) Fire.Tick(f, new FireId(0), m, events);
        world.Fires[new FireId(world.NextFireId++)] = f;
        world.Minute = f.LastUpdatedMinute;

        var w = Fire.WarmthFor(world, AgentId.Player);
        double airF = world.Weather.TemperatureC * 9 / 5 + 32;
        Assert.Equal(OldWarmedAirF, airF + w.AirHeatF, Digits);
        Assert.Equal(OldEffectiveF, airF + w.AirHeatF + 2.0 * w.PitHeatF, Digits);
    }
}
