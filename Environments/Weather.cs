using text_survival;
using text_survival.Environments;

public class Weather
{
    // todo: improve sunrise and sunset logic,
    // todo: add more continuity and state to more granular changes
    public double BaseTemperature { get; set; } // In Celsius
    public WeatherCondition CurrentCondition { get; set; }
    public double Precipitation { get; set; } // 0-1 intensity
    public double WindSpeed { get; set; }    // 0-1 intensity
    public double CloudCover { get; set; }   // 0-1 coverage
    public int Elevation { get; init; }

    // Weather transition tracking for events
    public WeatherCondition? PreviousCondition { get; set; }
    public bool WeatherJustChanged { get; set; }

    // Weather front system - multi-state sequences
    public WeatherFront? CurrentFront { get; set; }
    public WeatherFront? NextFront { get; set; }

    // Wind direction system
    public WindDirection CurrentWindDirection { get; set; } = WindDirection.Calm;

    // Helper properties for other systems to query
    public double VisibilityFactor => CurrentCondition switch
    {
        WeatherCondition.Whiteout => 0.1,
        WeatherCondition.Blizzard => 0.3,
        WeatherCondition.Misty => 0.5,
        WeatherCondition.HeavySnow => 0.6,
        WeatherCondition.LightSnow => 0.8,
        _ => 1.0
    };

    public bool IsIceHazard => CurrentCondition == WeatherCondition.FreezingRain;
    public bool IsNavigationHazard => CurrentCondition is WeatherCondition.Whiteout or WeatherCondition.Blizzard;

    // Season tracking - derived from game time
    public enum Season { Winter, Spring, Summer, Fall }

    /// <summary>
    /// Returns current season derived from game time.
    /// </summary>
    public Season CurrentSeason => GetCurrentSeason();

    private Season GetCurrentSeason()
    {
        int dayOfYear = Time.DayOfYear;
        return dayOfYear switch
        {
            < 80 => Season.Winter,   // Jan 1 - Mar 20
            < 172 => Season.Spring,  // Mar 21 - June 20
            < 266 => Season.Summer,  // June 21 - Sep 22
            < 355 => Season.Fall,    // Sep 23 - Dec 20
            _ => Season.Winter       // Dec 21 - Dec 31
        };
    }

    /// <summary>
    /// Returns 0-1 representing seasonal cold intensity.
    /// 1.0 = mid-winter (coldest), 0.0 = mid-summer (warmest).
    /// Uses cosine wave centered on solstices.
    /// </summary>
    public double SeasonalIntensity
    {
        get
        {
            int dayOfYear = Time.DayOfYear;

            // Midwinter solstice = Dec 21 (day 355), Midsummer = June 21 (day 172)
            // Distance from midwinter in days (wrapping around year)
            int midwinter = 355;
            int daysFromMidwinter = Math.Abs(dayOfYear - midwinter);
            if (daysFromMidwinter > 182)
                daysFromMidwinter = 365 - daysFromMidwinter;

            // Use cosine for smooth transition: 1 at midwinter, 0 at midsummer
            double radians = (daysFromMidwinter / 182.0) * Math.PI;
            return (Math.Cos(radians) + 1) / 2; // Normalize to 0-1
        }
    }

    public string GetSeasonLabel() => CurrentSeason.ToString();

    // Weather conditions for Ice Age Europe
    public enum WeatherCondition
    {
        Clear,        // Clear, cold skies
        Cloudy,       // Overcast conditions
        Misty,        // Low visibility with moisture
        LightSnow,    // Light snowfall (common in Ice Age)
        HeavySnow,    // Significant accumulation
        Rainy,        // Cold rain (uncommon but possible)
        FreezingRain, // Ice coating hazard
        Blizzard,     // Heavy snow with wind (dangerous)
        Whiteout,     // Zero visibility from blowing snow
        Stormy        // Thunderstorms (rare, mostly summer)
    }

    // Wind direction for tactical gameplay
    public enum WindDirection
    {
        North, Northeast, East, Southeast,
        South, Southwest, West, Northwest,
        Calm  // No meaningful wind direction
    }

