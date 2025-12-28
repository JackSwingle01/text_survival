namespace text_survival.Actions.Variants;

/// <summary>
/// Bundles animal-specific behavior with matched descriptions.
/// Ensures event text matches what the animal would actually do.
/// Eliminates wolf/bear branching logic scattered across events.
/// </summary>
public record AnimalTypeVariant(
    string AnimalType,          // "Wolf", "Bear", etc.
    string TacticsDescription,  // What the player sees about tactics
    string WaitDescription,     // Description for "wait for it to leave" choice
    double FireEffectiveness,   // 0-1 how well fire/smoke works
    double NoiseEffectiveness,  // 0-1 how well noise/intimidation works
    double AmbushChance,        // Likelihood of ambush-style attacks
    bool IsDiurnal,             // Hunts during day (wolves) vs nocturnal/hibernating
    double PackBonus            // Weight modifier when in group
);

/// <summary>
/// Predefined animal behavior variants.
/// </summary>
public static class AnimalVariants
{
    public static readonly AnimalTypeVariant Wolf = new(
        "Wolf",
        "Wolves. Fire works. Noise might work. They hunt during the day — maybe wait.",
        "Wolves hunt during the day. Maybe it's out.",
        FireEffectiveness: 0.7,
        NoiseEffectiveness: 0.5,
        AmbushChance: 0.3,
        IsDiurnal: true,
        PackBonus: 1.5
    );

    public static readonly AnimalTypeVariant Bear = new(
        "Bear",
        "Bear. Fire works best — smoke them out. Noise alone won't help.",
        "Wait and watch. Maybe an opportunity.",
        FireEffectiveness: 0.85,
        NoiseEffectiveness: 0.15,
        AmbushChance: 0.1,
        IsDiurnal: false,  // Less predictable
        PackBonus: 1.0
    );

    public static readonly AnimalTypeVariant Fox = new(
        "Fox",
        "Fox. Skittish. Noise works. Fire unnecessary.",
        "Wait quietly. It won't stay long.",
        FireEffectiveness: 0.5,
        NoiseEffectiveness: 0.9,
        AmbushChance: 0.05,
        IsDiurnal: true,
        PackBonus: 1.0
    );

    public static readonly AnimalTypeVariant Lynx = new(
        "Lynx",
        "Lynx. Solitary. Fire works. Will avoid confrontation if possible.",
        "Wait. Lynx prefer to slip away unseen.",
        FireEffectiveness: 0.75,
        NoiseEffectiveness: 0.6,
        AmbushChance: 0.4,
        IsDiurnal: false,
        PackBonus: 1.0
    );

    /// <summary>
    /// Default for unknown animals - cautious approach.
    /// </summary>
    public static readonly AnimalTypeVariant Unknown = new(
        "Unknown",
        "Something territorial. Proceed with caution.",
        "Wait and watch. Maybe an opportunity.",
        FireEffectiveness: 0.6,
        NoiseEffectiveness: 0.4,
        AmbushChance: 0.2,
        IsDiurnal: false,
        PackBonus: 1.2
    );
}

/// <summary>
/// Selects appropriate animal variant based on animal name.
/// </summary>
public static class AnimalSelector
{
    public static AnimalTypeVariant GetVariant(string? animalType)
    {
        if (string.IsNullOrEmpty(animalType))
            return AnimalVariants.Wolf;  // Default to wolf

        return animalType.ToLower() switch
        {
            var t when t.Contains("wolf") => AnimalVariants.Wolf,
            var t when t.Contains("bear") => AnimalVariants.Bear,
            var t when t.Contains("fox") => AnimalVariants.Fox,
            var t when t.Contains("lynx") => AnimalVariants.Lynx,
            _ => AnimalVariants.Unknown
        };
    }

    /// <summary>
    /// Get wait outcomes based on animal behavior.
    /// Diurnal animals (wolves) have different wait patterns than others.
    /// </summary>
    public static bool IsDiurnalHunter(AnimalTypeVariant variant) => variant.IsDiurnal;

    /// <summary>
    /// Calculate success chance for noise intimidation.
    /// </summary>
    public static double NoiseSuccessWeight(AnimalTypeVariant variant) => variant.NoiseEffectiveness;

    /// <summary>
    /// Calculate success chance for fire/smoke tactics.
    /// </summary>
    public static double FireSuccessWeight(AnimalTypeVariant variant) => variant.FireEffectiveness;
}
