using text_survival;
using text_survival.Environments;

public class ZoneWeather
{

    // todo: improve sunrise and sunset logic,
    // todo: add more continuity and state to more granular changes
    public double BaseTemperature { get; private set; } // In Celsius
    public WeatherCondition CurrentCondition { get; private set; }
    public double Precipitation { get; private set; } // 0-1 intensity
    public double WindSpeed { get; private set; }    // 0-1 intensity
    public double CloudCover { get; private set; }   // 0-1 coverage

    // Season tracking
    public enum Season { Winter, Spring, Summer, Fall }
    public Season CurrentSeason { get; private set; } = Season.Fall; // Start in fall

    // Weather conditions for Ice Age Europe
    public enum WeatherCondition
    {
        Clear,      // Clear, cold skies
        Cloudy,     // Overcast conditions
        Misty,      // Low visibility with moisture
        LightSnow,  // Light snowfall (common in Ice Age)
        Rainy,      // Cold rain (uncommon but possible)
        Blizzard,   // Heavy snow with wind (dangerous)
        Stormy      // Thunderstorms (rare, mostly summer)
    }

    // Add to ZoneWeather class
    public double SunlightIntensity
    {
        get
        {
            // No sun at night
            if (!IsDaytime())
                return 0;

            // Base sun intensity from time of day
            double timeOfDayFactor = GetSunIntensityByTime();

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
            }
            // Calculate final intensity (0-1)
            double baseIntensity = timeOfDayFactor * (1 - cloudReduction);
            return baseIntensity * (1 - conditionReduction);
        }
    }

    private bool IsDaytime()
    {
        // todo: flesh this out and combine it with the temperature cycle 
        int hour = World.Time.Hour;

        // Seasonal variation in daylight hours
        int sunriseHour, sunsetHour;

        switch (CurrentSeason)
        {
            case Season.Winter:
                sunriseHour = 8;  // Late sunrise
                sunsetHour = 16;  // Early sunset
                break;
            case Season.Spring:
            case Season.Fall:
                sunriseHour = 6;  // Normal sunrise
                sunsetHour = 18;  // Normal sunset
                break;
            case Season.Summer:
                sunriseHour = 4;  // Early sunrise
                sunsetHour = 20;  // Late sunset
                break;
            default:
                sunriseHour = 6;
                sunsetHour = 18;
                break;
        }

        return hour >= sunriseHour && hour < sunsetHour;
    }

    private double GetSunIntensityByTime()
    {
        // Get sun intensity purely based on time of day (0-1)
        int hour = World.Time.Hour;
        int minute = World.Time.Minute;

        // No sunlight before sunrise or after sunset
        if (!IsDaytime())
            return 0;

        // Convert to minutes since sunrise (0-720)
        double minutesSinceSunrise = ((hour - 6) * 60) + minute;
        double dayLengthMinutes = 12 * 60; // 12 hours of daylight

        // Calculate angle for sine function (0 to π over the day)
        double angle = (minutesSinceSunrise / dayLengthMinutes) * Math.PI;

        // Sine wave peaks at noon (6 hours after sunrise)
        return Math.Sin(angle);
    }

    // Time and update tracking
    private TimeSpan _weatherDuration;
    private TimeSpan _timeSinceChange = TimeSpan.Zero;

    // Zone this weather belongs to
    private Zone _zone;

    public ZoneWeather(Zone zone)
    {
        _zone = zone;

        // Initialize with fall weather
        BaseTemperature = 0; // 0°C is about 32°F - freezing point
        CurrentCondition = WeatherCondition.Clear;
        Precipitation = 0;
        WindSpeed = 0.3; // Moderate wind - 30% of maximum
        CloudCover = 0.3; // Light clouds - 30% coverage

        _weatherDuration = TimeSpan.FromHours(6);
    }

    public void Update(TimeSpan elapsed)
    {
        _timeSinceChange += elapsed;

        // Time to change weather?
        if (_timeSinceChange >= _weatherDuration)
        {
            GenerateNewWeather();
            _timeSinceChange = TimeSpan.Zero;
        }
    }

    private void GenerateNewWeather()
    {
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
        if (_zone.Elevation > 0)
        {
            // Higher elevation = colder (-0.6°C per 100m elevation)
            double elevationEffect = _zone.Elevation * -0.006; // -0.6% per 100m
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

        BaseTemperature = minTemp + (temperatureRange * randomFlux * timeOfDayFactor);

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

        int currentMinute = World.Time.Hour * 60 + World.Time.Minute;
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

    // Set season
    public void SetSeason(Season season)
    {
        CurrentSeason = season;
        GenerateNewWeather(); // Update weather for new season
    }
}


