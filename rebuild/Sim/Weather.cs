using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Sim.Tests")]

namespace Survival;

public enum WeatherCondition { Clear, Cloudy, Misty, LightSnow, HeavySnow, Rainy, FreezingRain, Blizzard, Whiteout, Stormy }
public enum Season { Winter, Spring, Summer, Fall }
public enum WindDirection { North, Northeast, East, Southeast, South, Southwest, West, Northwest, Calm }
public enum FrontType { ClearSpell, StormSystem, ColdSnap, Warming, UnsettledPeriod, ProlongedBlizzard, IceStorm }

/// The regional sky at one minute. Air temperature is the same all through a front state (the
/// old fronts had no diurnal curve); the day/night swing reaches a person through the sun term
/// of Weather.ExposedAirF.
public readonly record struct WeatherSample(
    long Minute, WeatherCondition Rolled, double TemperatureC, double WindLevel,
    double PrecipitationPct, double CloudCoverPct, WindDirection Heading, FrontType Front = FrontType.ClearSpell)
{
    /// What is actually falling: the front's rolled condition with its phase set by the air temperature
    /// (Weather.PhaseOf). Derived, never stored, so a `with` on TemperatureC can't leave it stale.
    /// Everything below (wetting, ground, visibility, sun) reads this one value.
    [System.Text.Json.Serialization.JsonIgnore] public WeatherCondition Condition => Weather.PhaseOf(Rolled, TemperatureC);

    public double TemperatureF => TemperatureC * 9 / 5 + 32;
    /// WindLevel 1 is a 45 mph gale.
    public double WindMph => WindLevel * Weather.MaxWindMph;
    /// Calm is derived from speed, never stored: a stored Calm went stale in the old game.
    public WindDirection WindDirection => WindMph < 3.0 ? WindDirection.Calm : Heading;

    public double VisibilityFactor => Condition switch
    {
        WeatherCondition.Whiteout => 0.1,
        WeatherCondition.Blizzard => 0.3,
        WeatherCondition.Misty => 0.5,
        WeatherCondition.HeavySnow => 0.6,
        WeatherCondition.LightSnow => 0.8,
        _ => 1.0,
    };

    public double SunlightLevel => Weather.Sunlight(Minute, Condition, CloudCoverPct);

    /// What falls on a person. Old SurvivalContext left HeavySnow and FreezingRain out, so they
    /// never wetted anyone; fixed here: heavy snow wets as snow, freezing rain as rain.
    public Precipitation Precipitation => Condition switch
    {
        WeatherCondition.Rainy or WeatherCondition.Stormy or WeatherCondition.FreezingRain => Precipitation.Rain,
        WeatherCondition.Blizzard => Precipitation.Blizzard,
        WeatherCondition.LightSnow or WeatherCondition.HeavySnow => Precipitation.Snow,
        _ => Precipitation.None,
    };

    /// Snow reaching open ground, metres water-equivalent per minute (old Location.GetSurfaceWeather).
    /// Ground multiplies by its own exposure (1 - overhead cover). A whiteout is snow already down, blown about.
    public double SnowfallWeMPerMinute => Condition is WeatherCondition.LightSnow or WeatherCondition.HeavySnow or WeatherCondition.Blizzard
        ? PrecipitationPct * 0.005 / 60 : 0;
    /// Rain reaching open ground, metres per minute.
    public double RainfallMPerMinute => Condition is WeatherCondition.Rainy or WeatherCondition.Stormy or WeatherCondition.FreezingRain
        ? PrecipitationPct * 0.010 / 60 : 0;
}

/// Weather is a pure function of (seed, minute): fronts are generated in order from per-front
/// seed-derived streams, so any past minute answers the same no matter when or in what order
/// it is asked, and nothing about the weather is saved. A port of the old Weather, WeatherFront
/// and FrontPatterns: same patterns, ranges, durations, skips, grace period and calendar.
public static class Weather
{
    public const double MaxWindMph = 45.0;

    /// Snow above this falls as rain; rain below the matching low edge falls as snow. Between the
    /// two either phase is plausible and the front's own choice stands.
    public const double RainAboveC = 1.0, SnowBelowC = -1.0;

