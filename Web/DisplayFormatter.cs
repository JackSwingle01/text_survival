using text_survival.Web.Dto;

namespace text_survival.Web;

/// <summary>
/// Centralized display formatting logic.
/// Keeps presentation concerns separate from game logic.
/// All threshold checks, color selections, and text formatting live here.
/// </summary>
public static class DisplayFormatter
{
    /// <summary>Badge types matching CSS classes in frontend.</summary>
    public static class BadgeType
    {
        public const string Danger = "danger";
        public const string Fire = "fire";
        public const string Water = "water";
        public const string Cold = "cold";
        public const string Warm = "warm";
        public const string Good = "good";
        public const string Neutral = "neutral";
    }

    /// <summary>Severity levels for stats and injuries.</summary>
    public static class Severity
    {
        public const string Good = "good";
        public const string Warning = "warning";
        public const string Danger = "danger";
        public const string Critical = "critical";
        public const string Severe = "severe";
        public const string Moderate = "moderate";
        public const string Minor = "minor";
    }

    /// <summary>Survival stat severity (health, food, water, energy).</summary>
    public static string StatSeverity(double percent) =>
        percent < 30 ? Severity.Danger :
        percent < 60 ? Severity.Warning :
        Severity.Good;

    /// <summary>Injury severity from tissue/organ condition.</summary>
    public static string InjurySeverity(double conditionPercent) =>
        conditionPercent <= 20 ? Severity.Critical :
        conditionPercent <= 50 ? Severity.Severe :
        conditionPercent <= 70 ? Severity.Moderate :
        Severity.Minor;

    /// <summary>Fire time display ("3hrs" or "45min").</summary>
    public static string FireTime(int minutes) =>
        minutes >= 60 ? $"{minutes / 60}hrs" : $"{minutes}min";

    /// <summary>Temperature trend arrow and color.</summary>
    public static TempTrendDisplay TempTrend(double trendPerHour) =>
        trendPerHour > 0.2 ? new TempTrendDisplay("↑", "#4caf50") :
        trendPerHour < -0.2 ? new TempTrendDisplay("↓", "#f44336") :
        new TempTrendDisplay("→", "#888");

    /// <summary>Background HSL from time of day (for day/night cycle).</summary>
    public static BackgroundDisplay BackgroundHsl(int minutesSinceMidnight)
    {
        // t = 0 at midnight, 1 at noon
        var t = minutesSinceMidnight <= 720
            ? minutesSinceMidnight / 720.0
            : (1440 - minutesSinceMidnight) / 720.0;

        // Deep Ocean color interpolation
        return new BackgroundDisplay(
            H: 215 + (212 - 215) * t,
            S: 30 + (25 - 30) * t,
            L: 5 + (26 - 5) * t
        );
    }

    /// <summary>Temperature badge CSS class (freezing, cold, warm, hot).</summary>
    public static string? TempBadgeClass(double tempF) =>
        tempF <= 20 ? "freezing" :
        tempF <= 40 ? "cold" :
        tempF >= 80 ? "hot" :
        tempF >= 60 ? "warm" :
        null;

    /// <summary>Body temp bar percentage (87-102°F range).</summary>
    public static int BodyTempBarPct(double bodyTempF) =>
        (int)Math.Clamp((bodyTempF - 87) / (102 - 87) * 100, 0, 100);

    /// <summary>Build glance badges for tile popup.</summary>
    public static List<BadgeDto> GlanceBadges(
        double? terrainHazardLevel,
        bool hasActiveFire,
        bool hasEmbers,
        bool hasWater,
        double? temperatureDeltaF,
        double? windFactor,
        bool? isDark,
        bool? isVantagePoint)
    {
        var badges = new List<BadgeDto>();

        // Priority 1: Safety hazards
        if (terrainHazardLevel is > 0.2)
        {
            var label = terrainHazardLevel > 0.5 ? "Dangerous" : "Hazardous";
            badges.Add(new BadgeDto("warning", label, BadgeType.Danger));
        }

        // Priority 2: Key resources
        if (hasActiveFire)
        {
            badges.Add(new BadgeDto("local_fire_department", "Fire", BadgeType.Fire));
        }
        else if (hasEmbers)
        {
            badges.Add(new BadgeDto("fireplace", "Embers", BadgeType.Fire));
        }

        if (hasWater)
        {
            badges.Add(new BadgeDto("water_drop", "Water", BadgeType.Water));
        }

        // Priority 3: Temperature effects
        if (temperatureDeltaF is < -5)
        {
            badges.Add(new BadgeDto("ac_unit", "Cold", BadgeType.Cold));
        }
        else if (temperatureDeltaF is > 5)
        {
            badges.Add(new BadgeDto("sunny", "Warm", BadgeType.Warm));
        }

        // Priority 4: Wind exposure
        if (windFactor is > 1.3)
        {
            badges.Add(new BadgeDto("air", "Exposed", BadgeType.Danger));
        }
        else if (windFactor is < 0.7)
        {
            badges.Add(new BadgeDto("forest", "Sheltered", BadgeType.Good));
        }

        // Priority 5: Special conditions
        if (isDark == true)
        {
            badges.Add(new BadgeDto("dark_mode", "Dark", BadgeType.Neutral));
        }

        if (isVantagePoint == true)
        {
            badges.Add(new BadgeDto("visibility", "Vantage", BadgeType.Good));
        }

        return badges.Take(4).ToList();
    }

    /// <summary>Format effect tooltip lines.</summary>
    public static List<string> EffectTooltipLines(
        Dictionary<string, int>? capacityImpacts,
        EffectStatsDto? statsImpact,
        bool requiresTreatment)
    {
        var lines = new List<string>();

        // Capacity impacts
        if (capacityImpacts != null)
        {
            foreach (var (cap, impact) in capacityImpacts)
            {
                var sign = impact > 0 ? "+" : "";
                lines.Add($"{cap}: {sign}{impact}%");
            }
        }

        // Stat impacts
        if (statsImpact != null)
        {
            if (statsImpact.TemperaturePerHour is { } temp and not 0)
            {
                var sign = temp > 0 ? "+" : "";
                lines.Add($"Temp: {sign}{temp:F1}°F/hr");
            }
            if (statsImpact.HydrationPerHour is { } hyd and not 0)
            {
                var sign = hyd > 0 ? "+" : "";
                lines.Add($"Hydration: {sign}{hyd:F0}ml/hr");
            }
            if (statsImpact.CaloriesPerHour is { } cal and not 0)
            {
                var sign = cal > 0 ? "+" : "";
                lines.Add($"Calories: {sign}{cal:F0}/hr");
            }
            if (statsImpact.EnergyPerHour is { } energy and not 0)
            {
                var sign = energy > 0 ? "+" : "";
                lines.Add($"Energy: {sign}{energy:F0}/hr");
            }
            if (statsImpact.DamagePerHour is { } dmg)
            {
                lines.Add($"{statsImpact.DamageType ?? "Damage"}: {dmg:F1}/hr");
            }
        }

        if (requiresTreatment)
        {
            lines.Add("Requires treatment");
        }

        return lines;
    }
}

// Display DTOs
public record BadgeDto(string Icon, string Label, string Type);
public record TempTrendDisplay(string Arrow, string Color);
public record BackgroundDisplay(double H, double S, double L);
public record SurvivalStatDisplay(int Value, string Severity);

// Forward reference to EffectStatsDto (defined in GameStateDto.cs)
// The EffectTooltipLines method uses it as a parameter