    public double SunlightIntensity
    {
        get
        {
            // No sun at night
            if (!IsDaytime(Time))
                return 0;

            // Base sun intensity from time of day
            double timeOfDayFactor = GetSunIntensityByTime(Time);

            // Reduction factors from weather conditions
            double cloudReduction = CloudCover * 0.9; // Clouds block up to 90% of sunlight

            // Additional reduction based on weather condition
            double conditionReduction = CurrentCondition switch
            {
                WeatherCondition.Misty => 0.6,
                WeatherCondition.Blizzard => 0.9,
                WeatherCondition.LightSnow => 0.3,
                WeatherCondition.Rainy => 0.5,
                WeatherCondition.Stormy => 0.8,
                _ => 0.0
            };
            // Calculate final intensity (0-1)
            double baseIntensity = timeOfDayFactor * (1 - cloudReduction);
            return baseIntensity * (1 - conditionReduction);
        }
    }

    private double GetSunIntensityByTime(DateTime time)
    {
        // Get sun intensity purely based on time of day (0-1)
        int hour = time.Hour;
        int minute = time.Minute;

        // No sunlight before sunrise or after sunset
        if (!IsDaytime(time))
            return 0;

        // Convert to minutes since sunrise (0-720)
        double minutesSinceSunrise = ((hour - 6) * 60) + minute;
        double dayLengthMinutes = 12 * 60; // 12 hours of daylight

        // Calculate angle for sine function (0 to π over the day)
        double angle = (minutesSinceSunrise / dayLengthMinutes) * Math.PI;

        // Sine wave peaks at noon (6 hours after sunrise)
        return Math.Sin(angle);
    }

    // Time and update tracking (public for serialization)
    private TimeSpan _weatherDuration = TimeSpan.FromHours(6);
    public TimeSpan TimeSinceWeatherChange { get; set; } = TimeSpan.Zero;

    public DateTime Time { get; set; }

    // Grace period for early game - prevents severe weather
    private DateTime _gameStartTime = new DateTime(2025, 1, 1, 9, 0, 0);
    private const int GRACE_PERIOD_DAYS = 7;

    // Parameterless constructor for deserialization
    public Weather()
    {
    }

    // Normal constructor for creation
    public Weather(double baseTemp)
    {
        // Initialize with fall weather
        BaseTemperature = baseTemp;
        CurrentCondition = WeatherCondition.Clear;
        Precipitation = 0;
        WindSpeed = 0.3; // Moderate wind - 30% of maximum
        CloudCover = 0.3; // Light clouds - 30% coverage

        _weatherDuration = TimeSpan.FromHours(6);

        // Initialize weather fronts - guaranteed gentle start
        CurrentFront = FrontPatterns.GenerateInitialFront();
        NextFront = FrontPatterns.Generate(Season.Winter, CurrentCondition);

        // Apply the initial state immediately (fixes bug where 6-hour legacy duration was used)
        ApplyWeatherState(CurrentFront.CurrentState);
    }

    public void Update(DateTime newTime)
    {
        // Reset transition flag after one update cycle
        WeatherJustChanged = false;

        TimeSpan elapsed = newTime - Time;
        TimeSinceWeatherChange += elapsed;

        // Time to change weather?
        if (TimeSinceWeatherChange >= _weatherDuration)
        {
            AdvanceFront();
            TimeSinceWeatherChange = TimeSpan.Zero;
        }
        Time = newTime;
    }

