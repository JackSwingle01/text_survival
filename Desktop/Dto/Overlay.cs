using System.Text.Json.Serialization;

namespace text_survival.Desktop.Dto;

/// <summary>
/// Base class for UI overlays. Multiple overlays can be active simultaneously.
/// Overlays stack on top of the base mode.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EventOverlay), "event")]
[JsonDerivedType(typeof(ConfirmOverlay), "confirm")]
[JsonDerivedType(typeof(DiscoveryLogOverlay), "discoveryLog")]
public abstract record Overlay;

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
/// Outcome data for event popup display after a choice is made.
/// </summary>
public record EventOutcomeDto(
    string Message,
    int TimeAddedMinutes,
    List<string> EffectsApplied,
    List<string> DamageTaken,
    List<string> ItemsGained,
    List<string> ItemsLost,
    List<string> TensionsChanged
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
