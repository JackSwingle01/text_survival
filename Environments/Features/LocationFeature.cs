using System.Text.Json.Serialization;
using text_survival.Environments.Surface;

namespace text_survival.Environments.Features;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ForageFeature), "forage")]
[JsonDerivedType(typeof(HarvestableFeature), "harvestable")]
[JsonDerivedType(typeof(HeatSourceFeature), "heatSource")]
[JsonDerivedType(typeof(SmallGameFeature), "animalTerritory")]
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
[JsonDerivedType(typeof(NPCBodyFeature), "npcbody")]
[JsonDerivedType(typeof(EventTriggerFeature), "eventTrigger")]
[JsonDerivedType(typeof(GroundItemsFeature), "groundItems")]
[JsonDerivedType(typeof(NetFishingFeature), "netFishing")]
public abstract class LocationFeature
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stable key for this thing, so a half-dug hole in the snow over it outlives a save.
    /// Older saves get a fresh one on load, which is harmless because they had no hole.
    /// </summary>
    public string PlacementId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// Where this sits, against <see cref="Surface.GroundSurface.SurfaceHeightM"/>. Null is
    /// ground level, where everything the world was born with sits;
    /// <see cref="Location.AddFeature"/> places anything dropped during play on the surface
    /// as it was at the time. Once set it stays set - snow moves the surface, not the object.
    /// </summary>
    public double? PlacedBaseElevationM { get; set; }

    /// <summary>How tall it stands. A berry bush is not buried by an inch of snow.</summary>
    [JsonIgnore]
    public virtual double PlacementHeightM => 0.3;

    /// <summary>How much ground has to be cleared to get at it.</summary>
    [JsonIgnore]
    public virtual double PlacementFootprintM2 => 0.6;

    /// <summary>
    /// Whether snow over this stops you using it. False for anything that is a property of
    /// the whole tile rather than an object on it - foraging, trees - and for a fire, whose
    /// ground you would have cleared before lighting it.
    /// </summary>
    [JsonIgnore]
    public virtual bool BlockedByCover => false;

    /// <summary>
    /// Whether the player knows this is here. Something they have not found must not
    /// advertise itself by offering to be dug up.
    /// </summary>
    [JsonIgnore]
    public virtual bool IsKnownToPlayer => true;

    /// <summary>What to call it when offering to dig it out.</summary>
    [JsonIgnore]
    public virtual string AccessName => Name;

    [JsonIgnore]
    public SurfacePlacement Placement =>
        new(PlacementId, PlacedBaseElevationM ?? 0, PlacementHeightM, PlacementFootprintM2);

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
    public virtual void Update(int minutes) { }
    public abstract List<Resource> ProvidedResources();

}


