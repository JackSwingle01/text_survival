using System.Text.Json.Serialization;

namespace text_survival.Web.Dto;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TravelMode), "travel")]
[JsonDerivedType(typeof(ProgressMode), "progress")]
[JsonDerivedType(typeof(TravelProgressMode), "travel_progress")]
[JsonDerivedType(typeof(CombatMode), "combat")]
public abstract record FrameMode;

public record TravelMode(GridStateDto Grid) : FrameMode;

public record ProgressMode(string ActivityText, int EstimatedDurationSeconds) : FrameMode;

public record TravelProgressMode(
    GridStateDto Grid,
    string ActivityText,
    int EstimatedDurationSeconds,
    int OriginX,
    int OriginY
) : FrameMode;

/// <summary>
/// Combat mode: Replaces the world map with the combat grid.
/// Uses the same canvas element, different content.
/// </summary>
public record CombatMode(CombatModeDto Combat) : FrameMode;

/// <summary>
/// Combat state for CombatMode. Streamlined version of CombatDto for mode-based rendering.
/// </summary>
public record CombatModeDto(
    // Grid with all combatants
    CombatGridDto Grid,

    // Distance to nearest enemy
    string DistanceZone,
    double DistanceMeters,

    // Phase and narrative
    CombatPhase Phase,
    string? NarrativeMessage,

    // Player state
    double PlayerVitality,
    double PlayerEnergy,

    // Threat factors (observable reasons for predator boldness)
    List<ThreatFactorDto> ThreatFactors,

    // Outcome (null during combat, populated when combat ends)
    CombatOutcomeDto? Outcome = null,

    // Auto-advance for AI turns (frontend auto-responds after delay)
    int? AutoAdvanceMs = null
);