    private void GenerateNewWeather()
    {
        // Track transition for event system
        PreviousCondition = CurrentCondition;
        WeatherJustChanged = true;

        // Generate new weather conditions based on season and zone
        // Determine base temperature range for season
        double minTemp, maxTemp;
        double precipChance;
        double snowRatio; // Chance of precipitation being snow vs rain

        switch (CurrentSeason)
        {
            case Season.Winter:
                minTemp = -30; // -30°C (-22°F) extreme winter low
                maxTemp = -5;  // -5°C (23°F) winter "warm" day
                precipChance = 0.2; // 20% chance of precipitation
                snowRatio = 0.95;   // 95% of precip is snow in winter
                break;

            case Season.Spring:
                minTemp = -15; // -15°C (5°F) cold spring night
                maxTemp = 5;   // 5°C (41°F) mild spring day
                precipChance = 0.25; // 25% chance of precipitation
                snowRatio = 0.6;    // 60% of precip is snow in spring
                break;

            case Season.Summer:
                minTemp = -5;  // -5°C (23°F) cold summer night
                maxTemp = 15;  // 15°C (59°F) warm summer day
                precipChance = 0.15; // 15% chance of precipitation
                snowRatio = 0.2;    // 20% of precip is snow in summer
                break;

            case Season.Fall:
                minTemp = -10; // -10°C (14°F) cold fall night
                maxTemp = 5;   // 5°C (41°F) mild fall day
                precipChance = 0.2;  // 20% chance of precipitation
                snowRatio = 0.7;    // 70% of precip is snow in fall
                break;

            default:
                minTemp = -15;
                maxTemp = 0;
                precipChance = 0.2;
                snowRatio = 0.8;
                break;
        }

        // Apply zone-specific modifications
        if (Elevation > 0)
        {
            // Higher elevation = colder (-0.6°C per 100m elevation)
            double elevationEffect = Elevation * -0.006; // -0.6% per 100m
            minTemp += elevationEffect;
            maxTemp += elevationEffect;
        }

        // Get time of day temperature modifier (0-1 scale)
        double timeOfDayFactor = GetTimeOfDayFactor();

        // Calculate random temperature within range, biased toward colder
        // For example: With Spring (-15°C to 5°C) at noon (factor=1.0):
        // Temperature range = -15 + (5-(-15)) * random(0,0.8) * 1.0 = -15 to +1°C
        double temperatureRange = maxTemp - minTemp;
        double randomFlux = Utils.RandDouble(0, 1);

        double baseCalc = minTemp + (temperatureRange * randomFlux * timeOfDayFactor);

        // Apply seasonal intensity modifier
        // SeasonalIntensity: 1.0 = mid-winter (colder), 0.0 = mid-summer (warmer)
        // This shifts temperature within the season's range based on where we are in the year
        // Max effect: ±5°C shift based on seasonal intensity
        double seasonalShift = (SeasonalIntensity - 0.5) * 10; // -5 to +5°C
        BaseTemperature = baseCalc - seasonalShift;

        // Determine weather condition
        if (Utils.RandDouble(0, 1) < precipChance) // Precipitation check (0.15-0.25 chance)
        {
            // Determine type of precipitation
            double snowVsRainRoll = Utils.RandDouble(0, 1);

            if (snowVsRainRoll < snowRatio) // Snow event
            {
                // Determine if blizzard (rare) or light snow (common)
                if (Utils.RandDouble(0, 1) < 0.15) // 15% of snow events are blizzards
                {
                    CurrentCondition = WeatherCondition.Blizzard;
                    Precipitation = Utils.RandDouble(0.7, 1.0); // 70-100% intensity
                    WindSpeed = Utils.RandDouble(0.7, 1.0);     // 70-100% of max wind
                    CloudCover = Utils.RandDouble(0.9, 1.0);    // 90-100% cloud cover
                    _weatherDuration = GenerateRandomWeatherDuration(4); // 1-7 hours

                    // Blizzards only occur in cold conditions - clamp to lower 40% of range
                    double coldHalfMax = minTemp + (temperatureRange * 0.4);
                    BaseTemperature = Math.Min(BaseTemperature, coldHalfMax);
                }
                else
                {
                    CurrentCondition = WeatherCondition.LightSnow;
                    Precipitation = Utils.RandDouble(0.2, 0.6); // 20-60% intensity
                    WindSpeed = Utils.RandDouble(0.2, 0.5);     // 20-50% of max wind
                    CloudCover = Utils.RandDouble(0.7, 0.9);    // 70-90% cloud cover
                    _weatherDuration = GenerateRandomWeatherDuration(6); // 1-11 hours
                }
            }
            else // Rain event (uncommon in Ice Age)
            {
                // Only happens when temperature is above freezing
                if (BaseTemperature > 0)
                {
                    // Determine if stormy (very rare) or rainy
                    if (CurrentSeason == Season.Summer && Utils.RandDouble(0, 1) < 0.1) // 10% of summer rain is storms
                    {
                        CurrentCondition = WeatherCondition.Stormy;
                        Precipitation = Utils.RandDouble(0.6, 0.9); // 60-90% intensity
                        WindSpeed = Utils.RandDouble(0.5, 0.8);     // 50-80% of max wind
                        CloudCover = Utils.RandDouble(0.9, 1.0);    // 90-100% cloud cover
                        _weatherDuration = GenerateRandomWeatherDuration(2); // 1-3 hours
                    }
                    else
                    {
                        CurrentCondition = WeatherCondition.Rainy;
                        Precipitation = Utils.RandDouble(0.3, 0.6); // 30-60% intensity
                        WindSpeed = Utils.RandDouble(0.2, 0.4);     // 20-40% of max wind
                        CloudCover = Utils.RandDouble(0.7, 0.9);    // 70-90% cloud cover
                        _weatherDuration = GenerateRandomWeatherDuration(4); // 1-7 hours
                    }
                }
                else // Temperature too cold for rain, adjust to snow
                {
                    CurrentCondition = WeatherCondition.LightSnow;
                    Precipitation = Utils.RandDouble(0.2, 0.5); // 20-50% intensity
                    WindSpeed = Utils.RandDouble(0.2, 0.4);     // 20-40% of max wind
                    CloudCover = Utils.RandDouble(0.7, 0.9);    // 70-90% cloud cover
                    _weatherDuration = GenerateRandomWeatherDuration(5); // 1-9 hours
                }
            }
        }
        else // No precipitation
        {
            // Choose between clear, cloudy or misty
            double clearVsCloudyRoll = Utils.RandDouble(0, 1);

            if (clearVsCloudyRoll < 0.4) // 40% chance for clear
            {
                CurrentCondition = WeatherCondition.Clear;
                Precipitation = 0;
                WindSpeed = Utils.RandDouble(0.1, 0.5);     // 10-50% of max wind
                CloudCover = Utils.RandDouble(0, 0.2);      // 0-20% cloud cover
                _weatherDuration = GenerateRandomWeatherDuration(9); // 1-17 hours
            }
            else if (clearVsCloudyRoll < 0.8) // 40% chance for cloudy
            {
                CurrentCondition = WeatherCondition.Cloudy;
                Precipitation = 0;
                WindSpeed = Utils.RandDouble(0.2, 0.6);     // 20-60% of max wind
                CloudCover = Utils.RandDouble(0.5, 0.8);    // 50-80% cloud cover
                _weatherDuration = GenerateRandomWeatherDuration(6); // 1-11 hours
            }
            else // 20% chance for misty
            {
                CurrentCondition = WeatherCondition.Misty;
                Precipitation = Utils.RandDouble(0, 0.1);   // 0-10% light moisture
                WindSpeed = Utils.RandDouble(0, 0.2);       // 0-20% of max wind
                CloudCover = Utils.RandDouble(0.6, 0.9);    // 60-90% cloud cover
                _weatherDuration = GenerateRandomWeatherDuration(3); // 1-5 hours
            }
        }
    }