    /// Old FrontPatterns rolled snow states across the whole seasonal range and had no rain state,
    /// so July fronts snowed at +15 °C and summer storms rained at -10 °C.
    /// old game: precipitation phase ignored temperature (bug, fixed).
    public static WeatherCondition PhaseOf(WeatherCondition c, double temperatureC) => c switch
    {
        WeatherCondition.LightSnow or WeatherCondition.HeavySnow when temperatureC > RainAboveC => WeatherCondition.Rainy,
        WeatherCondition.Blizzard when temperatureC > RainAboveC => WeatherCondition.Stormy,
        // A whiteout is fallen snow blown about; above freezing nothing drifts, and the murk is fog.
        WeatherCondition.Whiteout when temperatureC > RainAboveC => WeatherCondition.Misty,
        WeatherCondition.Rainy when temperatureC < SnowBelowC => WeatherCondition.LightSnow,
        WeatherCondition.Stormy when temperatureC < SnowBelowC => WeatherCondition.Blizzard,
        _ => c,
    };

    // ---- calendar: world minute 0 is July 1, 09:00 (the old GameContext.StartTime) ----

    const int StartDayOfYear = 182, StartMinuteOfDay = 9 * 60, DaysPerYear = 365; // ponytail: no leap years
    /// The old game (GameContext.CreateNewGame) built its weather 30 days before the start and
    /// ran it forward, so turn 1 opens a month into the fronts, long past the gentle Initial one.
    /// The timeline starts here; world minute 0 is still July 1, 09:00.
    public const long PreRollMinutes = 30 * 1440L;
    /// Relative to game start, as old Weather.IsInGracePeriod: the whole pre-roll is inside it.
    const int GracePeriodMinutes = 7 * 1440;

    static long CalendarMinute(long minute) => minute + (StartDayOfYear - 1) * 1440L + StartMinuteOfDay;
    /// 1-based: January 1 is day 1.
    public static int DayOfYear(long minute) => (int)(CalendarMinute(minute) / 1440 % DaysPerYear) + 1;
    /// Day 1 is the first day of the game, as the old GameContext.DayNumber.
    public static int DayNumber(long minute) => (int)(CalendarMinute(minute) / 1440 - (StartDayOfYear - 1)) + 1;
    public static int MinuteOfDay(long minute) => (int)(CalendarMinute(minute) % 1440);

    public static Season SeasonAt(long minute) => DayOfYear(minute) switch
    {
        < 80 => Season.Winter,   // Jan 1 - Mar 20
        < 172 => Season.Spring,  // Mar 21 - Jun 20
        < 266 => Season.Summer,  // Jun 21 - Sep 22
        < 355 => Season.Fall,    // Sep 23 - Dec 20
        _ => Season.Winter,
    };

    public static int SunriseHour(long minute) => SeasonAt(minute) switch { Season.Winter => 8, Season.Summer => 4, _ => 6 };
    public static int SunsetHour(long minute) => SeasonAt(minute) switch { Season.Winter => 16, Season.Summer => 20, _ => 18 };

    public static bool IsDaytime(long minute)
    {
        int hour = MinuteOfDay(minute) / 60;
        return hour >= SunriseHour(minute) && hour < SunsetHour(minute);
    }

    /// 0-1 sunlight on open ground. The sine is anchored at 06:00-18:00 whatever the season, as in the old game.
    public static double Sunlight(long minute, WeatherCondition condition, double cloudCoverPct)
    {
        if (!IsDaytime(minute)) return 0;
        double angle = (MinuteOfDay(minute) - 6 * 60) / (12.0 * 60) * Math.PI;
        double byTime = Math.Max(0, Math.Sin(angle));
        double conditionReduction = condition switch
        {
            WeatherCondition.Misty => 0.6,
            WeatherCondition.Blizzard => 0.9,
            WeatherCondition.LightSnow => 0.3,
            WeatherCondition.Rainy => 0.5,
            WeatherCondition.Stormy => 0.8,
            _ => 0.0,
        };
        return byTime * (1 - cloudCoverPct * 0.9) * (1 - conditionReduction);
    }

    /// Old AbilityContext.CalculateDarkness: by the hour, whatever the season; caves are always dark.
    public static double DarknessLevel(World world, TileId tile)
    {
        if (Map.Of(world).CaveAt(tile) == Cave.Floor) return 1.0;
        return (MinuteOfDay(world.Minute) / 60) switch
        {
            < 5 => 1.0,
            < 6 => 0.5,
            < 17 => 0.0,
            < 20 => 0.2,
            < 21 => 0.6,
            _ => 1.0,
        };
    }

