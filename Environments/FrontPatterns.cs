using text_survival;

namespace text_survival.Environments;

/// <summary>
/// Front generation library - creates realistic weather patterns
/// with state sequences and randomness
/// </summary>
public static class FrontPatterns
{
    /// <summary>
    /// Generate a new weather front based on season and current conditions
    /// </summary>
    public static WeatherFront Generate(Weather.Season season, Weather.WeatherCondition currentCondition)
    {
        // Weight front types by season and roll
        double roll = Utils.RandDouble(0, 1);

        if (roll < 0.40)  // 40% - Clear Spell
            return GenerateClearSpell(season);
        else if (roll < 0.70)  // 30% - Storm System
            return GenerateStormSystem(season);
        else if (roll < 0.85)  // 15% - Cold Snap
            return GenerateColdSnap(season);
        else if (roll < 0.95)  // 10% - Warming
            return GenerateWarmingPeriod(season);
        else  // 5% - Unsettled
            return GenerateUnsettledPeriod(season);
    }

    /// <summary>
    /// Storm System: Building → Peak → Trailing → Clearing (12-48 hours total)
    /// Some states can skip for variety
    /// </summary>
    private static WeatherFront GenerateStormSystem(Weather.Season season)
    {
        var states = new List<WeatherState>();
        var (minTemp, maxTemp, snowRatio) = GetSeasonalRanges(season);

        // Building phase (4-8 hours)
        states.Add(new WeatherState
        {
            Condition = Weather.WeatherCondition.Cloudy,
            TempRange = (minTemp - 2, maxTemp),  // Slight cooling
            WindRange = (0.3, 0.6),
            CloudRange = (0.6, 0.9),
            PrecipRange = (0, 0.1),
            Duration = GenerateTriangularDuration(4, 8),
            SkipProbability = 0.0  // Never skip building phase
        });

        // Peak phase (2-6 hours) - blizzard or storm based on temperature
        bool isBlizzard = season == Weather.Season.Winter || Utils.RandDouble(0, 1) < snowRatio;
        states.Add(new WeatherState
        {
            Condition = isBlizzard ? Weather.WeatherCondition.Blizzard : Weather.WeatherCondition.Stormy,
            TempRange = (minTemp - 5, maxTemp - 2),
            WindRange = (0.7, 1.0),
            CloudRange = (0.9, 1.0),
            PrecipRange = (0.7, 1.0),
            Duration = GenerateTriangularDuration(2, 6),
            SkipProbability = 0.0  // Never skip peak
        });

        // Trailing phase (3-8 hours) - can skip for abrupt clearing (20% chance)
        states.Add(new WeatherState
        {
            Condition = Weather.WeatherCondition.LightSnow,
            TempRange = (minTemp - 3, maxTemp - 1),
            WindRange = (0.3, 0.5),
            CloudRange = (0.7, 0.9),
            PrecipRange = (0.2, 0.4),
            Duration = GenerateTriangularDuration(3, 8),
            SkipProbability = 0.2  // 20% chance to skip for variety
        });

        // Clearing phase (2-4 hours)
        states.Add(new WeatherState
        {
            Condition = Weather.WeatherCondition.Cloudy,
            TempRange = (minTemp - 1, maxTemp + 1),
            WindRange = (0.2, 0.4),
            CloudRange = (0.3, 0.6),
            PrecipRange = (0, 0.1),
            Duration = GenerateTriangularDuration(2, 4),
            SkipProbability = 0.0
        });

        return new WeatherFront
        {
            Type = FrontType.StormSystem,
            States = states
        };
    }

