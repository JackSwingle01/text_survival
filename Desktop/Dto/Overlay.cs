using System.Text.Json.Serialization;

namespace text_survival.Desktop.Dto;

/// <summary>
/// Base class for UI overlays. Multiple overlays can be active simultaneously.
/// Overlays stack on top of the base mode.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EventOverlay), "event")]
[JsonDerivedType(typeof(ConfirmOverlay), "confirm")]
[JsonDerivedType(typeof(ForageOverlay), "forage")]
[JsonDerivedType(typeof(EatingOverlay), "eating")]
[JsonDerivedType(typeof(DiscoveryOverlay), "discovery")]
[JsonDerivedType(typeof(WeatherChangeOverlay), "weatherChange")]
[JsonDerivedType(typeof(DiscoveryLogOverlay), "discoveryLog")]
public abstract record Overlay;


/// <summary>
/// Eating overlay: Interactive eating and drinking UI with progress bar.
/// </summary>
public record EatingOverlay(EatingOverlayDto Data) : Overlay;

/// <summary>
/// Data for the eating overlay.
/// </summary>
public record EatingOverlayDto(
    int CaloriesPercent,
    int HydrationPercent,
    List<ConsumableItemDto> Foods,
    List<ConsumableItemDto> Drinks,
    ConsumableItemDto? SpecialAction
);

/// <summary>
/// A consumable item (food or drink).
/// </summary>
public record ConsumableItemDto(
    string Id,
    string Name,
    string Amount,
    int? CaloriesEstimate,
    int? HydrationEstimate,
    string? Warning
);

/// <summary>
/// Discovery overlay: Simple location discovery popup with name + text + Continue.
/// </summary>
public record DiscoveryOverlay(DiscoveryDto Data) : Overlay;

/// <summary>
/// Data for the discovery overlay - shown on first visit to named locations.
/// </summary>
public record DiscoveryDto(
    string LocationName,
    string DiscoveryText
);

/// <summary>
/// Weather change overlay: Simple notification popup when weather changes.
/// </summary>
public record WeatherChangeOverlay(WeatherChangeDto Data) : Overlay;

/// <summary>
/// Data for the weather change overlay - shown when weather transitions.
/// </summary>
public record WeatherChangeDto(
    string WeatherCondition,
    string WeatherFront
);

/// <summary>
/// Event overlay: Popup for narrative events requiring player choice.
/// </summary>
public record EventOverlay(EventDto Data) : Overlay;

/// <summary>
/// Confirm overlay: Simple yes/no confirmation prompt.
/// </summary>
public record ConfirmOverlay(string Prompt) : Overlay;

/// <summary>
/// Event data for popup display.
/// When Outcome is non-null and Choices is empty, the popup shows the outcome phase.
/// </summary>
public record EventDto(
    string Name,
    string Description,
    List<EventChoiceDto> Choices,
    EventOutcomeDto? Outcome = null
);

/// <summary>
/// Stat changes during work or events.
/// </summary>
public record StatsDeltaDto(
    double EnergyDelta,
    double CaloriesDelta,
    double HydrationDelta,
    double TemperatureDelta
);

/// <summary>
/// Outcome data for event popup display after a choice is made.
/// </summary>
public record EventOutcomeDto(
    string Message,
    int TimeAddedMinutes,
    List<string> EffectsApplied,
    List<string> DamageTaken,
    List<string> ItemsGained,
    List<string> ItemsLost,
    List<string> TensionsChanged,
    StatsDeltaDto? StatsDelta = null
);

/// <summary>
/// A choice option within an event popup.
/// </summary>
public record EventChoiceDto(
    string Id,
    string Label,
    string Description,
    bool IsAvailable,
    string? Cost = null  // e.g., "2 medicine" or null if free
);

/// <summary>
/// Forage overlay: Popup for foraging with clues, focus, and time selection.
/// </summary>
public record ForageOverlay(ForageDto Data) : Overlay;

/// <summary>
/// Data for the forage overlay popup.
/// </summary>
public record ForageDto(
    string LocationQuality,
    List<ForageClueDto> Clues,
    List<ForageFocusDto> FocusOptions,
    List<ForageTimeDto> TimeOptions,
    List<string> Warnings
);

/// <summary>
/// An environmental clue in the forage overlay.
/// </summary>
public record ForageClueDto(
    string Id,
    string Description,
    string? SuggestedFocusId
);

/// <summary>
/// A focus option in the forage overlay.
/// </summary>
public record ForageFocusDto(string Id, string Label, string Description);

/// <summary>
/// A time option in the forage overlay.
/// </summary>
public record ForageTimeDto(string Id, string Label, int Minutes);

/// <summary>
/// Discovery Log overlay: Shows all player discoveries organized by category.
/// </summary>
public record DiscoveryLogOverlay(DiscoveryLogDto Data) : Overlay;

/// <summary>
/// Data for the Discovery Log overlay.
/// </summary>
public record DiscoveryLogDto(
    List<DiscoveryLogCategoryDto> Categories
);

/// <summary>
/// A category in the Discovery Log with discovered and remaining items.
/// </summary>
public record DiscoveryLogCategoryDto(
    string Name,           // "The Land", "Beasts", "Provisions", "Medicine", "Works"
    string CountDisplay,   // "5 discovered" or "12 / ~25"
    List<string> Discovered,   // Names of discovered items
    int RemainingCount     // Number of "???" entries to show
);
