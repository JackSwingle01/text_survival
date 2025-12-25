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
public abstract class LocationFeature
{
    public string Name { get; set; } = string.Empty;
    public LocationFeature(string name)
    {
        Name = name;
    }
    public LocationFeature() { } // Parameterless constructor for deserialization
    public virtual void Update(int minutes) {}
    // public virtual void Initialize() { }
}