    private TimeSpan GenerateRandomWeatherDuration(int typicalHours = 6)
    {
        // int minHours = 1; // using roll instead which has 1 as default
        int maxHours = (typicalHours * 2) - 1;

        // triangular distribution - similar to normal dist, 
        // where values in the center are more common and extremes are rare
        // kind of like rolling dice, it's rare to roll 3 ones or 3 sixes
        int r1 = Utils.Roll(maxHours);
        int r2 = Utils.Roll(maxHours);
        int r3 = Utils.Roll(maxHours);
        double sum = r1 + r2 + r3;
        double average = sum / 3.0;
        int minutes = (int)(average * 60);
        return TimeSpan.FromMinutes(minutes);
    }

    private double GetTimeOfDayFactor()
    {
        // Returns 0-1 value representing relative temperature (0=coldest, 1=warmest)
        // todo combine this with seasonal day/ night lengths
        int minutesInDay = 24 * 60;
        int coldestTime = 4 * 60; // 4 AM

        int currentMinute = Time.Hour * 60 + Time.Minute;
        double minSinceColdest = currentMinute - coldestTime;
        double percentOfDay = minSinceColdest / minutesInDay; // scale so .5 is warmest and 0/1 is coldest
        double radians = 2 * Math.PI * percentOfDay; // scale for Cos

        // cos(x) => -1 to 1, but we need to shift to 0-1 so divide by 2 and shift up by 
        // but also the cos function needs to be flipped since cos(0 or 1) = 1, but we want 0 and 1 to be the min, so just * -1
        double temperature = -1 * (Math.Cos(radians) / 2) + .5; // Cos results in -1 to 1, so scale to 0-1 (divide 2, shift)

        return temperature;
    }

