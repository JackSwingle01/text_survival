namespace Survival;

public enum CoverKind
{
    Open,
    Sheltered,
    /// In a carrier's pack: the carrier's exposure under the pack's cover.
    Packed,
    /// Worn: the wearer's body's exposure.
    Worn,
    UnderSnow,
}

/// Shelter: the levels a shelter gives (default unless Sheltered). Holder: whose torch is in hand (the body, or a
/// pack's carrier); null for a thing on the ground.
public readonly record struct Cover(CoverKind Kind, ShelterCover Shelter = default, AgentId? Holder = null)
{
    public static Cover Open(AgentId? holder = null) => new(CoverKind.Open, default, holder);
    /// No shelter (or someone on the move) is the open.
    public static Cover In(ShelterCover shelter, AgentId? holder = null) =>
        shelter == default ? Open(holder) : new(CoverKind.Sheltered, shelter, holder);
    public static Cover Packed(AgentId carrier) => new(CoverKind.Packed, default, carrier);
    public static Cover Worn(AgentId wearer) => new(CoverKind.Worn, default, wearer);
    public static Cover UnderSnow() => new(CoverKind.UnderSnow);
}

/// Weather as the ground feels it: no body, fire or shelter. Not what a person feels: wind chill, clothing and fire melt no snow.
public readonly record struct SurfaceWeather(
    double AirTemperatureF, double WindLevel, double SunlightLevel, double SnowfallWeMPerMinute, double RainfallMPerMinute);