    /// <summary>
    /// Clear Spell: Stable good weather (12-36 hours)
    /// 1-2 states of consistent conditions
    /// </summary>
    private static WeatherFront GenerateClearSpell(Weather.Season season)
    {
        var states = new List<WeatherState>();
        var (minTemp, maxTemp, _) = GetSeasonalRanges(season);

        // Decide if clear or mostly cloudy
        bool isClear = Utils.DetermineSuccess(0.6);  // 60% fully clear, 40% partly cloudy

        if (isClear)
        {
            // Single long clear state (12-36 hours)
            states.Add(new WeatherState
            {
                Condition = Weather.WeatherCondition.Clear,
                TempRange = (minTemp, maxTemp),
                WindRange = (0.1, 0.5),
                CloudRange = (0, 0.2),
                PrecipRange = (0, 0),
                Duration = GenerateTriangularDuration(12, 36),
                SkipProbability = 0.0
            });
        }
        else
        {
            // Two states: cloudy → clear (or stay cloudy)
            states.Add(new WeatherState
            {
                Condition = Weather.WeatherCondition.Cloudy,
                TempRange = (minTemp, maxTemp),
                WindRange = (0.2, 0.6),
                CloudRange = (0.5, 0.8),
                PrecipRange = (0, 0),
                Duration = GenerateTriangularDuration(6, 12),
                SkipProbability = 0.0
            });

            states.Add(new WeatherState
            {
                Condition = Weather.WeatherCondition.Clear,
                TempRange = (minTemp, maxTemp),
                WindRange = (0.1, 0.4),
                CloudRange = (0, 0.3),
                PrecipRange = (0, 0),
                Duration = GenerateTriangularDuration(6, 24),
                SkipProbability = 0.0
            });
        }

        return new WeatherFront
        {
            Type = FrontType.ClearSpell,
            States = states
        };
    }

    /// <summary>
    /// Cold Snap: Progressive temperature drop (24-72 hours)
    /// 2-3 states with worsening cold
    /// </summary>
    private static WeatherFront GenerateColdSnap(Weather.Season season)
    {
        var states = new List<WeatherState>();
        var (minTemp, maxTemp, _) = GetSeasonalRanges(season);

        // Initial drop (8-12 hours)
        states.Add(new WeatherState
        {
            Condition = Utils.DetermineSuccess(0.5) ? Weather.WeatherCondition.Cloudy : Weather.WeatherCondition.Clear,
            TempRange = (minTemp - 5, maxTemp - 5),  // -5°C drop
            WindRange = (0.3, 0.6),
            CloudRange = (0.2, 0.7),
            PrecipRange = (0, 0.1),
            Duration = GenerateTriangularDuration(8, 12),
            SkipProbability = 0.0
        });

        // Extreme cold (12-36 hours)
        states.Add(new WeatherState
        {
            Condition = Weather.WeatherCondition.Clear,  // Clear cold nights are coldest
            TempRange = (minTemp - 10, maxTemp - 8),  // Additional -5°C
            WindRange = (0.2, 0.5),
            CloudRange = (0, 0.3),
            PrecipRange = (0, 0),
            Duration = GenerateTriangularDuration(12, 36),
            SkipProbability = 0.0
        });

        // Gradual warming (8-16 hours) - can skip for abrupt recovery
        states.Add(new WeatherState
        {
            Condition = Weather.WeatherCondition.Cloudy,
            TempRange = (minTemp - 3, maxTemp - 2),  // Partial recovery
            WindRange = (0.2, 0.4),
            CloudRange = (0.4, 0.7),
            PrecipRange = (0, 0.1),
            Duration = GenerateTriangularDuration(8, 16),
            SkipProbability = 0.15  // 15% skip for abrupt return to normal
        });

        return new WeatherFront
        {
            Type = FrontType.ColdSnap,
            States = states
        };
    }