    // ---- what a person feels ----

    /// NWS wind chill (old Location.CalculateWindChillNWS). Only valid at or below 50 °F and 3 mph or more.
    public static double WindChillF(double temperatureF, double windSpeedMph)
    {
        if (temperatureF > 50 || windSpeedMph < 3) return temperatureF;
        double v = Math.Pow(windSpeedMph, 0.16);
        return 35.74 + 0.6215 * temperatureF - 35.75 * v + 0.4275 * temperatureF * v;
    }

    /// Air a person feels before any fire: old Location.GetTemperatureBreakdown steps 1-2 (the
    /// tile's own cold, wind chill through the tile's wind factor, sun and precipitation through
    /// its cover). Fire heat is measured against this. The defaults are a fully exposed tile.
    // ponytail: no shelter step until phase 4 shelters (old step 3, a 40 °F floor scaled by insulation).
    public static double ExposedAirF(WeatherSample s, double fireWindProtectionLevel,
        double windFactor = 1, double overheadCoverLevel = 0, double temperatureDeltaF = 0)
    {
        double t = s.TemperatureF + temperatureDeltaF;
        // The threshold reads the regional wind, as old; the chill reads what reaches this tile.
        if (s.WindLevel > 0.1) t = WindChillF(t, s.WindMph * windFactor * (1 - fireWindProtectionLevel));
        double exposure = 1 - overheadCoverLevel;
        t += s.SunlightLevel * exposure * 15 * Math.Clamp((50 - t) / 30, 0.5, 1); // sun is felt more in the cold
        t -= s.PrecipitationPct * exposure * 5;
        return t;
    }

    /// ExposedAirF on a map tile, with that tile's wind factor, cover and cold.
    public static double AirAtF(World world, TileId tile, WeatherSample s, double fireWindProtectionLevel)
    {
        var m = Map.Of(world);
        return ExposedAirF(s, fireWindProtectionLevel, Map.WindFactor(m, tile), Map.OverheadCover(m, tile), Map.TemperatureDeltaF(m, tile));
    }

    // ---- the weather at a minute ----

    public static WeatherSample At(World world, long minute)
    {
        // Negative minutes are the pre-roll: the month of weather the world has already lived.
        if (minute < -PreRollMinutes) throw new ArgumentOutOfRangeException(nameof(minute), minute, "Weather starts with the pre-roll");
        if (world.FixedWeather is WeatherSample fixedSky) return fixedSky with { Minute = minute };
        return Timeline(world).At(minute);
    }

    /// The seed's front states in order, through the one holding minute. For systems that
    /// integrate over the sky (erosion), which is piecewise constant per state.
    internal static IEnumerable<Spell> SpellsUntil(World world, long minute) => Timeline(world).SpellsUntil(minute);

    static WeatherTimeline Timeline(World world) => world.WeatherCache ??= new WeatherTimeline(world.Seed);

    /// One realised state of a front: its values hold from Start until the next state's Start.
    internal readonly record struct Spell(long Start, int FrontIndex, FrontType Front, WeatherCondition Condition,
        double TemperatureC, double WindLevel, double PrecipitationPct, double CloudCoverPct, WindDirection Heading);

    /// A front pattern's state before its values are rolled (old WeatherState).
    readonly record struct State(WeatherCondition Condition, (double Min, double Max) Temp, (double Min, double Max) Wind,
        (double Min, double Max) Precip, (double Min, double Max) Cloud, int DurationMinutes, double SkipPct = 0);

    /// Front k's private stream. Derived, never stored: front k reads the same whatever was asked before it.
    internal static Rng FrontRng(int seed, int k)
    {
        // Hash the seed before mixing in k: ForSystem only XORs the seed into the low bits, so
        // seed ^ k alone would hand seed 1's front 0 to seed 0's front 1.
        var r = Rng.ForSystem(seed, "weather");
        r.State = r.NextUlong() ^ (ulong)k;
        r.State = r.NextUlong();
        return r;
    }

