using text_survival.Actors.Animals;

namespace text_survival.Actions.Variants;

/// <summary>
/// Bundles animal-specific behavior with matched descriptions.
/// Ensures event text matches what the animal would actually do.
/// Eliminates wolf/bear branching logic scattered across events.
/// </summary>
public record AnimalTypeVariant(
    AnimalType AnimalType,     
    string TacticsDescription,  // What the player sees about tactics
    string WaitDescription,     // Description for "wait for it to leave" choice
    double FireEffectiveness,   // 0-1 how well fire/smoke works
    double NoiseEffectiveness,  // 0-1 how well noise/intimidation works
    double AmbushChance,        // Likelihood of ambush-style attacks
    bool IsDiurnal,             // Hunts during day (wolves) vs nocturnal/hibernating
    double PackBonus,           // Weight modifier when in group
    // === STALKING BEHAVIOR ===
    double StalkingPersistence, // 0-1 how likely to keep following after losing sight
    double ChaseThreshold,      // 0-1 how easily running triggers chase (higher = more likely to chase)
    // === PACK BEHAVIOR ===
    double PackCoordination,    // 0-1 how well they coordinate (wolves high, bears N/A)
    string CirclingDescription  // Description for "They're circling" events
);

/// <summary>
/// Predefined animal behavior variants.
/// </summary>
public static class AnimalVariants
{
    public static readonly AnimalTypeVariant Wolf = new(
        AnimalType.Wolf,
        "Wolves. Fire works. Noise might work. They hunt during the day — maybe wait.",
        "Wolves hunt during the day. Maybe it's out.",
        FireEffectiveness: 0.7,
        NoiseEffectiveness: 0.5,
        AmbushChance: 0.3,
        IsDiurnal: true,
        PackBonus: 1.5,
        StalkingPersistence: 0.7,   // Wolves are persistent trackers
        ChaseThreshold: 0.6,        // Will chase if you run, but test first
        PackCoordination: 0.9,      // Excellent pack coordination
        CirclingDescription: "The wolves move in unison, cutting off escape routes."
    );

    public static readonly AnimalTypeVariant Bear = new(
        AnimalType.Bear,
        "Bear. Fire works best — smoke them out. Noise alone won't help.",
        "Wait and watch. Maybe an opportunity.",
        FireEffectiveness: 0.85,
        NoiseEffectiveness: 0.15,
        AmbushChance: 0.1,
        IsDiurnal: false,
        PackBonus: 1.0,
        StalkingPersistence: 0.3,   // Bears don't stalk long
        ChaseThreshold: 0.4,        // Less likely to chase, but devastating if they do
        PackCoordination: 0.0,      // Solitary
        CirclingDescription: "The bear watches, waiting for you to make a mistake."
    );

    public static readonly AnimalTypeVariant Fox = new(
        AnimalType.Fox,
        "Fox. Skittish. Noise works. Fire unnecessary.",
        "Wait quietly. It won't stay long.",
        FireEffectiveness: 0.5,
        NoiseEffectiveness: 0.9,
        AmbushChance: 0.05,
        IsDiurnal: true,
        PackBonus: 1.0,
        StalkingPersistence: 0.2,   // Easily discouraged
        ChaseThreshold: 0.1,        // Almost never chases humans
        PackCoordination: 0.1,      // Mostly solitary
        CirclingDescription: "The fox darts between cover, watching."
    );

    /// <summary>
    /// Default for unknown animals - cautious approach.
    /// </summary>
    public static readonly AnimalTypeVariant Unknown = new(
        AnimalType.Wolf,  // Default to wolf behavior
        "Something territorial. Proceed with caution.",
        "Wait and watch. Maybe an opportunity.",
        FireEffectiveness: 0.6,
        NoiseEffectiveness: 0.4,
        AmbushChance: 0.2,
        IsDiurnal: false,
        PackBonus: 1.2,
        StalkingPersistence: 0.5,
        ChaseThreshold: 0.5,
        PackCoordination: 0.3,
        CirclingDescription: "Something moves at the edge of your vision."
    );

    // === SMALL GAME ===
    // Quick opportunity targets - not threats, but rewards for prepared players

    public static readonly AnimalTypeVariant Rabbit = new(
        AnimalType.Rabbit,
        "Rabbit. Freeze response. Quick hands or a good throw.",
        "Wait quietly. It might come closer.",
        FireEffectiveness: 0.0,  // Fire irrelevant
        NoiseEffectiveness: 0.0, // Noise just spooks them
        AmbushChance: 0.15,      // Small chance to catch by hand
        IsDiurnal: true,
        PackBonus: 1.0,
        StalkingPersistence: 0.0, // They don't stalk
        ChaseThreshold: 0.0,      // They don't chase
        PackCoordination: 0.0,
        CirclingDescription: "N/A"
    );

    public static readonly AnimalTypeVariant Ptarmigan = new(
        AnimalType.Ptarmigan,
        "Ptarmigan. Ground bird. Reluctant to fly if you move slowly.",
        "Wait. They're calmer than most birds.",
        FireEffectiveness: 0.0,
        NoiseEffectiveness: 0.0,
        AmbushChance: 0.2,       // Slightly easier than rabbit
        IsDiurnal: true,
        PackBonus: 1.3,          // Often in groups
        StalkingPersistence: 0.0,
        ChaseThreshold: 0.0,
        PackCoordination: 0.0,
        CirclingDescription: "N/A"
    );

