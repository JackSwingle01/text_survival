using System.Diagnostics;
using System.Globalization;

namespace Survival.Tests;

/// Ground surface. The state-level tests are the old GroundSurfaceTests; the goldens are the old
/// GroundSurface driven through scripted weather (harness referencing text_survival.csproj).
public class GroundTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly SubstrateProfile Loam = SubstrateProfile.Loam;

    static GroundState Fresh(SubstrateProfile sub, double frozen = 0) => Ground.Baseline(sub, frozen);

    static SurfaceWeather Snowing(double temperatureF = 15, double weMPerMinute = 5e-3 / 60) => new(temperatureF, 0.2, 0, weMPerMinute, 0);
    static SurfaceWeather Raining(double temperatureF = 40, double mPerMinute = 10e-3 / 60) => new(temperatureF, 0.1, 0, 0, mPerMinute);

    /// Water in equals water held plus water gone, to a micrometre. (Not exact: displaced water
    /// under the 1e-9 m negligible threshold is dropped, as in the old game; ~1e-9 m a step at worst.)
    static void AssertConserved(GroundState s, SubstrateProfile sub)
    {
        double accounted = Ground.StoredWaterM(s, sub) + s.WaterDrainedM + s.WaterEvaporatedM + s.WaterOverflowedM;
        Assert.True(Math.Abs(s.WaterAddedM - accounted) < 1e-6, $"added {s.WaterAddedM:R}, accounted {accounted:R}");
    }

    static void AssertSane(GroundState s, SubstrateProfile sub)
    {
        Assert.True(Ground.SnowDepthM(s, sub) >= 0);
        Assert.True(Ground.StandingWaterDepthM(s, sub) >= 0);
        Assert.InRange(Ground.WetnessPct(s), 0, 1);
        Assert.True(double.IsFinite(Ground.SurfaceHeightM(s, sub)));
        Assert.True(double.IsFinite(Ground.TraversalFactor(s, sub)));
        Assert.InRange(Ground.HazardDelta(s, sub), 0, 0.5);
        foreach (var l in s.Layers)
        {
            Assert.True(l.ThicknessM >= 0);
            Assert.InRange(l.CompactionPct, 0, 1);
            Assert.InRange(l.WaterSaturationPct, 0, 1);
            Assert.InRange(l.FrozenFractionPct, 0, 1);
        }
    }

    // ================= ported: old GroundSurfaceTests =================

    [Fact]
    public void Snowfall_AccumulatesAndDeepensTheGround()
    {
        var s = Fresh(Loam);
        Ground.Advance(s, Loam, 60 * 6, Snowing());
        Assert.InRange(Ground.SnowDepthM(s, Loam), 0.18, 0.31); // 3 cm water-equivalent as ~30 cm of snow, less settling
        Assert.True(Ground.SurfaceHeightM(s, Loam) > 0.15);
        AssertConserved(s, Loam);
        AssertSane(s, Loam);
    }

    [Fact]
    public void WhiteoutWithoutSnowfall_LeavesNothingOnTheGround()
    {
        var s = Fresh(Loam);
        Ground.Advance(s, Loam, 60 * 12, new SurfaceWeather(10, 1.0, 0, 0, 0));
        Assert.Equal(0, Ground.SnowDepthM(s, Loam), 9);
        AssertConserved(s, Loam);
    }

    [Fact]
    public void Compaction_ShrinksSnowWithoutLosingAnyOfIt()
    {
        var s = Fresh(Loam);
        Ground.Advance(s, Loam, 60 * 4, Snowing());
        double before = Ground.SnowDepthM(s, Loam), water = Ground.StoredWaterM(s, Loam);
        Ground.Compact(s, Loam, 0.6);
        Assert.True(Ground.SnowDepthM(s, Loam) < before * 0.6, "packed snow should be markedly shallower");
        Assert.Equal(water, Ground.StoredWaterM(s, Loam), 6);
        AssertConserved(s, Loam);
        AssertSane(s, Loam);
    }

    [Fact]
    public void Thaw_TurnsSnowIntoWetGroundAndThenPuddles()
    {
        var s = Fresh(Loam);
        Ground.Advance(s, Loam, 60 * 10, Snowing());
        double snow = Ground.SnowDepthM(s, Loam), dryness = Ground.WetnessPct(s);
        Ground.Advance(s, Loam, 60 * 48, new SurfaceWeather(50, 0.2, 0.6, 0, 0));
        Assert.True(Ground.SnowDepthM(s, Loam) < snow, "snow should melt back");
        Assert.True(Ground.WetnessPct(s) > dryness, "meltwater should soak the ground");
        AssertConserved(s, Loam);
        AssertSane(s, Loam);
    }

    [Fact]
    public void ColdNight_GlazesStandingWaterWithoutDeletingIt()
    {
        var peat = SubstrateProfile.Peat;
        var s = Fresh(peat);
        Ground.AddWater(s, peat, 0.05);
        Ground.Advance(s, peat, 60 * 10, new SurfaceWeather(15, 0.1, 0, 0, 0));
        Assert.True(Ground.HazardDelta(s, peat) > 0.1, "an iced-over surface should be slippery");
        Assert.True(Ground.StandingWaterDepthM(s, peat) > 0.01, "the water is still there, it is just ice");
        Assert.True(Ground.StandingLiquidDepthM(s, peat) < Ground.StandingWaterDepthM(s, peat));
        AssertConserved(s, peat);
        double glazed = Ground.HazardDelta(s, peat);
        Ground.Advance(s, peat, 60 * 24, new SurfaceWeather(45, 0.1, 0.3, 0, 0));
        Assert.True(Ground.HazardDelta(s, peat) < glazed * 0.5, "the ice should melt back to water");
        AssertConserved(s, peat);
    }

    [Fact]
    public void Peat_PondsAtOnceWhilePermeableGroundSoaksItUp()
    {
        var peat = SubstrateProfile.Peat;
        var marsh = Fresh(peat);
        var loam = Fresh(Loam);
        Ground.Advance(marsh, peat, 120, Raining());
        Ground.Advance(loam, Loam, 120, Raining());
        Assert.True(Ground.StandingWaterDepthM(marsh, peat) > 0.005);
        Assert.True(Ground.StandingWaterDepthM(loam, Loam) < Ground.StandingWaterDepthM(marsh, peat) * 0.2);
        AssertConserved(marsh, peat);
        AssertConserved(loam, Loam);
    }

    [Fact]
    public void InfiltrationFillsTheGroundBeforeAnythingOverflows()
    {
        var s = Fresh(Loam);
        Ground.Advance(s, Loam, 60 * 24, Raining());
        Assert.True(Ground.WetnessPct(s) > 0.5, "a day of rain should wet permeable ground through");
        AssertConserved(s, Loam);
        AssertSane(s, Loam);
    }

    [Fact]
    public void StandingLiquidNeverExceedsAFoot_AndTheOverflowIsAccountedFor()
    {
        var peat = SubstrateProfile.Peat;
        var s = Fresh(peat);
        Ground.AddWater(s, peat, 2.0);
        Ground.Advance(s, peat, 30, new SurfaceWeather(40, 0, 0, 0, 0));
        Assert.True(Ground.StandingLiquidDepthM(s, peat) <= 0.3048 + 1e-6);
        Assert.True(s.WaterOverflowedM > 1.0, "the rest must be recorded leaving, not vanish");
        AssertConserved(s, peat);
    }

    [Fact]
    public void FrozenGroundCannotAbsorbAnExtraFullCapacity()
    {
        var s = Fresh(Loam, frozen: 1.0);
        double before = Ground.WetnessPct(s);
        Ground.Advance(s, Loam, 60 * 6, Raining(temperatureF: 20));
        Assert.InRange(Ground.WetnessPct(s), before, 1.0);
        AssertConserved(s, Loam);
        AssertSane(s, Loam);
    }

    [Fact]
    public void RepeatedDepositsAndPhaseCyclesKeepTheStackBounded()
    {
        var s = Fresh(Loam);
        for (int cycle = 0; cycle < 40; cycle++)
        {
            Ground.Advance(s, Loam, 120, Snowing(temperatureF: 12));
            Ground.Advance(s, Loam, 120, new SurfaceWeather(42, 0.3, 0.5, 0, 0));
            Ground.Advance(s, Loam, 60, new SurfaceWeather(18, 0.3, 0, 0, 0));
        }
        Assert.True(s.Layers.Count <= 5, $"layer stack grew to {s.Layers.Count}");
        AssertConserved(s, Loam);
        AssertSane(s, Loam);
    }

    [Fact]
    public void OneBatchAgreesWithManyShortSteps()
    {
        var w = Snowing(temperatureF: 25);
        var batched = Fresh(Loam);
        Ground.Advance(batched, Loam, 480, w);
        var stepped = Fresh(Loam);
        for (int i = 0; i < 480; i++) Ground.Advance(stepped, Loam, 1, w);
        Assert.InRange(Ground.SnowDepthM(batched, Loam), Ground.SnowDepthM(stepped, Loam) * 0.98, Ground.SnowDepthM(stepped, Loam) * 1.02);
        Assert.InRange(Ground.StoredWaterM(batched, Loam), Ground.StoredWaterM(stepped, Loam) * 0.98, Ground.StoredWaterM(stepped, Loam) * 1.02);
        AssertConserved(batched, Loam);
        AssertConserved(stepped, Loam);
    }

    [Fact]
    public void DryingAndDrainageAreAccountedFor()
    {
        var s = Fresh(Loam);
        Ground.Advance(s, Loam, 60 * 6, Raining());
        double wet = Ground.WetnessPct(s);
        Ground.Advance(s, Loam, 60 * 24 * 10, new SurfaceWeather(45, 0.5, 0.5, 0, 0));
        Assert.True(Ground.WetnessPct(s) < wet, "ground should dry out");
        Assert.True(s.WaterDrainedM + s.WaterEvaporatedM > 0);
        AssertConserved(s, Loam);
        AssertSane(s, Loam);
    }

    [Fact]
    public void DeepSnowSlowsTravelAndDeepWaterSlowsItFurther()
    {
        var bare = Fresh(Loam);
        var snowy = Fresh(Loam);
        Ground.Advance(snowy, Loam, 60 * 20, Snowing());
        Assert.InRange(Ground.TraversalFactor(bare, Loam), 1, 1.1);
        Assert.True(Ground.TraversalFactor(snowy, Loam) > 1.5);
        var peat = SubstrateProfile.Peat;
        var flooded = Fresh(peat);
        Ground.AddWater(flooded, peat, 0.3);
        Ground.Advance(flooded, peat, 1, new SurfaceWeather(40, 0, 0, 0, 0));
        Assert.True(Ground.TraversalFactor(flooded, peat) > 1.5);
    }

    [Fact]
    public void CrossingAFootOfWaterSoaksYouFarMoreThanWalkingOnDampGround()
    {
        var peat = SubstrateProfile.Peat;
        var flooded = Fresh(peat);
        Ground.AddWater(flooded, peat, 0.3);
        Ground.Advance(flooded, peat, 1, new SurfaceWeather(40, 0, 0, 0, 0));
        var damp = Fresh(Loam);
        Ground.Advance(damp, Loam, 60 * 6, Raining());
        double crossing = Ground.ContactWettingRate(flooded, peat, SurfaceContact.Walking) * 6;
        double onDamp = Ground.ContactWettingRate(damp, Loam, SurfaceContact.Walking) * 6;
        Assert.True(crossing > 0.4, $"a six-minute wade should soak you, got {crossing:F2}");
        Assert.True(crossing > onDamp * 5);
    }

    [Fact]
    public void FrozenGroundGivesLittleWaterHoweverSlipperyItIs()
    {
        var s = Fresh(Loam);
        Ground.AddWater(s, Loam, 0.1);
        Ground.Advance(s, Loam, 60 * 72, new SurfaceWeather(0, 0.2, 0, 0, 0));
        Assert.True(Ground.HazardDelta(s, Loam) > 0.1, "still slippery");
        Assert.True(Ground.ContactWettingRate(s, Loam, SurfaceContact.Walking) < 0.02);
    }

    [Fact]
    public void BurialReducesNewSearchProgressToAFloorButNeverToNothing()
    {
        var s = Fresh(Loam);
        Assert.Equal(1, Ground.SearchFactor(s, Loam, 0, 0.3), 6);
        Ground.Advance(s, Loam, 60 * 30, Snowing());
        Assert.True(Ground.SurfaceHeightM(s, Loam) > 0.3);
        double flat = Ground.SearchFactor(s, Loam, 0, 0.3), tall = Ground.SearchFactor(s, Loam, 0, 1.5);
        Assert.Equal(0.1, flat, 3);
        Assert.True(tall > flat && tall < 1);
    }

    [Fact]
    public void ContactCeiling_StopsWettingOnceThatContactHasSoakedYouAsFarAsItReaches()
    {
        var peat = SubstrateProfile.Peat;
        var s = Fresh(peat);
        Ground.AddWater(s, peat, 0.05);
        Assert.True(Ground.ContactWettingRate(s, peat, SurfaceContact.WorkingOnGround, 0.39) > 0);
        Assert.Equal(0, Ground.ContactWettingRate(s, peat, SurfaceContact.WorkingOnGround, 0.4));
        Assert.Equal(0, Ground.ContactWettingRate(s, peat, SurfaceContact.RestingOnGround, 0.7));
    }

    [Fact]
    public void SettlingSnowFullOfIce_NeverGrowsItsSkeleton()
    {
        // A weathered top layer: sublimation shrank the skeleton round its refrozen meltwater
        // until the pores were full of ice. Settling it must not conjure ice to make room.
        var s = Fresh(Loam);
        s.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = 0.004, CompactionPct = 0.53, WaterSaturationPct = 1, FrozenFractionPct = 1 });
        s.WaterAddedM = Ground.StoredWaterM(s, Loam);
        Ground.Advance(s, Loam, 15, new SurfaceWeather(25, 0.3, 0, 0, 0));
        AssertConserved(s, Loam);
        Assert.Equal(0.53, s.Layers[^1].CompactionPct, 12); // its pores are full: it cannot pack any tighter
    }

    // ================= goldens: the old GroundSurface, scripted weather =================

    static string R(double d) => d.ToString("R", CultureInfo.InvariantCulture);

    /// The substrates the golden script runs, by the names its tags carry (the old harness's profile names).
    static readonly (string Name, SubstrateProfile Sub)[] GoldenSubstrates =
    [
        ("loam", SubstrateProfile.Loam), ("forestFloor", SubstrateProfile.ForestFloor), ("slope", SubstrateProfile.Slope),
        ("rock", SubstrateProfile.Rock), ("peat", SubstrateProfile.Peat), ("iceSheet", SubstrateProfile.IceSheet),
    ];

    static string Dump(string tag, GroundState g, SubstrateProfile sub)
    {
        var layers = string.Join(";", g.Layers.Select(l => $"{l.Material}:{R(l.ThicknessM)}:{R(l.CompactionPct)}:{R(l.WaterSaturationPct)}:{R(l.FrozenFractionPct)}"));
        return $"{tag}|{layers}|add={R(g.WaterAddedM)}|drn={R(g.WaterDrainedM)}|evp={R(g.WaterEvaporatedM)}|ovf={R(g.WaterOverflowedM)}"
            + $"|tf={R(Ground.TraversalFactor(g, sub))}|hd={R(Ground.HazardDelta(g, sub))}|sf={R(Ground.SearchFactor(g, sub, 0, 0.3))}"
            + $"|cww={R(Ground.ContactWettingRate(g, sub, SurfaceContact.Walking, 0))}|cwr={R(Ground.ContactWettingRate(g, sub, SurfaceContact.RestingOnGround, 0.2))}"
            + $"|cwk={R(Ground.ContactWettingRate(g, sub, SurfaceContact.WorkingOnGround, 0))}";
    }

    /// Same script as the harness, line for line.
    static List<string> RunScript()
    {
        var lines = new List<string>();
        void Run(GroundState g, SubstrateProfile sub, int minutes, SurfaceWeather w, bool perMinute)
        {
            if (perMinute) for (int i = 0; i < minutes; i++) Ground.Advance(g, sub, 1, w);
            else Ground.Advance(g, sub, minutes, w);
        }
        var snow = new SurfaceWeather(15, 0.2, 0, 0.005 / 60, 0);
        var thaw = new SurfaceWeather(45, 0.3, 0.5, 0, 0);
        var freeze = new SurfaceWeather(10, 0.4, 0, 0, 0);
        var rain = new SurfaceWeather(40, 0.1, 0, 0, 0.010 / 60);
        var dry = new SurfaceWeather(45, 0.5, 0.4, 0, 0);
        foreach (bool pm in new[] { false, true })
        {
            string mode = pm ? "pm" : "batch";
            var g = Ground.Baseline(Loam);
            Run(g, Loam, 2 * 1440, snow, pm); lines.Add(Dump($"{mode}-seq-snow", g, Loam));
            Run(g, Loam, 2 * 1440, thaw, pm); lines.Add(Dump($"{mode}-seq-thaw", g, Loam));
            Run(g, Loam, 1440, freeze, pm); lines.Add(Dump($"{mode}-seq-freeze", g, Loam));
            foreach (var (name, sub) in GoldenSubstrates)
            {
                var r = Ground.Baseline(sub, 0);
                Run(r, sub, 1440, rain, pm); lines.Add(Dump($"{mode}-rain-{name}", r, sub));
                Run(r, sub, 2 * 1440, dry, pm); lines.Add(Dump($"{mode}-dry-{name}", r, sub));
                Run(r, sub, 720, freeze, pm); lines.Add(Dump($"{mode}-frz-{name}", r, sub));
            }
        }
        var c = Ground.Baseline(Loam, 0);
        Ground.Advance(c, Loam, 240, snow); Ground.Compact(c, Loam, 0.6); lines.Add(Dump("compact", c, Loam));
        var peat = SubstrateProfile.Peat;
        var p = Ground.Baseline(peat, 0);
        Ground.AddWater(p, peat, 0.3); Ground.Advance(p, peat, 1, new SurfaceWeather(40, 0, 0, 0, 0)); lines.Add(Dump("flooded", p, peat));
        Ground.Advance(p, peat, 600, new SurfaceWeather(15, 0.1, 0, 0, 0)); lines.Add(Dump("glazed", p, peat));
        var s30 = Ground.Baseline(Loam);
        Ground.Advance(s30, Loam, 1800, snow);
        lines.Add($"search30|{R(Ground.SearchFactor(s30, Loam, 0, 0.3))}|{R(Ground.SearchFactor(s30, Loam, 0, 1.5))}|{R(Ground.SearchFactor(s30, Loam, 0.2, 0.5))}");
        return lines;
    }

    /// Every number in a line, in order: layer fields and the named values.
    static double[] Numbers(string line) =>
        line.Split('|').Skip(1).SelectMany(f => f.Split(';')).SelectMany(f => f.Split(':', '='))
            .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? (double?)v : null)
            .Where(v => v != null).Select(v => v!.Value).ToArray();

    [Fact]
    public void Golden_LayerStateAndQueriesMatchTheOldGroundSurface()
    {
        var expected = OldGolden.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var actual = RunScript();
        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            string tag = expected[i].Split('|')[0];
            Assert.Equal(tag, actual[i].Split('|')[0]);
            // Same layer stack, same materials.
            Assert.Equal(string.Join(",", expected[i].Split('|')[1].Split(';').Select(l => l.Split(':')[0])),
                         string.Join(",", actual[i].Split('|')[1].Split(';').Select(l => l.Split(':')[0])));
            var e = Numbers(expected[i]);
            var a = Numbers(actual[i]);
            Assert.Equal(e.Length, a.Length);
            for (int k = 0; k < e.Length; k++)
                Assert.True(Math.Abs(e[k] - a[k]) <= 1e-9 + 1e-9 * Math.Abs(e[k]), $"{tag} value {k}: old {e[k]:R} new {a[k]:R}");
        }
    }

    [Fact]
    public void Golden_LedgerBalancesInEveryScriptedState()
    {
        foreach (var line in OldGolden.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(l => !l.StartsWith("search30")))
        {
            // Recompute the held water from the old layer fields and check the old ledger closes.
            string tag = line.Split('|')[0];
            var sub = tag.Contains("seq") || tag == "compact" ? Loam
                : tag is "flooded" or "glazed" ? SubstrateProfile.Peat
                : GoldenSubstrates.Single(s => tag.EndsWith("-" + s.Name)).Sub;
            var fields = line.Split('|');
            var state = new GroundState
            {
                Layers = fields[1].Split(';').Select(l => l.Split(':')).Select(f => new SurfaceLayer
                {
                    Material = Enum.Parse<SurfaceMaterial>(f[0]),
                    ThicknessM = double.Parse(f[1], CultureInfo.InvariantCulture),
                    CompactionPct = double.Parse(f[2], CultureInfo.InvariantCulture),
                    WaterSaturationPct = double.Parse(f[3], CultureInfo.InvariantCulture),
                    FrozenFractionPct = double.Parse(f[4], CultureInfo.InvariantCulture),
                }).ToList(),
            };
            double Field(string name) => double.Parse(fields.Single(f => f.StartsWith(name + "=")).Split('=')[1], CultureInfo.InvariantCulture);
            Assert.Equal(Field("add"), Ground.StoredWaterM(state, sub) + Field("drn") + Field("evp") + Field("ovf"), 9);
        }
    }

    // ================= the world: weather in, lazy catch-up, wiring =================

    static readonly GeneratedMap Seed17 = Map.Generate(17);

    static World World17(WeatherSample? sky = null, TileId? at = null)
    {
        var world = new World { Seed = 17, GeneratedMap = Seed17, FixedWeather = sky };
        world.Agents[P] = new Agent { Id = P, Tile = at ?? Seed17.Camp };
        return world;
    }

    static WeatherSample Sky(WeatherCondition cond, double c, double precip, double wind = 0.2, double cloud = 1) =>
        new(0, cond, c, wind, precip, cloud, WindDirection.North);

    static TileId First(Func<TileId, bool> pick)
    {
        for (int y = 0; y < Seed17.Height; y++)
            for (int x = 0; x < Seed17.Width; x++)
                if (pick(new TileId(x, y))) return new TileId(x, y);
        throw new InvalidOperationException("no such tile on seed 17");
    }

    static TileId OpenTile(Terrain t) => First(p => Seed17.TerrainAt(p) == t && Seed17.CaveAt(p) == Cave.None
        && Seed17.StageAt(p) == null && p != Seed17.Camp && !Seed17.BesideRiver(p));

    [Fact]
    public void Substrate_FollowsTerrain_AndNamedPlacesKeepTheirOldGround()
    {
        Assert.Equal(SubstrateProfile.Peat, Ground.Substrate(Seed17, OpenTile(Terrain.Marsh)));
        Assert.Equal(SubstrateProfile.Loam, Ground.Substrate(Seed17, OpenTile(Terrain.Plain)));
        Assert.Equal(SubstrateProfile.IceSheet, Ground.Substrate(Seed17, OpenTile(Terrain.Water)));
        Assert.Equal(SubstrateProfile.ForestFloor, Ground.Substrate(Seed17, Seed17.Camp));
        Assert.Equal(SubstrateProfile.Rock, Ground.Substrate(Seed17, Seed17.Exit));
        Assert.Equal(SubstrateProfile.Rock, Ground.Substrate(Seed17, First(p => Seed17.CaveAt(p) == Cave.Floor)));
    }

    [Fact]
    public void SurfaceWeather_CanopyInterceptsOnce_WhiteoutDepositsNothing_PassIsColder()
    {
        var world = World17(Sky(WeatherCondition.HeavySnow, -5, 0.8));
        var sky = Weather.At(world, 0);
        var forest = OpenTile(Terrain.Forest);
        var plain = OpenTile(Terrain.Plain);
        Assert.Equal(sky.SnowfallWeMPerMinute * 0.5, Exposure.Surface(world, forest, 0).SnowfallWeMPerMinute, 15);
        Assert.Equal(sky.SnowfallWeMPerMinute, Exposure.Surface(world, plain, 0).SnowfallWeMPerMinute, 15);
        Assert.Equal(0, Exposure.Surface(world, First(p => Seed17.CaveAt(p) == Cave.Floor), 0).SnowfallWeMPerMinute);
        Assert.Equal(Math.Clamp(sky.WindLevel * 1.2, 0, 1), Exposure.Surface(world, plain, 0).WindLevel, 12);
        // The regional air plus the tile's cold, never the person-felt wind chill.
        var proper = Seed17.PassStageIndex.Single(s => s.Value == 2).Key;
        Assert.Equal(sky.TemperatureF - 15, Exposure.Surface(world, proper, 0).AirTemperatureF, 12);
        Assert.Equal(sky.TemperatureF, Exposure.Surface(world, plain, 0).AirTemperatureF, 12);

        world.FixedWeather = Sky(WeatherCondition.Whiteout, -10, 1, wind: 1);
        world.Minute = 1440;
        Assert.Equal(0, Ground.SnowDepthM(world, plain), 9);
    }

    [Fact]
    public void BeforeTheFirstSubStep_NothingIsStored()
    {
        var world = World17(Sky(WeatherCondition.Clear, -10, 0)); // a pinned sky has no history before minute 0
        world.Minute = Ground.StepMinutes - 1;
        Ground.TraversalFactor(world, OpenTile(Terrain.Plain));
        Assert.Empty(world.Tiles);
        world.Minute = Ground.StepMinutes;
        Ground.TraversalFactor(world, OpenTile(Terrain.Plain));
        Assert.Equal(Ground.StepMinutes, Assert.Single(world.Tiles).Value.Ground!.LastUpdatedMinute);
    }

    [Fact]
    public void SeededWorld_OpensOnAMonthOfGround_NotFrozenThroughInJuly()
    {
        var world = World17(); // seeded: the ground has lived the June pre-roll
        var camp = Seed17.Camp;
        var ground = Ground.CatchUp(world, camp);
        Assert.Equal(0, ground.LastUpdatedMinute);
        Assert.Same(ground, world.Tiles[camp].Ground); // a month lived is worth keeping
        Assert.True(ground.Layers[0].FrozenFractionPct < 0.5, $"camp ground {ground.Layers[0].FrozenFractionPct:P0} frozen on July 1");
        AssertConserved(ground, Ground.Substrate(Seed17, camp));
    }

    [Fact]
    public void CatchUp_IsTheSameHoweverOftenTheTileIsLookedAt()
    {
        var tile = OpenTile(Terrain.Marsh);
        var often = World17();
        var once = World17();
        for (long m = 1; m <= 10 * 1440; m++) { often.Minute = m; if (m % 7 == 0) Ground.CatchUp(often, tile); }
        Ground.CatchUp(often, tile); // the last of them: 10 days is not a multiple of 7 minutes
        once.Minute = often.Minute;
        Ground.CatchUp(once, tile);
        Assert.Equal(Save.ToJson(once), Save.ToJson(often));
    }

    /// Lazy 15-minute sub-steps against minute-by-minute stepping (the old foreground tile), on
    /// the seed's real weather. Measured: see the report; bounds here have ~2x headroom.
    [Theory]
    [InlineData(Terrain.Plain)]
    [InlineData(Terrain.Forest)]
    [InlineData(Terrain.Marsh)]
    [InlineData(Terrain.Hills)]
    [InlineData(Terrain.Rock)]
    public void LazySubSteps_StayCloseToPerMinuteStepping(Terrain terrain)
    {
        var tile = OpenTile(terrain);
        var world = World17();
        var sub = Ground.Substrate(Seed17, tile);
        var reference = Ground.Baseline(sub);
        double maxSnowM = 0, maxWaterM = 0, maxTf = 0, maxHd = 0, maxWet = 0, peakSnowM = 0;
        for (long m = 1 + Ground.HistoryStartMinute(world); m <= 60 * 1440; m++) // the lazy tile lives the pre-roll too
        {
            Ground.Advance(reference, sub, 1, Exposure.Surface(world, tile, m));
            if (m % 1440 != 0 || m <= 0) continue;
            world.Minute = m;
            var lazy = Ground.CatchUp(world, tile);
            peakSnowM = Math.Max(peakSnowM, Ground.SnowDepthM(reference, sub));
            maxSnowM = Math.Max(maxSnowM, Math.Abs(Ground.SnowDepthM(lazy, sub) - Ground.SnowDepthM(reference, sub)));
            maxWaterM = Math.Max(maxWaterM, Math.Abs(Ground.StoredWaterM(lazy, sub) - Ground.StoredWaterM(reference, sub)));
            maxTf = Math.Max(maxTf, Math.Abs(Ground.TraversalFactor(lazy, sub) - Ground.TraversalFactor(reference, sub)));
            maxHd = Math.Max(maxHd, Math.Abs(Ground.HazardDelta(lazy, sub) - Ground.HazardDelta(reference, sub)));
            maxWet = Math.Max(maxWet, Math.Abs(Ground.ContactWettingRate(lazy, sub, SurfaceContact.Walking) - Ground.ContactWettingRate(reference, sub, SurfaceContact.Walking)));
            AssertConserved(lazy, sub);
        }
        string report = $"{terrain}: peak snow {peakSnowM:F3} m; max |d| snow {maxSnowM:E2} m, water {maxWaterM:E2} m, tf {maxTf:E2}, hd {maxHd:E4}, wet {maxWet:E2}/min";
        Console.WriteLine(report);
        // Snow and water track to a few millimetres over two months. The derived queries carry
        // threshold effects (a crust forming a sub-step earlier flips hazard by its whole 0.1), so
        // they are bounded by what a threshold flip costs, not by the integration error.
        Assert.True(maxSnowM < 0.03 && maxWaterM < 0.01, report);
        Assert.True(maxTf < 0.6 && maxHd <= 0.15 + 1e-9 && maxWet < 0.07, report); // hazard: crust (0.1) and first snow (0.05) can flip together
    }

    [Fact]
    public void Snow_SlowsACrossing_ByTheSurfaceFactorOnBothSegments()
    {
        var from = new TileId(2, 51);
        var to = new TileId(2, 52); // plain -> plain, the 5-minute floor when bare
        var calm = Sky(WeatherCondition.Clear, -10, 0, wind: 0, cloud: 0);
        var snowy = World17(Sky(WeatherCondition.HeavySnow, -10, 1), from);
        var bare = World17(calm, from);
        snowy.Minute = bare.Minute = 2 * 1440;
        var body = snowy.Agent(P).Body;
        int snowMinutes = Traversal.CrossingMinutes(snowy, body, from, to, Pace.Quick, calm);
        int bareMinutes = Traversal.CrossingMinutes(bare, body, from, to, Pace.Quick, calm);
        Assert.True(snowMinutes > bareMinutes + 5, $"{bareMinutes} -> {snowMinutes}");
        double expected = Traversal.SegmentMinutes(Seed17, from, Body.Speed(body), calm) * (Ground.TraversalFactor(snowy, from) - 1)
            + Traversal.SegmentMinutes(Seed17, to, Body.Speed(body), calm) * (Ground.TraversalFactor(snowy, to) - 1);
        int unfloored = Traversal.SegmentMinutes(Seed17, from, Body.Speed(body), calm) + Traversal.SegmentMinutes(Seed17, to, Body.Speed(body), calm);
        Assert.InRange(snowMinutes - unfloored, expected - 1, expected + 1);
    }

    /// old game: TravelRunner walked GetTraversalMinutes, which left the surface excess out, so snow
    /// slowed the preview but not the walk (bug, fixed). The walk lasts what the crossing costs.
    [Fact]
    public void ACrossingOverSnow_TakesItsCrossingMinutes_SurfaceIncluded()
    {
        var from = new TileId(2, 51);
        var to = new TileId(2, 52);
        var world = World17(Sky(WeatherCondition.HeavySnow, -10, 1), from);
        world.Minute = 2 * 1440;
        var body = world.Agent(P).Body;
        var sky = Weather.At(world, world.Minute);
        int expected = Traversal.CrossingMinutes(world, body, from, to, Pace.Careful, sky);
        double excess = Traversal.SurfaceExcessMinutes(world, from, Traversal.SegmentMinutes(Seed17, from, Body.Speed(body), sky));
        Assert.True(excess > 2, $"snow adds only {excess:F1} min");
        var r = Sim.Step(world, new Travel(P, Direction.South, Pace.Careful), 600);
        Assert.Equal(("arrived", expected), (r.InterruptedReason, r.MinutesElapsed));
    }

    [Fact]
    public void IcedOverStandingWater_IsAFootingHazard_ButNotBesideARiver()
    {
        var land = OpenTile(Terrain.Marsh);
        var river = First(p => Seed17.BesideRiver(p) && Seed17.TerrainAt(p) == Terrain.Marsh);
        var world = World17(Sky(WeatherCondition.Clear, -10, 0));
        foreach (var t in new[] { land, river })
        {
            var s = Ground.Baseline(Ground.Substrate(Seed17, t));
            Ground.AddWater(s, Ground.Substrate(Seed17, t), 0.08);
            world.Tiles[t] = new TileState { Ground = s };
        }
        world.Minute = 12 * 60;
        double bare = Map.TravelHazardLevel(Seed17, land);
        Assert.True(Traversal.EffectiveHazardLevel(world, land) > bare + 0.1, "verglas should be a hazard");
        Assert.Equal(0, Traversal.SurfaceHazardDelta(world, river));
    }

    static double WetAfter(World world, Activity a, int minutes)
    {
        Sim.Step(world, a, minutes);
        return Effects.Severity(world.Agent(P).Body, EffectKind.Wet);
    }

    static World Flooded(TileId tile, WeatherSample sky, double waterM)
    {
        var world = World17(sky, tile);
        var sub = Ground.Substrate(Seed17, tile);
        var s = Ground.Baseline(sub, 0);
        Ground.AddWater(s, sub, waterM);
        world.Tiles[tile] = new TileState { Ground = s };
        return world;
    }

    [Fact]
    public void StandingWater_WetsWhoeverStandsInIt_UnderARoofToo()
    {
        var sky = Sky(WeatherCondition.Cloudy, 5, 0);
        var marsh = OpenTile(Terrain.Marsh);
        double wading = WetAfter(Flooded(marsh, sky, 0.3), new Wait(P), 30);
        double dry = WetAfter(World17(sky, OpenTile(Terrain.Plain)), new Wait(P), 30);
        Assert.True(wading > 0.3, $"wading a flooded marsh should soak you, got {wading:F3}");
        Assert.True(dry < 0.05, $"dry ground, got {dry:F3}");

        // Lying in it: the contact with the longest reach.
        double lying = WetAfter(Flooded(marsh, sky, 0.3), new Sleep(P), 30);
        Assert.True(lying > 0.3);

        // A cave roof keeps rain off, not the water off the floor.
        var cave = First(p => Seed17.CaveAt(p) == Cave.Floor);
        Assert.True(WetAfter(Flooded(cave, sky, 0.05), new Wait(P), 10) > 0.02);
    }

    [Fact]
    public void GroundState_SurvivesSaveAndLoad_AndCarriesOnTheSame()
    {
        // Observed directly: seed 17 opens in heavy snow, and an agent with no fire would not live the days.
        var world = World.New(17);
        var tiles = Enumerable.Range(0, 5).Select(i => new TileId(Seed17.Camp.X + i, Seed17.Camp.Y)).ToList();
        world.Minute = 3 * 1440;
        foreach (var t in tiles) Ground.CatchUp(world, t);
        Assert.Contains(world.Tiles.Values, t => t.Ground != null);
        var loaded = Save.FromJson(Save.ToJson(world));
        world.Minute = loaded.Minute = 4 * 1440 + 7;
        foreach (var t in tiles) { Ground.CatchUp(world, t); Ground.CatchUp(loaded, t); }
        Assert.Equal(Save.ToJson(world), Save.ToJson(loaded));
    }

    /// A tile first looked at on day 30 simulates 30 days from its baseline. Must stay cheap, and
    /// a few hundred observed tiles must keep the save small (plan decision 1).
    [Fact]
    public void FirstObservationOnDay30_IsCheap_AndThreeHundredTilesKeepTheSaveSmall()
    {
        var world = World.New(17);
        world.Minute = 30 * 1440;
        var tiles = Enumerable.Range(0, 300).Select(i => new TileId(5 + i % 60, 30 + i / 60)).ToList();
        Ground.CatchUp(world, new TileId(4, 30)); // JIT and the weather timeline
        var watch = Stopwatch.StartNew();
        Ground.CatchUp(world, tiles[0]);
        double oneMs = watch.Elapsed.TotalMilliseconds;
        foreach (var t in tiles.Skip(1)) Ground.CatchUp(world, t);
        double allMs = watch.Elapsed.TotalMilliseconds;
        int bytes = Save.ToJson(world).Length;
        Console.WriteLine($"day-30 first observation: one tile {oneMs:F2} ms, 300 tiles {allMs:F0} ms, save {bytes / 1024} KB");
        Assert.True(oneMs < 50, $"one tile took {oneMs:F2} ms");
        Assert.True(bytes < 1024 * 1024, $"save is {bytes} bytes");
    }

    [Fact]
    public void TheActingAgentsTile_IsCaughtUpEveryMinute()
    {
        var world = World.New(17);
        var watch = Stopwatch.StartNew();
        var result = Sim.Step(world, new Wait(P), 30 * 1440);
        Console.WriteLine($"wait: {result.MinutesElapsed} min ({result.InterruptedReason}) in {watch.ElapsedMilliseconds} ms, save {Save.ToJson(world).Length / 1024} KB");
        Assert.True(result.MinutesElapsed > 60, "the agent should live long enough to watch the ground");
        // The agent's own tile is observed every minute it stands there.
        Assert.Equal(world.Minute / Ground.StepMinutes * Ground.StepMinutes, world.Tiles[Map.Of(world).Camp].Ground!.LastUpdatedMinute);
    }

    /// Output of the old-code harness (golden-ground/Program.cs).
    const string OldGolden = """
batch-seq-snow|Ground:0.3:0:0.3:1;Snow:0.31988250964630066:0.47115602322061095:0:0;Snow:0.30495439884600334:0.2960507718571081:0:0;Snow:0.4604035511885626:0.10909934723885269:0:0|add=0.27599999999999963|drn=0|evp=0.00040319999999999945|ovf=0|tf=6|hd=0.05|sf=0.09999999999999998|cww=0|cwr=0|cwk=0
batch-seq-thaw|Ground:0.3:0:0.873973907683327:0.34325967556081904;Snow:0.31860575963802984:0.7415583672384389:0.09988801222698711:0|add=0.27599999999999963|drn=0.014582453530868114|evp=0.0015491999999999954|ovf=0|tf=1.6677228568510225|hd=0.05|sf=0.09999999999999998|cww=0.03944384344609996|cwr=0.032869869538416634|cwk=0.01972192172304998
batch-seq-freeze|Ground:0.3:0:0.73772377689818:0.48489344069039364;Snow:0.2921663770247698:0.8274614347554023:0.11777663728925428:1|add=0.27599999999999963|drn=0.030932469225085756|evp=0.0018083999999999965|ovf=0|tf=1.5490009580080626|hd=0.15000000000000002|sf=0.12350086892569068|cww=0.002280038138633455|cwr=0.001900031782194546|cwk=0.0011400190693167274
batch-rain-loam|Ground:0.3:0:0.9922:0;Water:0.06715387679168223:0:1:0|add=0.2760000000000002|drn=0.07131063219140017|evp=9.900000000000012E-05|ovf=0.01837249101691738|tf=1.3221421558025477|hd=0.024805|sf=0.7985383696249533|cww=0.059676301433345784|cwr=0.04973025119445482|cwk=0.029838150716672892
batch-dry-loam|Ground:0.3:0:0.4694341295459439:0|add=0.2760000000000002|drn=0.18480122600513033|evp=0.0013596000000000068|ovf=0.03350707844935658|tf=1.058679266193243|hd=0.011735853238648598|sf=1|cww=0.0028166047772756634|cwr=0.0023471706477297197|cwk=0.0014083023886378317
batch-frz-loam|Ground:0.3:0:0.4522314770025381:0.44693571827346623|add=0.2760000000000002|drn=0.18673594431033896|evp=0.0014892000000000074|ovf=0.03350707844935658|tf=1.0312641346253173|hd=0.006252826925063456|sf=1|cww=0.0015006784620152293|cwr=0.0012505653850126914|cwk=0.0007503392310076147
batch-rain-forestFloor|Ground:0.35:0:0.9910000000000001:0;Water:0.03614153503390033:0:1:0|add=0.2951250000000002|drn=0.09848197463744691|evp=6.840000000000006E-05|ovf=0.004350590328653463|tf=1.2169228396876437|hd=0.022297500000000005|sf=0.891575394898299|cww=0.03485922802712027|cwr=0.029049356689266892|cwk=0.017429614013560135
batch-dry-forestFloor|Ground:0.35:0:0.4438595340797156:0|add=0.2951250000000002|drn=0.21712264083405997|evp=0.0013290000000000066|ovf=0.0067654825483864575|tf=1.049934197583968|hd=0.009986839516793602|sf=1|cww=0.002663157204478294|cwr=0.0022192976703985785|cwk=0.001331578602239147
batch-frz-forestFloor|Ground:0.35:0:0.4393326480355696:0.35052020858714666|add=0.2951250000000002|drn=0.21770602538601322|evp=0.0014586000000000072|ovf=0.0067654825483864575|tf=1.0321004886182874|hd=0.006420097723657462|sf=1|cww=0.0017120260596419897|cwr=0.0014266883830349915|cwk=0.0008560130298209948
batch-rain-slope|Ground:0.2:0:0.9718749999999999:0;Water:0.00817807607902226:0:1:0|add=0.25200000000000017|drn=0.14055361462812532|evp=0.00011880000000000015|ovf=0.0448370092928523|tf=1.0728906249999999|hd=0.014578124999999997|sf=0.9754657717629333|cww=0.01237371086321781|cwr=0.010311425719348176|cwk=0.006186855431608905
batch-dry-slope|Ground:0.2:0:0.2451917665735211:0|add=0.25200000000000017|drn=0.18989357880747904|evp=0.001379400000000007|ovf=0.046015515198109716|tf=1.0183893824930141|hd=0.003677876498602816|sf=1|cww=0.0014711505994411266|cwr=0.0012259588328676057|cwk=0.0007355752997205633
batch-frz-slope|Ground:0.2:0:0.24393176657352128:1|add=0.25200000000000017|drn=0.18989357880747904|evp=0.0014550000000000073|ovf=0.046015515198109716|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
batch-rain-rock|Ground:0.08:0:0.9595:0;Water:0.01195139811459289:0:1:0|add=0.24096000000000017|drn=0.031725107115857167|evp=0.00017279999999999986|ovf=0.18789949476955006|tf=1.0437384749231755|hd=0.0023987500000000003|sf=0.9641458056562213|cww=0.015318118491674311|cwr=0.012765098743061927|cwk=0.0076590592458371555
batch-dry-rock|Ground:0.08:0:0.03623479295765552:0|add=0.24096000000000017|drn=0.04380609256780492|evp=0.0014334000000000072|ovf=0.19537265341980173|tf=1.0004529349119706|hd=9.05869823941388E-05|sf=1|cww=0.0002174087577459331|cwr=0.0001811739647882776|cwk=0.00010870437887296655
batch-frz-rock|Ground:0.08:0:0.03623479295765552:1|add=0.24096000000000017|drn=0.04380609256780492|evp=0.0014334000000000072|ovf=0.19537265341980173|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
batch-rain-peat|Ground:0.5:0:0.9998875:0;Water:0.18033021430978996:0:1:0|add=0.4950000000000002|drn=0.00222915716842121|evp=0.00017279999999999986|ovf=0.012301578521787291|tf=1.841593124606194|hd=0.04999437500000001|sf=0.45900935707063006|cww=0.12|cwr=0.1|cwk=0.06
batch-dry-peat|Ground:0.5:0:0.9998875:0;Water:0.1284481158677981:0:1:0|add=0.4950000000000002|drn=0.008709157168421218|evp=0.0014400000000000072|ovf=0.056436476963780104|tf=1.6713800960800231|hd=0.04999437500000001|sf=0.6146556523966056|cww=0.10875781769423849|cwr=0.09063151474519876|cwk=0.054378908847119246
batch-frz-peat|Ground:0.5:0:0.9998875:0;Water:0.11870225661261924:0:1:0.20432811213650945|add=0.4950000000000002|drn=0.010329157168421256|evp=0.0015696000000000078|ovf=0.06443273621895931|tf=1.5598338288435873|hd=0.2645388927433349|sf=0.6373073001817715|cww=0.0815577638900954|cwr=0.06796480324174618|cwk=0.0407788819450477
batch-rain-iceSheet|Ground:0.05:0:0:0|add=0.24000000000000016|drn=0|evp=0|ovf=0.24000000000000016|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
batch-dry-iceSheet|Ground:0.05:0:0:0|add=0.24000000000000016|drn=0|evp=0|ovf=0.24000000000000016|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
batch-frz-iceSheet|Ground:0.05:0:0:0|add=0.24000000000000016|drn=0|evp=0|ovf=0.24000000000000016|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
pm-seq-snow|Ground:0.3:0:0.3:1;Snow:0.3230987718037584:0.4688311083151518:0:0;Snow:0.31248055083686554:0.29126054193679213:0:0;Snow:0.4548325058175723:0.10462367999079975:0:0|add=0.2759999999999862|drn=0|evp=0.0004031999999999852|ovf=0|tf=6|hd=0.05|sf=0.09999999999999998|cww=0|cwr=0|cwk=0
pm-seq-thaw|Ground:0.3:0:0.8741046203372885:0.3432083448823786;Snow:0.3188637695051693:0.7407787035100721:0.10111488883671006:0|add=0.2759999999999862|drn=0.014329121472816956|evp=0.001554800000000013|ovf=0|tf=1.6689099460279688|hd=0.05|sf=0.09999999999999998|cww=0.03944462772202372|cwr=0.03287052310168644|cwk=0.01972231386101186
pm-seq-freeze|Ground:0.3:0:0.7376997693102456:0.484335208367056;Snow:0.2923518922182824:0.8268036945210682:0.11925232611428577:1|add=0.2759999999999862|drn=0.030697703596062238|evp=0.001814000000000035|ovf=0|tf=1.549818959079736|hd=0.15000000000000002|sf=0.12294432334515282|cww=0.002282434786974232|cwr=0.0019020289891451938|cwk=0.001141217393487116
pm-rain-loam|Ground:0.3:0:0.9994799999999999:0;Water:0.0672951844051391:0:1:0|add=0.27600000000000285|drn=0.07077962086807202|evp=9.816000000000134E-05|ovf=0.017889434726783456|tf=1.3236297723590424|hd=0.024987|sf=0.7981144467845827|cww=0.05983302752411128|cwr=0.049860856270092735|cwk=0.02991651376205564
pm-dry-loam|Ground:0.3:0:0.47192674369961696:0|add=0.27600000000000285|drn=0.18480123480573404|evp=0.0013649199999999704|ovf=0.03320263595030066|tf=1.0589908429624522|hd=0.011798168592490424|sf=1|cww=0.002831560462197702|cwr=0.002359633718498085|cwk=0.001415780231098851
pm-frz-loam|Ground:0.3:0:0.4536736723036846:0.44551494243356415|add=0.27600000000000285|drn=0.18686200337324405|evp=0.0014945199999999814|ovf=0.03320263595030066|tf=1.0314444090379606|hd=0.006288881807592123|sf=1|cww=0.0015093316338221094|cwr=0.0012577763615184247|cwk=0.0007546658169110547
pm-rain-forestFloor|Ground:0.35:0:0.9994000000000001:0;Water:0.03591679950153079:0:1:0|add=0.2951250000000005|drn=0.09758393631925687|evp=6.683999999999987E-05|ovf=0.004151924179214359|tf=1.2173013059356603|hd=0.022486500000000003|sf=0.8922496014954077|cww=0.03472983960122463|cwr=0.028941533001020526|cwk=0.017364919800612316
pm-dry-forestFloor|Ground:0.35:0:0.4452284528701035:0|add=0.2951250000000005|drn=0.217050567644482|evp=0.00133359999999997|ovf=0.006617351028473482|tf=1.0500882009478867|hd=0.010017640189577329|sf=1|cww=0.002671370717220621|cwr=0.0022261422643505176|cwk=0.0013356853586103106
pm-frz-forestFloor|Ground:0.35:0:0.44024242748196274:0.3497958438703188|add=0.2951250000000005|drn=0.21770626664311252|evp=0.001463199999999981|ovf=0.006617351028473482|tf=1.0322028388060065|hd=0.00644056776120132|sf=1|cww=0.001717484736320352|cwr=0.00143123728026696|cwk=0.000858742368160176
pm-rain-slope|Ground:0.2:0:0.9981249999999999:0;Water:0.008927967684104154:0:1:0|add=0.2520000000000055|drn=0.13940886562803806|evp=0.00011676000000000221|ovf=0.043658906687857414|tf=1.074859375|hd=0.014971874999999999|sf=0.9732160969476875|cww=0.013131124147283322|cwr=0.010942603456069436|cwk=0.006565562073641661
pm-dry-slope|Ground:0.2:0:0.24560140502917094:0|add=0.2520000000000055|drn=0.19061474226645975|evp=0.0013835199999999708|ovf=0.045265653431786665|tf=1.0184201053771877|hd=0.0036840210754375637|sf=1|cww=0.0014736084301750255|cwr=0.0012280070251458547|cwk=0.0007368042150875128
pm-frz-slope|Ground:0.2:0:0.2442964050291661:1|add=0.2520000000000055|drn=0.19061474226645975|evp=0.0014618199999999774|ovf=0.045265653431786665|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
pm-rain-rock|Ground:0.08:0:0.9973:0;Water:0.013921747277157065:0:1:0|add=0.24096000000000525|drn=0.031419984282672016|evp=0.0001727999999999991|ovf=0.18587138844017165|tf=1.049461786113257|hd=0.0024932500000000002|sf=0.9582347581685288|cww=0.017121197821725654|cwr=0.014267664851438044|cwk=0.008560598910862827
pm-dry-rock|Ground:0.08:0:0.03880917479309171:0|add=0.24096000000000525|drn=0.04419614962011229|evp=0.0014395599999999676|ovf=0.19495172230187444|tf=1.0004851146849136|hd=9.70229369827293E-05|sf=1|cww=0.00023285504875855028|cwr=0.0001940458739654586|cwk=0.00011642752437927514
pm-frz-rock|Ground:0.08:0:0.0386029247930917:1|add=0.24096000000000525|drn=0.04419614962011229|evp=0.0014415399999999677|ovf=0.19495172230187444|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
pm-rain-peat|Ground:0.5:0:0.9999925000000001:0;Water:0.1804504123947491:0:1:0|add=0.4949999999999736|drn=0.00221311181328546|evp=0.0001727999999999991|ovf=0.012165925791973158|tf=1.8420261490128953|hd=0.049999625000000006|sf=0.45864876281575273|cww=0.12|cwr=0.1|cwk=0.06
pm-dry-peat|Ground:0.5:0:0.9999925000000001:0;Water:0.12856169960354968:0:1:0|add=0.4949999999999736|drn=0.008693111813285113|evp=0.0014399999999999676|ovf=0.05630743858325336|tf=1.6717878453444894|hd=0.049999625000000006|sf=0.614314901189351|cww=0.10884931468283975|cwr=0.0907077622356998|cwk=0.05442465734141987
pm-frz-peat|Ground:0.5:0:0.9999925000000001:0;Water:0.11879595789974004:0:1:0.20416694666050605|add=0.4949999999999736|drn=0.010313111813285484|evp=0.0015695999999999785|ovf=0.06432358028707814|tf=1.560174004550903|hd=0.2643749189935313|sf=0.6370261963204091|cww=0.08163335491979208|cwr=0.06802779576649341|cwk=0.04081667745989604
pm-rain-iceSheet|Ground:0.05:0:0:0|add=0.24000000000000518|drn=0|evp=0|ovf=0.24000000000000518|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
pm-dry-iceSheet|Ground:0.05:0:0:0|add=0.24000000000000518|drn=0|evp=0|ovf=0.24000000000000518|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
pm-frz-iceSheet|Ground:0.05:0:0:0|add=0.24000000000000518|drn=0|evp=0|ovf=0.24000000000000518|tf=1|hd=0|sf=1|cww=0|cwr=0|cwk=0
compact|Ground:0.3:0:0.3:0.07689283629609212;Snow:0.0530466722458564:0.6139742705561418:0:0|add=0.056000000000000015|drn=0|evp=3.36E-05|ovf=0|tf=1.0726205662863404|hd=0.05|sf=0.8408599832624308|cww=0.0016615928946670342|cwr=0.001384660745555862|cwk=0.0008307964473335171
flooded|Ground:0.5:0:0.8501666583333334:0;Water:0.299919905:0:1:0|add=0.5549999999999999|drn=2.4999999999997246E-09|evp=1E-07|ovf=2.9995E-05|tf=2.167043976526575|hd=0.04250833291666667|sf=0.10024028499999993|cww=0.12|cwr=0.1|cwk=0.06
glazed|Ground:0.5:0:0.9486391154217391:0;Water:0.253661424248126:0:1:0.050376441896424505|add=0.5549999999999999|drn=0.00045826537347726476|evp=7.210000000000007E-05|ovf=0.016216475751875282|tf=2.019339807892903|hd=0.10032721976233268|sf=0.2355458674519143|cww=0.12|cwr=0.1|cwk=0.06
search30|0.09999999999999998|0.5141344219705644|0.09999999999999998
""";
}