    /// Front k's pattern. The old game drew each front one ahead, when the front before it began,
    /// so season and grace period are read at that minute (negative during the pre-roll).
    static (FrontType Type, List<State> States) Generate(Rng r, int k, long generatedAtMinute)
    {
        var season = SeasonAt(generatedAtMinute);
        if (k == 0) return Initial(r, season);
        return generatedAtMinute < GracePeriodMinutes ? Gentle(r, season) : Any(r, season);
    }

    /// Realises front k from its start: skips rolled at each transition, values rolled per state.
    /// Returns the minute the front ends. A skip past the last state ends the front early (the old
    /// code threw ArgumentOutOfRange there, and the next minute moved on to the next front).
    internal static long Realise(int seed, int k, long start, long generatedAt, List<Spell> into)
    {
        var r = FrontRng(seed, k);
        var (type, states) = Generate(r, k, generatedAt);
        long t = start;
        int i = 0;
        while (i < states.Count)
        {
            var s = states[i];
            into.Add(new Spell(t, k, type, s.Condition, Roll(r, s.Temp), Roll(r, s.Wind), Roll(r, s.Precip), Roll(r, s.Cloud),
                (WindDirection)r.NextInt(8)));
            t += s.DurationMinutes;
            i++;
            while (i < states.Count && r.NextDouble() < states[i].SkipPct) i++;
        }
        return t;
    }

    static double Roll(Rng r, (double Min, double Max) range) => r.NextDouble() * (range.Max - range.Min) + range.Min;
    static int RollDie(Rng r, int sides) => r.NextInt(sides) + 1;
    static bool Chance(Rng r, double pct) => r.NextDouble() < pct;

    /// Three dice averaged: the middle is common, the ends rare.
    static int TriangularMinutes(Rng r, int minHours, int maxHours)
    {
        int range = maxHours - minHours + 1;
        double average = (RollDie(r, range) + RollDie(r, range) + RollDie(r, range)) / 3.0;
        return (int)((minHours - 1 + average) * 60);
    }

    static (double Min, double Max, double SnowRatio) SeasonalRanges(Season season) => season switch
    {
        Season.Winter => (-30, -5, 0.95),
        Season.Spring => (-15, 5, 0.6),
        Season.Summer => (-5, 15, 0.2),
        _ => (-10, 5, 0.7), // Fall
    };

    // ---- front patterns (old FrontPatterns) ----

    static (FrontType, List<State>) Any(Rng r, Season season)
    {
        double roll = r.NextDouble();
        if (roll < 0.35) return ClearSpell(r, season);
        if (roll < 0.60) return StormSystem(r, season);
        if (roll < 0.70) return ColdSnap(r, season);
        if (roll < 0.80) return season is Season.Spring or Season.Fall ? IceStorm(r) : ColdSnap(r, season);
        if (roll < 0.90) return Warming(r, season);
        if (roll < 0.95) return ProlongedBlizzard(r, season);
        return Unsettled(r, season);
    }

    /// The early-game grace period: no blizzards, cold snaps, ice storms or whiteouts.
    static (FrontType, List<State>) Gentle(Rng r, Season season)
    {
        double roll = r.NextDouble();
        if (roll < 0.50) return ClearSpell(r, season);
        if (roll < 0.80) return MildStorm(r, season);
        return Warming(r, season);
    }

    /// Game start: gentle light snow, gentle for the season.
    static (FrontType, List<State>) Initial(Rng r, Season season)
    {
        var (lo, hi, _) = SeasonalRanges(season);
        return (FrontType.StormSystem, [new(WeatherCondition.LightSnow, (lo, hi), (0.1, 0.3), (0.1, 0.3), (0.5, 0.8), TriangularMinutes(r, 24, 48))]);
    }

    static (FrontType, List<State>) MildStorm(Rng r, Season season)
    {
        var (lo, hi, _) = SeasonalRanges(season);
        return (FrontType.StormSystem, [
            new(WeatherCondition.LightSnow, (lo, hi), (0.2, 0.4), (0.2, 0.4), (0.5, 0.8), TriangularMinutes(r, 4, 8)),
            new(WeatherCondition.HeavySnow, (lo - 2, hi), (0.4, 0.6), (0.5, 0.7), (0.8, 1.0), TriangularMinutes(r, 3, 6)),
            new(WeatherCondition.LightSnow, (lo - 1, hi), (0.2, 0.4), (0.1, 0.3), (0.6, 0.8), TriangularMinutes(r, 4, 8), 0.2),
            new(WeatherCondition.Cloudy, (lo, hi + 1), (0.2, 0.3), (0, 0.1), (0.3, 0.6), TriangularMinutes(r, 3, 6)),
        ]);
    }

