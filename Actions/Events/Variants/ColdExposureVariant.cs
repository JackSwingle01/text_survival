using text_survival.Effects;

namespace text_survival.Actions.Variants;

/// <summary>
/// Exposure type determines the cold source and appropriate responses.
/// </summary>
public enum ColdExposureType
{
    ExtremeCold,    // Ambient temperature dangerously low
    WetCold,        // Rain/precipitation + cold
    WindChill       // High wind stripping body heat
}

/// <summary>
/// Bundles cold exposure description with matched mechanics.
/// Ensures the event text matches the actual cold source.
/// </summary>
public record ColdExposureVariant(
    ColdExposureType ExposureType,
    string EventName,             // "Frostbite Warning", "Cold Rain Soaking", etc.
    string Description,           // Event intro text
    Effect[] BaseEffects,         // Auto-applied effects (wetness for rain)
    double FrostbiteRisk = 0      // Base frostbite probability modifier
);

/// <summary>
/// Variant pools for cold exposure events.
/// Each pool has variants specific to the cold source type.
/// </summary>
public static class ColdExposureVariants
{
    /// <summary>
    /// Extreme cold exposure - ambient temperature dangerously low.
    /// No precipitation or wind component - just raw cold.
    /// </summary>
    public static readonly ColdExposureVariant[] ExtremeCold =
    [
        new(ColdExposureType.ExtremeCold,
            "Frostbite Warning",
            "Your fingers have gone white. You can't feel your toes. This is getting serious.",
            [],
            FrostbiteRisk: 0.3),
        new(ColdExposureType.ExtremeCold,
            "Killing Cold",
            "The cold cuts through you. Your extremities are going numb.",
            [],
            FrostbiteRisk: 0.25),
        new(ColdExposureType.ExtremeCold,
            "Frozen Breath",
            "Ice crystals form on your eyelashes. Every breath burns. This is dangerous cold.",
            [],
            FrostbiteRisk: 0.35),
    ];

    /// <summary>
    /// Wet cold exposure - rain/sleet soaking through clothes.
    /// Wetness is the primary threat multiplier.
    /// </summary>
    public static readonly ColdExposureVariant[] WetCold =
    [
        new(ColdExposureType.WetCold,
            "Cold Rain Soaking",
            "The rain is seeping through everything. You're getting dangerously wet in freezing conditions.",
            [EffectFactory.Wet(0.4)]),
        new(ColdExposureType.WetCold,
            "Freezing Rain",
            "Freezing rain soaks through your outer layer. Your clothes cling, stealing your warmth.",
            [EffectFactory.Wet(0.5)]),
        new(ColdExposureType.WetCold,
            "Driving Sleet",
            "Sleet drives through gaps in your clothing. Water runs down your back.",
            [EffectFactory.Wet(0.45)]),
    ];

    /// <summary>
    /// Wind chill exposure - high wind stripping body heat.
    /// Wind is the primary threat; no wetness component.
    /// </summary>
    public static readonly ColdExposureVariant[] WindChill =
    [
        new(ColdExposureType.WindChill,
            "Bitter Wind",
            "The wind cuts through your clothes like they're not there. Your body heat is being stripped away.",
            []),
        new(ColdExposureType.WindChill,
            "Brutal Gusts",
            "Gusts hammer you from all sides. The wind chill is brutal.",
            []),
        new(ColdExposureType.WindChill,
            "Piercing Wind",
            "The wind finds every gap in your clothing. Cold air rushes in with each gust.",
            []),
    ];
}

/// <summary>
/// Selects appropriate cold exposure variants based on weather context.
/// </summary>
public static class ColdExposureSelector
{
    /// <summary>
    /// Select a cold exposure variant based on current weather conditions.
    /// Priority: WetCold > WindChill > ExtremeCold (wetness is most dangerous)
    /// </summary>
    public static ColdExposureVariant SelectByWeather(GameContext ctx)
    {
        // Rain/precipitation takes precedence (wetness most dangerous)
        if (ctx.Check(EventCondition.IsRaining) && ctx.Check(EventCondition.LowTemperature))
            return SelectFromPool(ColdExposureVariants.WetCold);

        // High wind is next priority
        if (ctx.Check(EventCondition.HighWind))
            return SelectFromPool(ColdExposureVariants.WindChill);

        // Default to extreme cold
        return SelectFromPool(ColdExposureVariants.ExtremeCold);
    }

    /// <summary>
    /// Select a specific exposure type variant.
    /// </summary>
    public static ColdExposureVariant SelectByType(ColdExposureType type)
    {
        return type switch
        {
            ColdExposureType.ExtremeCold => SelectFromPool(ColdExposureVariants.ExtremeCold),
            ColdExposureType.WetCold => SelectFromPool(ColdExposureVariants.WetCold),
            ColdExposureType.WindChill => SelectFromPool(ColdExposureVariants.WindChill),
            _ => SelectFromPool(ColdExposureVariants.ExtremeCold)
        };
    }

    private static ColdExposureVariant SelectFromPool(ColdExposureVariant[] pool)
    {
        return pool[Random.Shared.Next(pool.Length)];
    }
}
