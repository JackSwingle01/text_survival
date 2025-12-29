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
[JsonDerivedType(typeof(ConfirmOverlay), "confirm")]
[JsonDerivedType(typeof(ForageOverlay), "forage")]
[JsonDerivedType(typeof(DeathScreenOverlay), "deathScreen")]
[JsonDerivedType(typeof(HuntOverlay), "hunt")]
[JsonDerivedType(typeof(TransferOverlay), "transfer")]
[JsonDerivedType(typeof(FireOverlay), "fire")]
public abstract record Overlay;

/// <summary>
/// Inventory overlay: Shows full inventory details.
/// </summary>
public record InventoryOverlay(InventoryDto Data) : Overlay;

/// <summary>
/// Transfer overlay: Side-by-side inventory transfer view for camp storage.
/// </summary>
public record TransferOverlay(TransferDto Data) : Overlay;

/// <summary>
/// Fire overlay: Combined fire starting and tending UI.
/// </summary>
public record FireOverlay(FireManagementDto Data) : Overlay;

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
    bool IsAvailable
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
    string? HintText,
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
/// Hunt overlay: Interactive hunting popup with distance tracking and choices.
/// </summary>
public record HuntOverlay(HuntDto Data) : Overlay;

/// <summary>
/// Hunt state data for the hunt overlay popup.
/// </summary>
public record HuntDto(
    string AnimalName,
    string AnimalDescription,
    string AnimalActivity,
    string AnimalState,              // "idle", "alert", "detected"
    double CurrentDistanceMeters,
    double? PreviousDistanceMeters,  // For animation (null on first frame)
    bool IsAnimatingDistance,
    int MinutesSpent,
    string? StatusMessage,
    List<HuntChoiceDto> Choices,
    HuntOutcomeDto? Outcome          // null during choice phase
);

/// <summary>
/// A choice option in the hunt overlay.
/// </summary>
public record HuntChoiceDto(
    string Id,
    string Label,
    string? Description,
    bool IsAvailable,
    string? DisabledReason
);

/// <summary>
/// Hunt outcome when the hunt concludes.
/// </summary>
public record HuntOutcomeDto(
    string Result,                   // "success", "fled", "abandoned", "combat"
    string Message,
    int TotalMinutesSpent,
    List<string> ItemsGained,
    List<string> EffectsApplied,
    bool TransitionToCombat
);

/// <summary>
/// Death screen overlay: Game over popup with restart option.
/// </summary>
public record DeathScreenOverlay(DeathScreenDto Data) : Overlay;

/// <summary>
/// Data for the death screen overlay.
/// </summary>
public record DeathScreenDto(
    string CauseOfDeath,
    string TimeSurvived,
    double FinalVitality,
    double FinalCalories,
    double FinalHydration,
    double FinalTemperature
);
