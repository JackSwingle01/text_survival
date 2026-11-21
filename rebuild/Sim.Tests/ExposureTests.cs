namespace Survival.Tests;

/// One exposure calculation (emergent-world-design §3.4, decision 2026-11-21): the wind at a body is
/// regional x tile x (1 - pit walls) x (1 - shelter), and wind chill, convection and drying read that
/// one value. Old game: the tile's wind factor reached the wind chill and nothing else.
public class ExposureTests
{
    static readonly AgentId P = AgentId.Player;
    const int Digits = 12;
    /// A snowy wind at night (no sun): what a person waiting out in it feels.
    static readonly WeatherSample SnowyWind = new(0, WeatherCondition.LightSnow, -10, 0.6, 0.5, 0.9, WindDirection.North);

    /// Seed 13 has a Wind Gap (its 2.5 is the windiest place there is).
    const int Seed = 13;

    static World Night(WeatherSample sky)
    {
        var w = World.New(Seed);
        w.FixedWeather = sky;
        w.Minute = 960; // Jul 2 01:00
        return w;
    }

    static TileId Find(World w, Func<GeneratedMap, TileId, bool> pick)
    {
        var m = Map.Of(w);
        return (from x in Enumerable.Range(0, m.Width) from y in Enumerable.Range(0, m.Height)
                let t = new TileId(x, y) where pick(m, t) select t).First();
    }

    /// An ordinary tile of this terrain: not camp, a cave, a named site or beside a river.
    static TileId Ordinary(World w, Terrain terrain) => Find(w, (m, t) =>
        m.TerrainAt(t) == terrain && t != m.Camp && m.CaveAt(t) == Cave.None && m.SiteAt(t) == null && !m.BesideRiver(t));

    static TileId WindGap(World w) => Find(w, (m, t) => m.SiteAt(t)?.Name == "Wind Gap");

    static SurvivalContext Waiting(World w, TileId at)
    {
        w.Agent(P).Tile = at;
        return Sim.Surroundings(w, w.Agent(P), new Wait(P), []);
    }

    // ---- the body's wind follows the tile (fails on the old wiring: every tile gave the sky's 0.6) ----

    [Fact]
    public void DenseForest_CalmsTheBodysWind_BelowTheOpenValley()
    {
        var w = Night(SnowyWind);
        double forest = Waiting(w, Ordinary(w, Terrain.Forest)).WindLevel, plain = Waiting(w, Ordinary(w, Terrain.Plain)).WindLevel;
        Assert.True(forest < plain - 0.1, $"forest {forest:F3} vs open plain {plain:F3}");
        Assert.Equal(0.6 * 0.3, forest, Digits);
    }

    [Fact]
    public void WindGap_RaisesTheBodysWind_AboveTheOpenValley()
    {
        var w = Night(SnowyWind);
        double gap = Waiting(w, WindGap(w)).WindLevel, plain = Waiting(w, Ordinary(w, Terrain.Plain)).WindLevel;
        Assert.True(gap > plain + 0.1, $"Wind Gap {gap:F3} vs open plain {plain:F3}");
        Assert.Equal(0.6 * 2.5, gap, Digits);
    }

    /// The convection the wind drives follows it: the same body loses heat faster through the same
    /// clothes in the Wind Gap's wind than in the forest's, the air held equal.
    [Fact]
    public void ConvectiveLoss_FollowsTheTilesWind()
    {
        var w = Night(SnowyWind);
        var body = w.Agent(P).Body;
        var forest = Waiting(w, Ordinary(w, Terrain.Forest));
        var gap = Waiting(w, WindGap(w)) with { AirTempF = forest.AirTempF };
        Assert.True(Body.TotalThermalResistance(body, forest) > Body.TotalThermalResistance(body, gap) + 0.01);
        Assert.True(Body.TemperatureChangeFPerHour(body, gap) < Body.TemperatureChangeFPerHour(body, forest) - 0.1);
    }

    /// The wind chill in the air and the convection read the same wind (old: the chill read the tile's,
    /// the convection the sky's). No sun at night and no precipitation, so the air is the chill alone.
    [Theory]
    [InlineData("forest")]
    [InlineData("plain")]
    [InlineData("wind gap")]
    [InlineData("camp")]
    public void WindChill_AndConvection_ReadTheSameWind(string place)
    {
        var w = Night(SnowyWind with { Rolled = WeatherCondition.Cloudy, PrecipitationPct = 0 });
        var m = Map.Of(w);
        var tile = place switch
        {
            "forest" => Ordinary(w, Terrain.Forest),
            "plain" => Ordinary(w, Terrain.Plain),
            "wind gap" => WindGap(w),
            _ => m.Camp,
        };
        Shelters.Remove(w, m.Camp); // the camp's overhang is the shelter step's, not this
        var ctx = Waiting(w, tile);
        double skyF = Weather.At(w, w.Minute).TemperatureF + Map.TemperatureDeltaF(m, tile);
        Assert.Equal(Weather.WindChillF(skyF, ctx.WindLevel * Weather.MaxWindMph), ctx.AirTempF, Digits);
    }

    /// A sealed snow shelter in the windiest place there is still leaves the body no wind.
    [Fact]
    public void SealedSnowShelter_InTheWindGap_LeavesTheBodyNoWind()
    {
        var w = Night(SnowyWind);
        var gap = WindGap(w);
        Shelters.Place(w, gap, new ShelterDef("Snow shelter", ShelterKind.SnowShelter, Wind: 1.0, WindCap: 1.0));
        Assert.True(Waiting(w, gap).WindLevel < 0.01);
    }