    static (FrontType, List<State>) StormSystem(Rng r, Season season)
    {
        var (lo, hi, snowRatio) = SeasonalRanges(season);
        var building = new State(WeatherCondition.HeavySnow, (lo - 2, hi), (0.3, 0.6), (0.3, 0.5), (0.6, 0.9), TriangularMinutes(r, 4, 8));
        bool blizzard = season == Season.Winter || r.NextDouble() < snowRatio;
        return (FrontType.StormSystem, [
            building,
            new(blizzard ? WeatherCondition.Blizzard : WeatherCondition.Stormy, (lo - 5, hi - 2), (0.7, 1.0), (0.7, 1.0), (0.9, 1.0), TriangularMinutes(r, 2, 6)),
            new(WeatherCondition.Whiteout, (lo - 3, hi - 1), (0.6, 0.8), (0, 0.2), (0.5, 0.7), TriangularMinutes(r, 2, 4), 0.2),
            new(WeatherCondition.LightSnow, (lo - 2, hi), (0.2, 0.4), (0.1, 0.3), (0.6, 0.8), TriangularMinutes(r, 3, 8), 0.2),
            new(WeatherCondition.Cloudy, (lo - 1, hi + 1), (0.2, 0.4), (0, 0.1), (0.3, 0.6), TriangularMinutes(r, 2, 4)),
        ]);
    }

    static (FrontType, List<State>) ClearSpell(Rng r, Season season)
    {
        var (lo, hi, _) = SeasonalRanges(season);
        if (Chance(r, 0.6))
            return (FrontType.ClearSpell, [new(WeatherCondition.Clear, (lo, hi), (0.1, 0.5), (0, 0), (0, 0.2), TriangularMinutes(r, 12, 36))]);
        return (FrontType.ClearSpell, [
            new(WeatherCondition.Cloudy, (lo, hi), (0.2, 0.6), (0, 0), (0.5, 0.8), TriangularMinutes(r, 6, 12)),
            new(WeatherCondition.Clear, (lo, hi), (0.1, 0.4), (0, 0), (0, 0.3), TriangularMinutes(r, 6, 24)),
        ]);
    }

    static (FrontType, List<State>) ColdSnap(Rng r, Season season)
    {
        var (lo, hi, _) = SeasonalRanges(season);
        var drop = Chance(r, 0.5) ? WeatherCondition.Cloudy : WeatherCondition.Clear;
        return (FrontType.ColdSnap, [
            new(drop, (lo - 5, hi - 5), (0.3, 0.6), (0, 0.1), (0.2, 0.7), TriangularMinutes(r, 8, 12)),
            new(WeatherCondition.Clear, (lo - 10, hi - 8), (0.2, 0.5), (0, 0), (0, 0.3), TriangularMinutes(r, 12, 36)),
            new(WeatherCondition.Cloudy, (lo - 3, hi - 2), (0.2, 0.4), (0, 0.1), (0.4, 0.7), TriangularMinutes(r, 8, 16), 0.15),
        ]);
    }

    static (FrontType, List<State>) Warming(Rng r, Season season)
    {
        var (lo, hi, _) = SeasonalRanges(season);
        var states = new List<State>();
        if (Chance(r, 0.5))
            states.Add(new(WeatherCondition.Cloudy, (lo + 2, hi + 3), (0.2, 0.4), (0, 0.1), (0.5, 0.8), TriangularMinutes(r, 6, 12)));
        var peak = Chance(r, 0.7) ? WeatherCondition.Clear : WeatherCondition.Cloudy;
        states.Add(new(peak, (lo + 5, hi + 5), (0.1, 0.3), (0, 0), (0, 0.5), TriangularMinutes(r, 12, 24)));
        return (FrontType.Warming, states);
    }

