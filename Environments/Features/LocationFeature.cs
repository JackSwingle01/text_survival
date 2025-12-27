using System.Text.Json.Serialization;

namespace text_survival.Environments.Features;

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
    public virtual void Update(int minutes) {}
}