    // ---- what Exposure.At holds ----

    [Fact]
    public void Exposure_AtABody_IsWhatTheBodyReads()
    {
        var w = Night(SnowyWind);
        var tile = WindGap(w);
        w.Agent(P).Tile = tile;
        var e = Exposure.At(w, tile, Cover.Open(P), SurfaceContact.Walking, 0);
        var ctx = Sim.Surroundings(w, w.Agent(P), new Wait(P), []);
        Assert.Equal(e.AirF, ctx.AirTempF);
        Assert.Equal(e.WindLevel, ctx.WindLevel);
        Assert.Equal(e.FireDryingWindLevel, ctx.FireDryingWindLevel);
        Assert.Equal(e.OverheadCoverLevel, ctx.OverheadCoverLevel);
        Assert.Equal(e.GroundWettingPctPerMinute, ctx.GroundWettingPctPerMinute);
        // The ground and a carcass feel the air itself: no chill. The Wind Gap is 8 °F colder than the sky.
        Assert.Equal(SnowyWind.TemperatureF - 8, e.SurfaceAirF, Digits);
        Assert.Equal(Exposure.Surface(w, tile, w.Minute).AirTemperatureF, e.SurfaceAirF);
        Assert.True(e.AirF < e.SurfaceAirF - 20);
    }

    /// Drying beside a fire reads the tile's wind past the pit's walls, without the shelter's.
    [Fact]
    public void FireDryingWind_OmitsTheShelterOnly()
    {
        var w = Night(SnowyWind);
        var tile = Ordinary(w, Terrain.Plain);
        var cover = new ShelterCover(0.3, 0.3, 0.5);
        var e = Exposure.At(w, tile, Cover.In(cover));
        Assert.Equal(0.6 * 1.2 * (1 - 0.5), e.WindLevel, Digits);
        Assert.Equal(0.6 * 1.2, e.FireDryingWindLevel, Digits);
    }

    [Theory]
    [InlineData(CoverKind.Packed)]
    [InlineData(CoverKind.Worn)]
    [InlineData(CoverKind.UnderSnow)]
    public void CoverForThings_IsNamed_ButNotBuiltYet(CoverKind kind)
    {
        var w = Night(SnowyWind);
        var cover = kind switch { CoverKind.Packed => Cover.Packed(P), CoverKind.Worn => Cover.Worn(P), _ => Cover.UnderSnow() };
        Assert.Throws<NotSupportedException>(() => Exposure.At(w, w.Agent(P).Tile, cover));
    }

    // ---- where the wind factor is exactly 1, nothing changed ----

    /// Captured from the old wiring (f0223c0) on an ordinary Rock tile (wind factor 1), bit for bit: no
    /// shelter, with and without an open pit burning beside you. Where the tile lets the whole wind
    /// through, tile wind reaching the body changes nothing.
    [Theory]
    [InlineData(false, -8.243078392479587, 0.6, 0.6, 0, -3.645589109217371, 91.5126325335029, 0.3240000000000002)]
    // rebuild: shared burn (2026-11-21): the bark and kindling go first, so a little more birch burns at minute
    // 30 and this row is no longer the old wiring's. Old → new: air 3.576741759363495 → 3.6852252553580183, fire
    // 58.22737464236477 → 58.758882665518485, rate -0.21952233102853913 → -0.1882198115126869, core
    // 98.58811999999998 → 98.59615249999999, wet 0.04319999999999998 → 0.024300000000000006. The no-fire row still is.
    [InlineData(true, 3.6852252553580183, 0.6, 0.6, 58.758882665518485, -0.1882198115126869, 98.59615249999999, 0.024300000000000006)]
    public void FactorOneTile_IsBitForBitTheOldWiring(bool fire, double airF, double wind, double dryingWind, double fireBonusF,
        double rateFPerHour, double coreAfter4hF, double wetAfter4h)
    {
        var w = Night(SnowyWind);
        var rock = Ordinary(w, Terrain.Rock);
        Assert.Equal(1.0, Map.WindFactor(Map.Of(w), rock));
        w.Agent(P).Tile = rock;
        if (fire)
        {
            var f = new FireState { Tile = rock, LastUpdatedMinute = 0, Pit = PitType.Open };
            Fire.AddFuel(f, Resource.BirchBark, 0.05);
            Fire.AddFuel(f, Resource.Kindling, 1.0);
            Fire.IgniteAll(f);
            Fire.AddFuel(f, Resource.Birch, 3.0);
            var events = new List<WorldEvent>();
            for (int i = 1; i <= 30; i++) Fire.Tick(f, new FireId(0), i, events);
            f.LastUpdatedMinute = w.Minute;
            w.Fires[new FireId(w.NextFireId++)] = f;
        }
        var ctx = Sim.Surroundings(w, w.Agent(P), new Wait(P), []);
        Assert.Equal(airF, ctx.AirTempF);
        Assert.Equal(wind, ctx.WindLevel);
        Assert.Equal(dryingWind, ctx.FireDryingWindLevel);
        Assert.Equal(fireBonusF, ctx.FireBonusF);
        Assert.Equal(rateFPerHour, Body.TemperatureChangeFPerHour(w.Agent(P).Body, ctx));
        Sim.Step(w, new Wait(P), 240);
        Assert.Equal(coreAfter4hF, w.Agent(P).Body.CoreTempF);
        Assert.Equal(wetAfter4h, Effects.Severity(w.Agent(P).Body, EffectKind.Wet));
    }
}
