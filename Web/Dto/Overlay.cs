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
[JsonDerivedType(typeof(CookingOverlay), "cooking")]
[JsonDerivedType(typeof(ButcherOverlay), "butcher")]
[JsonDerivedType(typeof(EncounterOverlay), "encounter")]
[JsonDerivedType(typeof(CombatOverlay), "combat")]
[JsonDerivedType(typeof(EatingOverlay), "eating")]
[JsonDerivedType(typeof(DiscoveryOverlay), "discovery")]
[JsonDerivedType(typeof(WeatherChangeOverlay), "weatherChange")]
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
/// Cooking overlay: Cooking and snow melting at fire.
/// </summary>
public record CookingOverlay(CookingDto Data) : Overlay;

/// <summary>
/// Data for the cooking overlay.
/// </summary>
public record CookingDto(
    List<CookingOptionDto> Options,
    double WaterLiters,
    double RawMeatKg,
    double CookedMeatKg,
    CookingResultDto? LastResult
);

/// <summary>
/// A cooking action option.
/// </summary>
public record CookingOptionDto(
    string Id,
    string Label,
    string Icon,
    int TimeMinutes,
    bool IsAvailable,
    string? DisabledReason
);

/// <summary>
/// Result of a cooking action (for feedback display).
/// </summary>
public record CookingResultDto(
    string Message,
    string Icon,
    bool IsSuccess
);

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

/// <summary>
/// Butcher overlay: Popup for butchering with mode and time selection.
/// </summary>
public record ButcherOverlay(ButcherDto Data) : Overlay;

/// <summary>
/// Data for the butcher overlay popup.
/// </summary>
public record ButcherDto(
    string AnimalName,
    string DecayStatus,
    double RemainingKg,
    bool IsFrozen,
    List<ButcherModeDto> ModeOptions,
    List<string> Warnings
);

/// <summary>
/// A butchering mode option in the butcher overlay.
/// </summary>
public record ButcherModeDto(
    string Id,
    string Label,
    string Description,
    int EstimatedMinutes
);

// ============================================
// ENCOUNTER OVERLAY
// ============================================

/// <summary>
/// Encounter overlay: Predator encounter popup with distance and boldness tracking.
/// </summary>
public record EncounterOverlay(EncounterDto Data) : Overlay;

/// <summary>
/// Encounter state data for the predator encounter overlay popup.
/// </summary>
public record EncounterDto(
    string PredatorName,
    double CurrentDistanceMeters,
    double? PreviousDistanceMeters,      // For animation (null on first frame)
    bool IsAnimatingDistance,
    double BoldnessLevel,                 // 0-1
    string BoldnessDescriptor,            // "aggressive", "wary", "hesitant"
    List<ThreatFactorDto> ThreatFactors,  // Observable factors making predator bold
    string? StatusMessage,
    List<EncounterChoiceDto> Choices,
    EncounterOutcomeDto? Outcome          // null during choice phase
);

/// <summary>
/// An observable factor affecting predator boldness (shown to player).
/// </summary>
public record ThreatFactorDto(
    string Id,                            // "meat", "weakness", "blood"
    string Description,
    string Icon                           // Material icon name
);

/// <summary>
/// A choice option in the encounter overlay.
/// </summary>
public record EncounterChoiceDto(
    string Id,                            // "stand", "back", "run", "fight", "drop_meat"
    string Label,
    string? Description,
    bool IsAvailable,
    string? DisabledReason
);

/// <summary>
/// Encounter outcome when the encounter concludes (before combat if fight chosen).
/// </summary>
public record EncounterOutcomeDto(
    string Result,                        // "retreated", "escaped", "fight", "died"
    string Message
);

// ============================================
// COMBAT OVERLAY (Distance-Based Strategic Combat)
// ============================================

/// <summary>
/// Combat phase for pacing narrative beats.
/// Each phase gets its own frame with appropriate UI.
/// </summary>
public enum CombatPhase
{
    Intro,           // "A wolf attacks!" - dramatic opening
    PlayerChoice,    // Waiting for player to select action
    PlayerAction,    // Result of player's action
    AnimalAction,    // Animal's attack/charge result
    BehaviorChange,  // Animal behavior transition
    Outcome          // Combat ended
}

