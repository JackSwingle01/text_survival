namespace text_survival.Actors.Animals;

/// <summary>
/// All animal types in the game. Centralizes animal identity to eliminate string-based lookups.
/// </summary>
public enum AnimalType
{
    // Large prey
    Caribou,
    Megaloceros,
    Bison,
    Mammoth,

    // Predators
    Wolf,
    Bear,
    CaveBear,
    SaberTooth,
    Hyena,

    // Small game
    Rabbit,
    Ptarmigan,
    Fox,
    Rat,
    Fish
}

/// <summary>
/// Extension methods for AnimalType - single source of truth for all animal properties.
/// </summary>
public static class AnimalTypes
{
    /// <summary>
    /// Human-readable display name for the animal.
    /// </summary>
    public static string DisplayName(this AnimalType type) => type switch
    {
        AnimalType.Caribou => "Caribou",
        AnimalType.Megaloceros => "Megaloceros",
        AnimalType.Bison => "Bison",
        AnimalType.Mammoth => "Mammoth",
        AnimalType.Wolf => "Wolf",
        AnimalType.Bear => "Bear",
        AnimalType.CaveBear => "CaveBear",
        AnimalType.SaberTooth => "SaberTooth",
        AnimalType.Hyena => "Hyena",
        AnimalType.Rabbit => "Rabbit",
        AnimalType.Ptarmigan => "Ptarmigan",
        AnimalType.Fox => "Fox",
        AnimalType.Rat => "Rat",
        AnimalType.Fish => "Fish",
        _ => type.ToString()
    };

    /// <summary>
    /// Emoji icon for UI display.
    /// </summary>
    public static string Emoji(this AnimalType type) => type switch
    {
        AnimalType.Caribou => "🦌",
        AnimalType.Megaloceros => "🦌",
        AnimalType.Bison => "🦬",
        AnimalType.Mammoth => "🦣",
        AnimalType.Wolf => "🐺",
        AnimalType.Bear => "🐻",
        AnimalType.CaveBear => "🐻",
        AnimalType.SaberTooth => "🐅",
        AnimalType.Hyena => "🐕",
        AnimalType.Rabbit => "🐇",
        AnimalType.Ptarmigan => "🐦",
        AnimalType.Fox => "🦊",
        AnimalType.Rat => "🐀",
        AnimalType.Fish => "🐟",
        _ => "🐾"
    };

    /// <summary>
    /// Whether this animal is a predator (hunts other animals, potential threat to player).
    /// </summary>
    public static bool IsPredator(this AnimalType type) => type switch
    {
        AnimalType.Wolf or AnimalType.Bear or AnimalType.CaveBear
            or AnimalType.SaberTooth or AnimalType.Hyena => true,
        _ => false
    };

    /// <summary>
    /// Whether this is small game (spawn-based hunting, not persistent herds).
    /// </summary>
    public static bool IsSmallGame(this AnimalType type) => type switch
    {
        AnimalType.Rabbit or AnimalType.Ptarmigan or AnimalType.Fox or AnimalType.Fish => true,
        _ => false
    };

    /// <summary>
    /// Whether this animal is a bird (yields feathers instead of hide).
    /// </summary>
    public static bool IsBird(this AnimalType type) => type switch
    {
        AnimalType.Ptarmigan => true,
        _ => false
    };

    /// <summary>
    /// Detection range in tiles for herd awareness.
    /// </summary>
    public static int BaseDetectionRange(this AnimalType type) => type switch
    {
        AnimalType.Wolf => 3,
        AnimalType.SaberTooth => 3,  // Stealthy ambush hunter
        AnimalType.Bear or AnimalType.CaveBear => 2,
        AnimalType.Hyena => 2,
        AnimalType.Mammoth => 2,
        AnimalType.Caribou or AnimalType.Megaloceros or AnimalType.Bison => 2,
        _ => 2
    };

    /// <summary>
    /// Typical weight in kg for this animal type.
    /// </summary>
    public static double WeightKg(this AnimalType type) => type switch
    {
        AnimalType.Mammoth => 5000,
        AnimalType.Bison => 800,
        AnimalType.Megaloceros => 600,
        AnimalType.CaveBear => 350,
        AnimalType.Bear => 250,
        AnimalType.Caribou => 120,
        AnimalType.Hyena => 70,
        AnimalType.Wolf => 40,
        AnimalType.Fox => 6,
        AnimalType.Rabbit => 2,
        AnimalType.Rat => 0.3,
        AnimalType.Ptarmigan => 0.5,
        AnimalType.Fish => 1,
        _ => 10
    };

    /// <summary>
    /// Diet type for foraging/grazing behavior.
    /// </summary>
    public static AnimalDiet GetDiet(this AnimalType type) => type switch
    {
        AnimalType.Bear or AnimalType.CaveBear => AnimalDiet.Omnivore,
        AnimalType.Caribou or AnimalType.Megaloceros => AnimalDiet.Browser,
        AnimalType.Mammoth => AnimalDiet.Browser,
        AnimalType.Bison => AnimalDiet.Grazer,
        AnimalType.Wolf or AnimalType.SaberTooth or AnimalType.Hyena => AnimalDiet.Carnivore,
        AnimalType.Fox => AnimalDiet.Carnivore,
        _ => AnimalDiet.Carnivore  // Default to no foraging competition
    };

    /// <summary>
    /// Herd behavior type for AI.
    /// </summary>
    public static HerdBehaviorType GetBehaviorType(this AnimalType type) => type switch
    {
        AnimalType.Wolf => HerdBehaviorType.PackPredator,
        AnimalType.Bear or AnimalType.CaveBear => HerdBehaviorType.SolitaryPredator,
        AnimalType.SaberTooth => HerdBehaviorType.SolitaryPredator,
        AnimalType.Hyena => HerdBehaviorType.Scavenger,
        _ => HerdBehaviorType.Prey
    };

    /// <summary>
    /// Parse a string to AnimalType, handling common aliases.
    /// Returns null if the string doesn't match any known animal.
    /// </summary>
    public static AnimalType? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        return name.ToLower().Trim() switch
        {
            "caribou" => AnimalType.Caribou,
            "megaloceros" => AnimalType.Megaloceros,
            "bison" or "steppe bison" => AnimalType.Bison,
            "mammoth" or "woolly mammoth" => AnimalType.Mammoth,
            "wolf" => AnimalType.Wolf,
            "bear" => AnimalType.Bear,
            "cave bear" or "cavebear" => AnimalType.CaveBear,
            "saber-tooth" or "saber tooth tiger" or "sabertooth" => AnimalType.SaberTooth,
            "hyena" or "cave hyena" => AnimalType.Hyena,
            "rabbit" => AnimalType.Rabbit,
            "ptarmigan" => AnimalType.Ptarmigan,
            "fox" => AnimalType.Fox,
            "rat" => AnimalType.Rat,
            "fish" => AnimalType.Fish,
            _ => null
        };
    }

    /// <summary>
    /// Parse a string to AnimalType, throwing if not found.
    /// </summary>
    public static AnimalType ParseRequired(string name)
    {
        return Parse(name) ?? throw new ArgumentException($"Unknown animal type: {name}");
    }
}