    public static readonly AnimalTypeVariant Squirrel = new(
        AnimalType.Rabbit,  // Use generic small game type
        "Squirrel. Fast, arboreal. Caches are worth more than the animal.",
        "Watch where it goes. It might lead to a cache.",
        FireEffectiveness: 0.0,
        NoiseEffectiveness: 0.0,
        AmbushChance: 0.05,      // Very hard to catch
        IsDiurnal: true,
        PackBonus: 1.0,
        StalkingPersistence: 0.0,
        ChaseThreshold: 0.0,
        PackCoordination: 0.0,
        CirclingDescription: "N/A"
    );

    public static readonly AnimalTypeVariant Fish = new(
        AnimalType.Fish,
        "Fish. Visible in shallows. Spear or patience required.",
        "Wait for them to settle. Movement spooks them.",
        FireEffectiveness: 0.0,
        NoiseEffectiveness: 0.0,
        AmbushChance: 0.25,      // Easier with spear
        IsDiurnal: true,
        PackBonus: 1.5,          // Often in schools
        StalkingPersistence: 0.0,
        ChaseThreshold: 0.0,
        PackCoordination: 0.0,
        CirclingDescription: "N/A"
    );

    public static readonly AnimalTypeVariant Grouse = new(
        AnimalType.Ptarmigan,  // Use ptarmigan as ground bird type
        "Grouse. Camouflaged until disturbed. Explosive takeoff.",
        "Mark where they land. Approach from downwind.",
        FireEffectiveness: 0.0,
        NoiseEffectiveness: 0.0,
        AmbushChance: 0.1,
        IsDiurnal: true,
        PackBonus: 1.2,
        StalkingPersistence: 0.0,
        ChaseThreshold: 0.0,
        PackCoordination: 0.0,
        CirclingDescription: "N/A"
    );
}

/// <summary>
/// Selects appropriate animal variant based on animal name.
/// </summary>
public static class AnimalSelector
{
    public static AnimalTypeVariant GetVariant(AnimalType animalType)
    {
        var typeStr = animalType.ToString().ToLower();
        return typeStr switch
        {
            var t when t.Contains(AnimalType.Wolf.ToString().ToLower()) => AnimalVariants.Wolf,
            var t when t.Contains(AnimalType.Bear.ToString().ToLower()) => AnimalVariants.Bear,
            var t when t.Contains(AnimalType.Fox.ToString().ToLower()) => AnimalVariants.Fox,
            // Small game
            var t when t.Contains(AnimalType.Rabbit.ToString().ToLower()) => AnimalVariants.Rabbit,
            var t when t.Contains(AnimalType.Ptarmigan.ToString().ToLower()) => AnimalVariants.Ptarmigan,
            var t when t.Contains("squirrel") => AnimalVariants.Squirrel,
            var t when t.Contains(AnimalType.Fish.ToString().ToLower()) => AnimalVariants.Fish,
            var t when t.Contains("grouse") => AnimalVariants.Grouse,
            _ => AnimalVariants.Unknown
        };
    }

    /// <summary>
    /// Returns true if this is a small game animal (not a predator threat).
    /// </summary>
    public static bool IsSmallGame(AnimalTypeVariant variant)
        => variant == AnimalVariants.Rabbit ||
           variant == AnimalVariants.Ptarmigan ||
           variant == AnimalVariants.Squirrel ||
           variant == AnimalVariants.Fish ||
           variant == AnimalVariants.Grouse;

    /// <summary>
    /// Get a random small game variant for sighting events.
    /// </summary>
    public static AnimalTypeVariant GetRandomSmallGame()
    {
        var smallGame = new[] {
            AnimalVariants.Rabbit,
            AnimalVariants.Ptarmigan,
            AnimalVariants.Squirrel,
            AnimalVariants.Grouse
        };
        return smallGame[Random.Shared.Next(smallGame.Length)];
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

    // === STALKING HELPERS ===

    /// <summary>
    /// Calculate weight for "stalker continues following" outcomes.
    /// Higher persistence = more likely to keep tracking.
    /// </summary>
    public static double LoseTrailSuccessWeight(AnimalTypeVariant variant)
        => 1.0 - variant.StalkingPersistence;

    /// <summary>
    /// Calculate weight for "running triggers chase" outcomes.
    /// Higher threshold = more likely to chase.
    /// </summary>
    public static double ChaseTriggeredWeight(AnimalTypeVariant variant)
        => variant.ChaseThreshold;

    /// <summary>
    /// Calculate weight for successful evasion when retreating slowly.
    /// Low chase threshold + low persistence = better chance.
    /// </summary>
    public static double SlowRetreatSuccessWeight(AnimalTypeVariant variant)
        => (1.0 - variant.ChaseThreshold) * (1.0 - variant.StalkingPersistence * 0.5);

    // === PACK HELPERS ===

    /// <summary>
    /// Returns true if this animal typically operates in packs.
    /// </summary>
    public static bool IsPredatorPackAnimal(AnimalTypeVariant variant)
        => variant.PackCoordination > 0.5;

    /// <summary>
    /// Get weight modifier for pack-based attacks.
    /// High coordination = more dangerous group tactics.
    /// </summary>
    public static double PackTacticsWeight(AnimalTypeVariant variant)
        => variant.PackCoordination * variant.PackBonus;
}