/// <summary>
/// Combat overlay: Distance-based strategic combat with defensive options and readable tells.
/// Unified system that handles both pre-combat (encounter) and active combat phases.
/// </summary>
public record CombatOverlay(CombatDto Data) : Overlay;

/// <summary>
/// Combat state data for the strategic combat overlay.
/// Uses distance zones, animal behavior states, and descriptive health (no HP bars).
/// </summary>
public record CombatDto(
    // Animal info
    string AnimalName,
    string AnimalHealthDescription,       // "wounded", "badly hurt", "staggering" (no HP bar)
    string AnimalConditionNarrative,      // "Blood mats the wolf's fur."

    // Distance and positioning
    string DistanceZone,                  // "Melee", "Close", "Mid", "Far"
    double DistanceMeters,
    double? PreviousDistanceMeters,       // For animation

    // Animal behavior (readable tells)
    string BehaviorState,                 // "Circling", "Threatening", "CHARGING!", etc.
    string BehaviorDescription,           // The readable tell text

    // Player state (for UI display)
    double PlayerVitality,                // Still 0-1 for player's own HP display
    double PlayerEnergy,                  // 0-1, affects action effectiveness
    bool PlayerBraced,                    // Whether player has set a brace

    // Aggression/boldness (for gauge display)
    double BoldnessLevel,                 // 0-1 for fill bar width
    string BoldnessDescriptor,            // "cautious", "wary", "bold", "aggressive"

    // Phase and narrative
    CombatPhase Phase,                    // Current combat phase for pacing
    string? NarrativeMessage,             // Current narrative beat (replaces IntroMessage/LastActionMessage)
    List<CombatActionDto> Actions,        // Available actions (only shown in PlayerChoice phase)

    // Threat factors (carried over from encounter, shown to player)
    List<ThreatFactorDto> ThreatFactors,

    // Outcome (null during combat)
    CombatOutcomeDto? Outcome,

    // 2D Grid visualization (new)
    CombatGridDto? Grid = null            // null if grid not initialized
);

/// <summary>
/// An action available during combat. Actions vary by distance zone.
/// </summary>
public record CombatActionDto(
    string Id,                            // "attack", "dodge", "advance", etc.
    string Label,
    string? Description,                  // Hover text / additional info
    bool IsAvailable,
    string? DisabledReason,
    string? HitChance                     // e.g., "65% hit" for attacks, null for non-attacks
);

/// <summary>
/// A choice option in the combat overlay (legacy compatibility).
/// </summary>
public record CombatChoiceDto(
    string Id,                            // "attack", "flee"
    string Label,
    bool IsAvailable,
    string? DisabledReason
);

/// <summary>
/// Combat outcome when combat concludes.
/// </summary>
public record CombatOutcomeDto(
    string Result,                        // "victory", "defeat", "fled", "animal_fled", "disengaged"
    string Message,
    List<string>? Rewards                 // Items gained on victory
);

/// <summary>
/// A position on the combat grid.
/// </summary>
public record CombatGridPositionDto(
    int X,
    int Y
);

/// <summary>
/// An actor on the combat grid (player, enemy, ally).
/// </summary>
public record CombatGridActorDto(
    string Id,
    string Name,
    string Team,                          // "player", "enemy", "ally"
    CombatGridPositionDto Position,
    string? BehaviorState,                // null for player
    double? Vitality,                     // 0-1, null if not shown
    bool IsAlpha                          // Pack alpha (for visual indicator)
);

/// <summary>
/// Combat grid data for 2D visualization.
/// </summary>
public record CombatGridDto(
    int GridSize,                         // e.g., 25 for 25x25
    double CellSizeMeters,                // e.g., 1.0
    List<CombatGridActorDto> Actors,
    string? Terrain = null,               // e.g., "Forest", "Plain" for background
    int? LocationX = null,                // World X coordinate for seeded textures
    int? LocationY = null                 // World Y coordinate for seeded textures
);