    static (FrontType, List<State>) Unsettled(Rng r, Season season)
    {
        var (lo, hi, _) = SeasonalRanges(season);
        var states = new List<State>();
        int count = RollDie(r, 3) + 2;
        for (int i = 0; i < count; i++)
        {
            var c = r.NextDouble() switch
            {
                < 0.3 => WeatherCondition.Cloudy,
                < 0.5 => WeatherCondition.Misty,
                < 0.7 => WeatherCondition.LightSnow,
                < 0.85 => WeatherCondition.HeavySnow,
                < 0.95 => WeatherCondition.Whiteout,
                _ => WeatherCondition.Clear,
            };
            states.Add(new(c, (lo, hi), (0.1, 0.6),
                c == WeatherCondition.LightSnow ? (0.1, 0.4) : (0, 0.2),
                c == WeatherCondition.Clear ? (0, 0.3) : (0.5, 0.9),
                TriangularMinutes(r, 3, 8)));
        }
        return (FrontType.UnsettledPeriod, states);
    }

    /// Calm before the storm, then a multi-day siege.
    static (FrontType, List<State>) ProlongedBlizzard(Rng r, Season season)
    {
        var (lo, hi, _) = SeasonalRanges(season);
        return (FrontType.ProlongedBlizzard, [
            new(WeatherCondition.Clear, (lo + 2, hi + 3), (0.0, 0.2), (0, 0), (0.0, 0.3), TriangularMinutes(r, 20, 28)),
            new(WeatherCondition.HeavySnow, (lo - 5, hi - 3), (0.6, 0.8), (0.7, 0.9), (0.9, 1.0), TriangularMinutes(r, 8, 12)),
            new(WeatherCondition.Blizzard, (lo - 10, hi - 7), (0.8, 1.0), (0.9, 1.0), (1.0, 1.0), TriangularMinutes(r, 30, 50)),
            new(WeatherCondition.Whiteout, (lo - 7, hi - 5), (0.7, 0.9), (0, 0.2), (0.5, 0.8), TriangularMinutes(r, 8, 16)),
            new(WeatherCondition.Cloudy, (lo - 3, hi - 1), (0.3, 0.5), (0, 0), (0.5, 0.7), TriangularMinutes(r, 6, 12)),
        ]);
    }

    /// Spring and fall only, near freezing whatever the season's range.
    static (FrontType, List<State>) IceStorm(Rng r) => (FrontType.IceStorm, [
        new(WeatherCondition.Cloudy, (-2, 2), (0.2, 0.4), (0.1, 0.3), (0.8, 1.0), TriangularMinutes(r, 4, 8)),
        new(WeatherCondition.FreezingRain, (-1, 1), (0.2, 0.5), (0.4, 0.7), (0.9, 1.0), TriangularMinutes(r, 12, 24)),
        new(WeatherCondition.Clear, (-5, -2), (0.1, 0.3), (0, 0), (0, 0.3), TriangularMinutes(r, 6, 12)),
    ]);
}

/// The front sequence, realised in order on demand. Pure derived data: rebuilt from the seed
/// after a load, never saved, so it cannot disagree with the seed.
internal sealed class WeatherTimeline(int seed)
{
    readonly List<Weather.Spell> spells = [];
    int nextFront;
    // The old game drew front k when front k-1 began; front 0 and 1 both at the pre-roll start.
    long nextStart = -Weather.PreRollMinutes, nextGeneratedAt = -Weather.PreRollMinutes;

    /// Any minute from the start of the pre-roll; the world itself only lives minutes from 0.
    public WeatherSample At(long minute)
    {
        if (minute < -Weather.PreRollMinutes)
            throw new ArgumentOutOfRangeException(nameof(minute), minute, "Weather starts with the pre-roll");
        while (nextStart <= minute) ExtendOneFront();
        int lo = 0, hi = spells.Count - 1;
        while (lo < hi) // last spell starting at or before minute
        {
            int mid = (lo + hi + 1) / 2;
            if (spells[mid].Start <= minute) lo = mid; else hi = mid - 1;
        }
        var s = spells[lo];
        return new WeatherSample(minute, s.Condition, s.TemperatureC, s.WindLevel, s.PrecipitationPct, s.CloudCoverPct, s.Heading, s.Front);
    }

    /// Fronts in order, for statistics.
    public IEnumerable<Weather.Spell> SpellsUntil(long minute)
    {
        At(minute);
        return spells.TakeWhile(s => s.Start <= minute);
    }

    void ExtendOneFront()
    {
        long start = nextStart;
        nextStart = Weather.Realise(seed, nextFront, start, nextGeneratedAt, spells);
        nextGeneratedAt = start;
        nextFront++;
    }
}
