using System.Text.Json.Serialization;

namespace text_survival.Environments.Features;

/// <summary>
/// Context passed to features during the update loop.
/// Provides environmental information without features needing to reach back to Location.
/// </summary>
public class FeatureUpdateContext
{
    /// <summary>
    /// Temperature at the location in Fahrenheit.
    /// </summary>
    public double TemperatureF { get; init; }

    /// <summary>
    /// Minutes elapsed since last update.
    /// </summary>
    public int Minutes { get; init; }

    public FeatureUpdateContext(int minutes, double temperatureF)
    {
        Minutes = minutes;
        TemperatureF = temperatureF;
    }
}

/// <summary>
/// UI-agnostic feature information for display.
/// Keeps domain code independent of web DTOs.
/// </summary>
public record FeatureUIInfo(
    string Type,           // "shelter", "forage", "animal", etc.
    string Label,          // Display name
    string? Status,        // e.g., "75% insulation", "abundant"
    List<string>? Details  // Additional details like resource types
);

[JsonDerivedType(typeof(ForageFeature), "forage")]
[JsonDerivedType(typeof(HarvestableFeature), "harvestable")]
[JsonDerivedType(typeof(HeatSourceFeature), "heatSource")]
[JsonDerivedType(typeof(AnimalTerritoryFeature), "animalTerritory")]
[JsonDerivedType(typeof(ShelterFeature), "shelter")]
[JsonDerivedType(typeof(CacheFeature), "cache")]
[JsonDerivedType(typeof(SnareLineFeature), "snareLine")]
[JsonDerivedType(typeof(WaterFeature), "water")]
[JsonDerivedType(typeof(SalvageFeature), "salvage")]
[JsonDerivedType(typeof(CuringRackFeature), "curingRack")]
[JsonDerivedType(typeof(BeddingFeature), "bedding")]
[JsonDerivedType(typeof(CraftingProjectFeature), "craftingProject")]
[JsonDerivedType(typeof(WoodedAreaFeature), "woodedArea")]
[JsonDerivedType(typeof(EnvironmentalDetail), "environmentalDetail")]
[JsonDerivedType(typeof(CarcassFeature), "carcass")]
public abstract class LocationFeature
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Material symbol name for map display. Null = don't show on map.
    /// </summary>
    [JsonIgnore]
    public virtual string? MapIcon => null;

    /// <summary>
    /// Icon priority for tile display (higher = shown first when space limited).
    /// </summary>
    [JsonIgnore]
    public virtual int IconPriority => 0;

    public LocationFeature(string name)
    {
        Name = name;
    }
    public LocationFeature() { } // Parameterless constructor for deserialization

    /// <summary>
    /// Update feature state for elapsed time with environmental context.
    /// </summary>
    /// <param name="ctx">Context containing elapsed time and environmental data.</param>
    public virtual void Update(FeatureUpdateContext ctx) {}

    /// <summary>
    /// Returns UI display information for this feature.
    /// Override in feature classes to provide self-describing UI info.
    /// Returns null if feature should not be displayed (destroyed, depleted, etc.)
    /// </summary>
    public virtual FeatureUIInfo? GetUIInfo() => null;
}