    /// <summary>
    /// Warming Period: Brief thaw (12-30 hours)
    /// 1-2 states with temperature increase
    /// </summary>
    private static WeatherFront GenerateWarmingPeriod(Weather.Season season)
    {
        var states = new List<WeatherState>();
        var (minTemp, maxTemp, _) = GetSeasonalRanges(season);

        // Gradual warming (6-12 hours) - optional state
        if (Utils.DetermineSuccess(0.5))
        {
            states.Add(new WeatherState
            {
                Condition = Weather.WeatherCondition.Cloudy,
                TempRange = (minTemp + 2, maxTemp + 3),
                WindRange = (0.2, 0.4),
                CloudRange = (0.5, 0.8),
                PrecipRange = (0, 0.1),
                Duration = GenerateTriangularDuration(6, 12),
                SkipProbability = 0.0
            });
        }

        // Peak warmth (12-24 hours)
        states.Add(new WeatherState
        {
            Condition = Utils.DetermineSuccess(0.7) ? Weather.WeatherCondition.Clear : Weather.WeatherCondition.Cloudy,
            TempRange = (minTemp + 5, maxTemp + 5),  // +5°C warmer than normal
            WindRange = (0.1, 0.3),
            CloudRange = (0, 0.5),
            PrecipRange = (0, 0),
            Duration = GenerateTriangularDuration(12, 24),
            SkipProbability = 0.0
        });

        return new WeatherFront
        {
            Type = FrontType.Warming,
            States = states
        };
    }

    /// <summary>
    /// Unsettled Period: Oscillating conditions (18-48 hours)
    /// 3-5 states with changing weather
    /// </summary>
    private static WeatherFront GenerateUnsettledPeriod(Weather.Season season)
    {
        var states = new List<WeatherState>();
        var (minTemp, maxTemp, snowRatio) = GetSeasonalRanges(season);

        int stateCount = Utils.Roll(3) + 2;  // 3-5 states

        for (int i = 0; i < stateCount; i++)
        {
            // Randomly pick condition
            Weather.WeatherCondition condition = Utils.RandDouble(0, 1) switch
            {
                < 0.4 => Weather.WeatherCondition.Cloudy,
                < 0.7 => Weather.WeatherCondition.Misty,
                < 0.9 => Weather.WeatherCondition.LightSnow,
                _ => Weather.WeatherCondition.Clear
            };

            states.Add(new WeatherState
            {
                Condition = condition,
                TempRange = (minTemp, maxTemp),
                WindRange = (0.1, 0.6),
                CloudRange = condition == Weather.WeatherCondition.Clear ? (0, 0.3) : (0.5, 0.9),
                PrecipRange = condition == Weather.WeatherCondition.LightSnow ? (0.1, 0.4) : (0, 0.2),
                Duration = GenerateTriangularDuration(3, 8),
                SkipProbability = 0.0
            });
        }

        return new WeatherFront
        {
            Type = FrontType.UnsettledPeriod,
            States = states
        };
    }

    /// <summary>
    /// Get season-specific temperature ranges and snow ratio
    /// Matches the ranges from Weather.cs GenerateNewWeather()
    /// </summary>
    private static (double minTemp, double maxTemp, double snowRatio) GetSeasonalRanges(Weather.Season season)
    {
        return season switch
        {
            Weather.Season.Winter => (-30, -5, 0.95),
            Weather.Season.Spring => (-15, 5, 0.6),
            Weather.Season.Summer => (-5, 15, 0.2),
            Weather.Season.Fall => (-10, 5, 0.7),
            _ => (-15, 0, 0.8)
        };
    }

    /// <summary>
    /// Generate duration using triangular distribution (like current Weather.cs)
    /// Results cluster around the middle, extremes are rare
    /// </summary>
    private static TimeSpan GenerateTriangularDuration(int minHours, int maxHours)
    {
        // Convert to 1-based range for Utils.Roll
        int range = maxHours - minHours + 1;

        // Three rolls averaged (triangular distribution)
        int r1 = Utils.Roll(range);
        int r2 = Utils.Roll(range);
        int r3 = Utils.Roll(range);
        double average = (r1 + r2 + r3) / 3.0;

        // Convert to hours (offset by minHours - 1 since Roll returns 1-based)
        double hours = minHours - 1 + average;
        int minutes = (int)(hours * 60);

        return TimeSpan.FromMinutes(minutes);
    }
}
