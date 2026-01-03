using text_survival.Actors.Animals;
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
    AnimalType? AnimalType = null,
    string? Direction = null,
    string? Description = null
);

/// <summary>
/// Configuration for spawning a predator encounter from an event outcome.
/// </summary>
public record EncounterConfig(
    AnimalType AnimalType,
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
/// Unified gear damage specification - applies to all gear types via durability loss.
/// Use ToolType to target a specific tool, Slot to target equipment, or Category for general damage.
/// </summary>
public record GearDamage(
    GearCategory Category,
    int DurabilityLoss,
    ToolType? ToolType = null,
    EquipSlot? Slot = null
);

/// <summary>
/// Gear repair specification - restores durability to equipment or tools.
/// </summary>
public record GearRepair(
    GearCategory Category,
    int DurabilityGain,
    ToolType? ToolType = null,
    EquipSlot? Slot = null
);
