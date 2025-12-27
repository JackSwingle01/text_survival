using System.Text.Json.Serialization;

namespace text_survival.Web.Dto;

/// <summary>
/// Base class for UI overlays. Multiple overlays can be active simultaneously.
/// Overlays stack on top of the base mode.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InventoryOverlay), "inventory")]
[JsonDerivedType(typeof(CraftingOverlay), "crafting")]
[JsonDerivedType(typeof(EventOverlay), "event")]
[JsonDerivedType(typeof(HazardOverlay), "hazard")]
public abstract record Overlay;

/// <summary>
/// Inventory overlay: Shows full inventory details.
/// </summary>
public record InventoryOverlay(InventoryDto Data) : Overlay;

/// <summary>
/// Crafting overlay: Shows crafting recipes organized by category.
/// </summary>
public record CraftingOverlay(CraftingDto Data) : Overlay;

/// <summary>
/// Event overlay: Popup for narrative events requiring player choice.
/// </summary>
public record EventOverlay(EventDto Data) : Overlay;

/// <summary>
/// Hazard overlay: Popup for hazardous terrain choice (quick vs careful).
/// </summary>
public record HazardOverlay(HazardPromptDto Data) : Overlay;

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
    string Label,
    string Description,
    bool IsAvailable
);
