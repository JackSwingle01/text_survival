using System.Text.Json.Serialization;

namespace text_survival.Web.Dto;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LocationMode), "location")]
[JsonDerivedType(typeof(TravelMode), "travel")]
[JsonDerivedType(typeof(ProgressMode), "progress")]
[JsonDerivedType(typeof(TravelProgressMode), "travel_progress")]
public abstract record FrameMode;

public record LocationMode() : FrameMode;

public record TravelMode(GridStateDto Grid) : FrameMode;

public record ProgressMode(string ActivityText, int EstimatedDurationSeconds) : FrameMode;

public record TravelProgressMode(
    GridStateDto Grid,
    string ActivityText,
    int EstimatedDurationSeconds,
    int OriginX,
    int OriginY
) : FrameMode;
