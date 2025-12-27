using System.Text.Json.Serialization;

namespace text_survival.Web.Dto;

/// <summary>
/// Base class for UI modes. Exactly one mode is active at a time.
/// Determines which primary view the frontend renders.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LocationMode), "location")]
[JsonDerivedType(typeof(TravelMode), "travel")]
[JsonDerivedType(typeof(ProgressMode), "progress")]
public abstract record FrameMode;

/// <summary>
/// Location mode: Player is at a location, can take actions.
/// Shows narrative log and action buttons.
/// </summary>
public record LocationMode() : FrameMode;

/// <summary>
/// Travel mode: Grid map is active, player navigating tiles.
/// Shows canvas grid with clickable tiles.
/// </summary>
public record TravelMode(GridStateDto Grid) : FrameMode;

/// <summary>
/// Progress mode: Action in progress, showing animation.
/// Frontend animates progress bar locally.
/// </summary>
public record ProgressMode(string ActivityText, int EstimatedDurationSeconds) : FrameMode;
