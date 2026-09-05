using text_survival.Environments.Grid;

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
/// Species temperament: how a herd's engage chance is built up.
/// </summary>
/// <param name="Base">Engage chance with nothing else going for it.</param>
/// <param name="PerPackMember">Added per living member.</param>
/// <param name="HungryAt">Hunger above which HungerBonus applies once.</param>
/// <param name="StarvingAt">Hunger above which HungerBonus applies a second time.</param>
/// <param name="HungerBonus">Added at each hunger threshold.</param>
/// <param name="DefendBonus">Added when defending a den, kill, or carcass.</param>
/// <param name="Cap">Maximum engage chance.</param>
public record Temperament(double Base, double PerPackMember, double HungryAt, double StarvingAt, double HungerBonus, double DefendBonus, double Cap);

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
    /// The kind of print this animal leaves. Only herd animals ever mark the map, so
    /// the split is simply predator or prey - which is also the only thing a player
    /// can read from prints at map scale, and the only thing they act on.
    /// </summary>
    public static TrackMaker Tracks(this AnimalType type) =>
        type.IsPredator() ? TrackMaker.Paw : TrackMaker.Hoof;

    /// <summary>
    /// How heavily one of these marks the ground, with an adult human at 1.0. Scales
    /// with the square root of body mass, so a mammoth prints far deeper than a fox
    /// without printing seventy times deeper.
    /// </summary>
    public static double IndividualTrackDepth(this AnimalType type) =>
        Math.Clamp(Math.Sqrt(type.WeightKg() / 70.0), 0.4, 3.0);

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
        AnimalType.Wolf => 12,
        AnimalType.SaberTooth => 12,  // Stealthy ambush hunter
        AnimalType.Bear or AnimalType.CaveBear => 8,
        AnimalType.Hyena => 8,
        AnimalType.Mammoth => 8,
        AnimalType.Caribou or AnimalType.Megaloceros or AnimalType.Bison => 8,
        _ => 8
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
    /// How readily a species engages a target. The inputs to <see cref="Herd.BoldnessToward"/>.
    /// Prey and megafauna herbivores never hunt the player, so their cap is 0.
    /// </summary>
    public static Temperament Temperament(this AnimalType type) => type switch
    {
        AnimalType.Wolf => new(Base: 0.20, PerPackMember: 0.05, HungryAt: 0.7, StarvingAt: 0.9, HungerBonus: 0.2, DefendBonus: 0.6, Cap: 0.9),
        AnimalType.Bear or AnimalType.CaveBear => new(Base: 0.15, PerPackMember: 0, HungryAt: 0.8, StarvingAt: 0.95, HungerBonus: 0.3, DefendBonus: 0.55, Cap: 1.0),
        AnimalType.SaberTooth => new(Base: 0.25, PerPackMember: 0, HungryAt: 0.7, StarvingAt: 0.9, HungerBonus: 0.3, DefendBonus: 0.5, Cap: 1.0),
        AnimalType.Hyena => new(Base: 0.10, PerPackMember: 0.08, HungryAt: 0.8, StarvingAt: 1.1, HungerBonus: 0.2, DefendBonus: 0.15, Cap: 0.6),
        _ => new(Base: 0, PerPackMember: 0, HungryAt: 1.1, StarvingAt: 1.1, HungerBonus: 0, DefendBonus: 0, Cap: 0)
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

}
