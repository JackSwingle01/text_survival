using text_survival.Environments;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Configuration for creating a new tension from an event outcome.
/// Uses explicit properties instead of Dictionary<string, object>.
/// </summary>
public record TensionCreation(
    string Type,
    double Severity,
    Location? RelevantLocation = null,
    string? AnimalType = null,
    string? Direction = null,
    string? Description = null
);

/// <summary>
/// Configuration for spawning a predator encounter from an event outcome.
/// </summary>
public record EncounterConfig(
    string AnimalType,
    double InitialDistance,
    double InitialBoldness,
    List<string>? Modifiers = null
);

/// <summary>
/// Template for dynamically discovering locations through events.
/// </summary>
public record LocationTemplate(
    string NamePattern,
    int TravelTime,
    string? Direction = null
);

/// <summary>
/// Configuration for adding a feature to a location.
/// </summary>
public record FeatureCreation(
    Type FeatureType,
    object? Config = null
);

/// <summary>
/// Configuration for modifying an existing feature.
/// </summary>
public record FeatureModification(
    Type FeatureType,
    double? DepleteAmount = null
);

/// <summary>
/// Tool damage specification - uses integer durability loss.
/// </summary>
public record ToolDamage(
    ToolType Type,
    int UsesLost
);

/// <summary>
/// Clothing damage specification - directly reduces insulation.
/// </summary>
public record ClothingDamage(
    EquipSlot Slot,
    double InsulationLoss
);