    // Convert Celsius to Fahrenheit
    public double TemperatureInFahrenheit => (BaseTemperature * 9 / 5) + 32;

    // Get detailed weather description
    public string GetWeatherDescription()
    {
        string temp = GetTemperatureDescription();
        string conditions = GetConditionsDescription();
        string wind = GetWindDescription();

        return $"{temp} {conditions} {wind}";
    }

    private string GetTemperatureDescription()
    {
        if (BaseTemperature < -25)
            return "It's brutally cold.";
        else if (BaseTemperature < -15)
            return "It's extremely cold.";
        else if (BaseTemperature < -5)
            return "It's very cold.";
        else if (BaseTemperature < 0)
            return "It's freezing cold.";
        else if (BaseTemperature < 5)
            return "It's cold.";
        else if (BaseTemperature < 10)
            return "It's cool.";
        else
            return "It's mild."; // As warm as it gets in Ice Age
    }

    private string GetConditionsDescription()
    {
        switch (CurrentCondition)
        {
            case WeatherCondition.Clear:
                return "The sky is clear.";

            case WeatherCondition.Cloudy:
                return "The sky is cloudy and gray.";

            case WeatherCondition.Misty:
                return "A cold mist hangs in the air.";

            case WeatherCondition.Rainy:
                if (Precipitation < 0.5)
                    return "A cold drizzle is falling.";
                else
                    return "Cold rain is falling steadily.";

            case WeatherCondition.LightSnow:
                if (Precipitation < 0.3)
                    return "A few snowflakes drift through the air.";
                else
                    return "Snow is falling steadily.";

            case WeatherCondition.Blizzard:
                return "A blizzard rages with heavy snow and wind.";

            case WeatherCondition.Stormy:
                return "A thunderstorm rumbles overhead.";

            default:
                return "";
        }
    }

    private string GetWindDescription()
    {
        if (WindSpeed < 0.2)           // 0-20%
            return "The air is still.";
        else if (WindSpeed < 0.4)      // 20-40%
            return "A light breeze blows.";
        else if (WindSpeed < 0.6)      // 40-60%
            return "A cold wind blows steadily.";
        else if (WindSpeed < 0.8)      // 60-80%
            return "Strong, bitter winds howl across the landscape.";
        else                           // 80-100%
            return "Powerful, freezing gusts threaten to knock you over.";
    }

    // Short-form labels for UI panels
    public string GetWindLabel()
    {
        if (WindSpeed < 0.2) return "Calm";
        if (WindSpeed < 0.4) return "Breezy";
        if (WindSpeed < 0.6) return "Windy";
        if (WindSpeed < 0.8) return "Strong";
        return "Fierce";
    }

    public string GetPrecipitationLabel()
    {
        if (Precipitation < 0.1) return "None";
        if (Precipitation < 0.3) return "Light";
        if (Precipitation < 0.6) return "Moderate";
        return "Heavy";
    }

    public string GetConditionLabel()
    {
        return CurrentCondition switch
        {
            WeatherCondition.Clear => "Clear",
            WeatherCondition.Cloudy => "Cloudy",
            WeatherCondition.Misty => "Misty",
            WeatherCondition.LightSnow => "Light Snow",
            WeatherCondition.Rainy => "Rain",
            WeatherCondition.Blizzard => "Blizzard",
            WeatherCondition.Stormy => "Storm",
            WeatherCondition.HeavySnow => "Heavy Snow",
            WeatherCondition.FreezingRain => "Freezing Rain",
            WeatherCondition.Whiteout => "Whiteout",
            _ => "Unknown"
        };
    }

    // Sunrise/sunset times based on season
    public int GetSunriseHour()
    {
        return CurrentSeason switch
        {
            Season.Winter => 8,
            Season.Summer => 4,
            _ => 6 // Spring/Fall
        };
    }

    public int GetSunsetHour()
    {
        return CurrentSeason switch
        {
            Season.Winter => 16,
            Season.Summer => 20,
            _ => 18 // Spring/Fall
        };
    }

    public double GetHoursUntilSunset(DateTime time)
    {
        int hour = time.Hour;
        int minute = time.Minute;
        int sunsetHour = GetSunsetHour();

        if (hour >= sunsetHour)
            return 0; // Already past sunset

        double currentTimeInHours = hour + (minute / 60.0);
        return sunsetHour - currentTimeInHours;
    }