/// What the world is doing to a body or thing in one place right now (GLOSSARY: Exposure).
/// SurfaceAirF: the sky's air with the tile's own cold, what snow and meat feel.
/// AirBeforeFireF: the air a body feels before any fire (wind chill, sun, precipitation, shelter step).
/// AirF: that air plus the fire's warming (Warmth.AirHeatF). Radiant heat (PitHeatF, ShelterHeatF, TorchHeatF) is
/// the activity's to take, by how close it keeps the body.
/// WindLevel: the one wind, read by wind chill, convection and drying alike.
/// FireDryingWindLevel: wind drying beside a fire, omitting the shelter's walls.
/// OverheadCoverLevel: the tile's overhead or the shelter's roof, whichever is more.
public sealed record Exposure(
    WeatherSample Sky,
    double SurfaceAirF,
    double AirBeforeFireF,
    double AirF,
    double WindLevel,
    double FireDryingWindLevel,
    Fire.Warmth Warmth,
    double ShelterInsulationLevel,
    double OverheadCoverLevel,
    double SunlightLevel,
    double GroundWettingPctPerMinute)
{
    public Precipitation Precipitation => Sky.Precipitation;
    public double PrecipitationPct => Sky.PrecipitationPct;

    /// contact and wetnessPct: how a body touches the ground and how wet it is; without a contact the ground wets nothing.
    /// Nothing else in the sim combines the sky with the tile, a shelter or a fire for a body (ArchitectureTests).
    public static Exposure At(World world, TileId tile, Cover cover, SurfaceContact? contact = null, double wetnessPct = 0)
    {
        if (cover.Kind is CoverKind.Packed or CoverKind.Worn or CoverKind.UnderSnow)
            throw new NotSupportedException($"Exposure under {cover.Kind} cover comes with effects on things");
        var m = Map.Of(world);
        var sky = Weather.At(world, world.Minute);
        var fires = Fire.Around(world, tile, cover.Holder);
        double overhead = Map.OverheadCover(m, tile), deltaF = Map.TemperatureDeltaF(m, tile), tileWind = Map.WindFactor(m, tile);
        double wind = WindAt(sky, tileWind, fires.WallsLevel, cover.Shelter.WindLevel);
        var (beforeFireF, insulation) = ShelterStep(FeltAirF(sky, wind, overhead, deltaF), sky, cover.Shelter.InsulationLevel, wind, overhead);
        var warmth = Fire.WarmthAgainst(fires, beforeFireF, insulation);
        return new Exposure(
            Sky: sky,
            SurfaceAirF: SiteAirF(sky, deltaF),
            AirBeforeFireF: beforeFireF,
            AirF: beforeFireF + warmth.AirHeatF,
            WindLevel: wind,
            // Drying at a fire reads the wind past the pit's walls, not the shelter's.
            FireDryingWindLevel: WindAt(sky, tileWind, fires.WallsLevel, 0),
            Warmth: warmth,
            ShelterInsulationLevel: insulation,
            // A roof over someone keeping still, or the sky's own cover, whichever is more.
            OverheadCoverLevel: Math.Max(overhead, cover.Shelter.OverheadLevel),
            SunlightLevel: sky.SunlightLevel * (1 - overhead),
            GroundWettingPctPerMinute: contact is SurfaceContact c ? Ground.ContactWettingRate(world, tile, c, wetnessPct) : 0);
    }

    /// The one wind at a body or thing: regional wind reaching this tile, less a pit's walls and a shelter's.
    /// 1 is a 45 mph gale; a funnelled tile can exceed it. Convection and drying read it, not only wind chill.
    public static double WindAt(WeatherSample sky, double tileWindFactor, double pitWallsLevel, double shelterWindLevel) =>
        sky.WindLevel * tileWindFactor * (1 - pitWallsLevel) * (1 - shelterWindLevel);

    /// The sky's air with the tile's own cold (pass stages, Wind Gap); the one place the two are added.
    public static double SiteAirF(WeatherSample sky, double temperatureDeltaF) => sky.TemperatureF + temperatureDeltaF;

    /// Minute-addressable so lazy catch-ups (ground, snow shelter melt) read the same weather however often run.
    /// A whiteout deposits nothing. The ground's wind is capped at a gale; a body's (WindAt) is not.
    public static SurfaceWeather Surface(World world, TileId tile, long minute)
    {
        var m = Map.Of(world);
        var s = Weather.At(world, minute);
        double exposure = 1 - Map.OverheadCover(m, tile);
        return new SurfaceWeather(
            AirTemperatureF: SiteAirF(s, Map.TemperatureDeltaF(m, tile)),
            WindLevel: Math.Clamp(s.WindLevel * Map.WindFactor(m, tile), 0, 1),
            SunlightLevel: s.SunlightLevel * exposure,
            SnowfallWeMPerMinute: s.SnowfallWeMPerMinute * exposure,
            RainfallMPerMinute: s.RainfallMPerMinute * exposure);
    }

    /// windLevel is WindAt's. The defaults are a fully exposed tile.
    public static double FeltAirF(WeatherSample s, double windLevel, double overheadCoverLevel = 0, double temperatureDeltaF = 0)
    {
        double t = SiteAirF(s, temperatureDeltaF);
        // The threshold reads the regional wind; the chill reads what reaches the body.
        if (s.WindLevel > 0.1) t = Weather.WindChillF(t, windLevel * Weather.MaxWindMph);
        double exposure = 1 - overheadCoverLevel;
        t += s.SunlightLevel * exposure * 15 * Math.Clamp((50 - t) / 30, 0.5, 1); // sun is felt more in the cold
        t -= s.PrecipitationPct * exposure * 5;
        return t;
    }

    public static double OpenAirF(World world, TileId tile, WeatherSample s) => Walking(world, tile, s).AirF;

    /// No fire or shelter: a walker keeps still under neither and leaves a fire's warmth at the first step.
    /// What an NPC forecasts a walk by.
    public static (double AirF, double WindLevel) Walking(World world, TileId tile, WeatherSample s)
    {
        var m = Map.Of(world);
        double wind = OpenWindLevel(m, tile, s);
        return (FeltAirF(s, wind, Map.OverheadCover(m, tile), Map.TemperatureDeltaF(m, tile)), wind);
    }

    public static double OpenWindLevel(GeneratedMap m, TileId tile, WeatherSample s) => WindAt(s, Map.WindFactor(m, tile), 0, 0);

    /// Holds the air toward a 40 F floor (what body heat keeps a good shelter at) by insulation (capped at 0.9), less up
    /// to 30% each for precipitation reaching the tile and wind at the body. Never cools a warm day. Returns the
    /// insulation left, which also keeps a fire's heat in (Fire.WarmthAgainst).
    public static (double AirF, double InsulationLevel) ShelterStep(double airF, WeatherSample s, double insulationLevel,
        double windLevel, double overheadCoverLevel)
    {
        double ins = Math.Clamp(insulationLevel, 0, 0.9);
        ins *= 1 - s.PrecipitationPct * (1 - overheadCoverLevel) * 0.3;
        ins *= 1 - windLevel * 0.3;
        return (airF + Math.Max(0, 40 - airF) * ins, ins);
    }
}