    public double GetHoursUntilSunrise(DateTime time)
    {
        int hour = time.Hour;
        int minute = time.Minute;
        int sunriseHour = GetSunriseHour();

        double currentTimeInHours = hour + (minute / 60.0);

        if (hour < sunriseHour)
        {
            // Before sunrise today
            return sunriseHour - currentTimeInHours;
        }
        else
        {
            // After sunrise, calculate until next day's sunrise
            return (24 - currentTimeInHours) + sunriseHour;
        }
    }

    public bool IsDaytime(DateTime time)
    {
        int hour = time.Hour;
        return hour >= GetSunriseHour() && hour < GetSunsetHour();
    }

    /// <summary>
    /// Check if we're still in the early-game grace period where severe weather is excluded.
    /// </summary>
    private bool IsInGracePeriod(DateTime currentTime)
    {
        return (currentTime - _gameStartTime).TotalDays < GRACE_PERIOD_DAYS;
    }

    /// <summary>
    /// Advance to the next weather state within the current front,
    /// or transition to the next front if the current one is complete.
    /// </summary>
    private void AdvanceFront()
    {
        // If no fronts exist (old save), fall back to old generation
        if (CurrentFront == null)
        {
            GenerateNewWeather();
            return;
        }

        if (CurrentFront.IsComplete)
        {
            // Current front is done, move to next front
            CurrentFront = NextFront ?? FrontPatterns.Generate(CurrentSeason, CurrentCondition);

            // Generate next front - use gentle version during grace period
            if (IsInGracePeriod(Time))
            {
                NextFront = FrontPatterns.GenerateGentleFront(CurrentSeason, CurrentCondition);
            }
            else
            {
                NextFront = FrontPatterns.Generate(CurrentSeason, CurrentCondition);
            }

            CurrentFront.CurrentStateIndex = 0;
        }
        else
        {
            // Advance to next state in current front
            CurrentFront.CurrentStateIndex++;

            // Handle state skipping - if next state should be skipped, advance again
            while (CurrentFront.CurrentStateIndex < CurrentFront.States.Count &&
                   Utils.DetermineSuccess(CurrentFront.CurrentState.SkipProbability))
            {
                CurrentFront.CurrentStateIndex++;
            }
        }

        ApplyWeatherState(CurrentFront.CurrentState);

        // Track transition for event system
        PreviousCondition = CurrentCondition;
        WeatherJustChanged = true;
    }

    /// <summary>
    /// Apply a weather state to the current weather properties.
    /// Rolls within the state's ranges for variability.
    /// </summary>
    private void ApplyWeatherState(WeatherState state)
    {
        // Roll within state's ranges for variability
        BaseTemperature = Utils.RandDouble(state.TempRange.Min, state.TempRange.Max);
        WindSpeed = Utils.RandDouble(state.WindRange.Min, state.WindRange.Max);
        Precipitation = Utils.RandDouble(state.PrecipRange.Min, state.PrecipRange.Max);
        CloudCover = Utils.RandDouble(state.CloudRange.Min, state.CloudRange.Max);
        CurrentCondition = state.Condition;
        _weatherDuration = state.Duration;

        // Set wind direction based on wind speed
        if (WindSpeed < 0.2)
            CurrentWindDirection = WindDirection.Calm;
        else
            CurrentWindDirection = (WindDirection)Utils.Roll(8);  // 8 cardinal/intercardinal directions
    }

    /// <summary>
    /// Get the player-facing label for the current weather front.
    /// </summary>
    public string GetFrontLabel()
    {
        // Special handling for Prolonged Blizzard calm phase
        if (CurrentFront?.Type == FrontType.ProlongedBlizzard && CurrentFront.CurrentStateIndex == 0)
            return "Calm Before The Storm";

        return CurrentFront?.Type switch
        {
            FrontType.StormSystem => "Storm Front",
            FrontType.ProlongedBlizzard => "Blizzard",
            FrontType.IceStorm => "Ice Storm",
            FrontType.ClearSpell => "Clear Spell",
            FrontType.ColdSnap => "Cold Snap",
            FrontType.Warming => "Warming Period",
            FrontType.UnsettledPeriod => "Unsettled Weather",
            _ => "Stable Weather"
        };
    }
}


