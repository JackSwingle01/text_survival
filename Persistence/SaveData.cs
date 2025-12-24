using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Persistence;

/// <summary>
/// Root save data structure containing all game state.
/// </summary>
public record GameSaveData
{
    public int SaveVersion { get; init; } = 1;
    public DateTime GameTime { get; init; }
    public PlayerSaveData Player { get; init; } = new();
    public InventorySaveData PlayerInventory { get; init; } = new();
    public InventorySaveData CampStorage { get; init; } = new();
    public ZoneSaveData Zone { get; init; } = new();
    public string CampLocationName { get; init; } = "";
    public ExpeditionSaveData? Expedition { get; init; }  // null = at camp
    public string CurrentActivity { get; init; } = "Idle";
    public EncounterConfigSaveData? PendingEncounter { get; init; }
    public List<TensionSaveData> Tensions { get; init; } = [];
    public List<LogEntrySaveData> NarrativeLog { get; init; } = [];
    public Dictionary<string, DateTime> EventTriggerTimes { get; init; } = new();
}

/// <summary>
/// Narrative log entry.
/// </summary>
public record LogEntrySaveData(string Text, string Level);

/// <summary>
/// Player state including body and effects.
/// </summary>
public record PlayerSaveData
{
    public BodySaveData Body { get; init; } = new();
    public List<EffectSaveData> Effects { get; init; } = [];
}

/// <summary>
/// Body state including survival stats, composition, and part conditions.
/// </summary>
public record BodySaveData
{
    // Survival stats
    public double CalorieStore { get; init; }
    public double Energy { get; init; }
    public double Hydration { get; init; }
    public double BodyTemperature { get; init; }

    // Body composition
    public double BodyFatKG { get; init; }
    public double MuscleKG { get; init; }
    public double BloodCondition { get; init; }

    // Body part conditions (matched by name on restore)
    public List<BodyPartSaveData> Parts { get; init; } = [];
    public List<OrganSaveData> Organs { get; init; } = [];
}

/// <summary>
/// Body region tissue conditions.
/// </summary>
public record BodyPartSaveData(string Name, double SkinCondition, double MuscleCondition, double BoneCondition);

/// <summary>
/// Organ condition.
/// </summary>
public record OrganSaveData(string Name, double Condition);

/// <summary>
/// Active effect state.
/// </summary>
public record EffectSaveData
{
    // Identity
    public string EffectKind { get; init; } = "";
    public string? TargetBodyPart { get; init; }

    // State
    public double Severity { get; init; }
    public double HourlySeverityChange { get; init; }
    public bool RequiresTreatment { get; init; }
    public bool CanHaveMultiple { get; init; }

    // Effects on stats/capacities
    public Dictionary<string, double> CapacityModifiers { get; init; } = [];
    public StatsDeltaSaveData? StatsDelta { get; init; }
    public DamageOverTimeSaveData? Damage { get; init; }

    // Messages
    public string? ApplicationMessage { get; init; }
    public string? RemovalMessage { get; init; }
}

public record StatsDeltaSaveData(
    double TemperatureDelta,
    double CalorieDelta,
    double HydrationDelta,
    double EnergyDelta
);

public record DamageOverTimeSaveData(double PerHour, string DamageType);

/// <summary>
/// Inventory state for both player and camp storage.
/// </summary>
public record InventorySaveData
{
    public double MaxWeightKg { get; init; }

    // Fuel
    public Stack<double> Logs { get; init; } = new();
    public Stack<double> Sticks { get; init; } = new();
    public Stack<double> Tinder { get; init; } = new();

    // Food
    public Stack<double> CookedMeat { get; init; } = new();
    public Stack<double> RawMeat { get; init; } = new();
    public Stack<double> Berries { get; init; } = new();

    // Water
    public double WaterLiters { get; init; }

    // Crafting materials
    public Stack<double> Stone { get; init; } = new();
    public Stack<double> Bone { get; init; } = new();
    public Stack<double> Hide { get; init; } = new();
    public Stack<double> PlantFiber { get; init; } = new();
    public Stack<double> Sinew { get; init; } = new();

    // Stone types
    public Stack<double> Shale { get; init; } = new();
    public Stack<double> Flint { get; init; } = new();
    public double Pyrite { get; init; }

    // Wood types
    public Stack<double> Pine { get; init; } = new();
    public Stack<double> Birch { get; init; } = new();
    public Stack<double> Oak { get; init; } = new();
    public Stack<double> BirchBark { get; init; } = new();

    // Fungi
    public Stack<double> BirchPolypore { get; init; } = new();
    public Stack<double> Chaga { get; init; } = new();
    public Stack<double> Amadou { get; init; } = new();

    // Persistent plants
    public Stack<double> RoseHips { get; init; } = new();
    public Stack<double> JuniperBerries { get; init; } = new();
    public Stack<double> WillowBark { get; init; } = new();
    public Stack<double> PineNeedles { get; init; } = new();

    // Tree products
    public Stack<double> PineResin { get; init; } = new();
    public Stack<double> Usnea { get; init; } = new();
    public Stack<double> Sphagnum { get; init; } = new();

    // Produced
    public double Charcoal { get; init; }

    // Food expansion
    public Stack<double> Nuts { get; init; } = new();
    public Stack<double> Roots { get; init; } = new();
    public Stack<double> DriedMeat { get; init; } = new();
    public Stack<double> DriedBerries { get; init; } = new();

    // Processing states
    public Stack<double> ScrapedHide { get; init; } = new();
    public Stack<double> CuredHide { get; init; } = new();
    public Stack<double> RawFiber { get; init; } = new();
    public Stack<double> RawFat { get; init; } = new();
    public Stack<double> Tallow { get; init; } = new();

    // Discrete items
    public List<ToolSaveData> Tools { get; init; } = [];
    public List<ToolSaveData> Special { get; init; } = [];

    // Equipment slots
    public EquipmentSaveData? Head { get; init; }
    public EquipmentSaveData? Chest { get; init; }
    public EquipmentSaveData? Legs { get; init; }
    public EquipmentSaveData? Feet { get; init; }
    public EquipmentSaveData? Hands { get; init; }
    public ToolSaveData? Weapon { get; init; }

    // Active torch state
    public ToolSaveData? ActiveTorch { get; init; }
    public double TorchBurnTimeRemainingMinutes { get; init; }
}

/// <summary>
/// Tool state.
/// </summary>
public record ToolSaveData
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public double Weight { get; init; }
    public int Durability { get; init; }
    public double? Damage { get; init; }
    public double? BlockChance { get; init; }
    public string? WeaponClass { get; init; }
}

/// <summary>
/// Equipment (armor/clothing) state.
/// </summary>
public record EquipmentSaveData
{
    public string Name { get; init; } = "";
    public string Slot { get; init; } = "";
    public double Weight { get; init; }
    public double Insulation { get; init; }
}

/// <summary>
/// Zone state including weather and locations.
/// </summary>
public record ZoneSaveData
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public WeatherSaveData Weather { get; init; } = new();
    public List<LocationSaveData> Locations { get; init; } = [];
    public List<LocationSaveData> UnrevealedLocations { get; init; } = [];
}

/// <summary>
/// Weather state.
/// </summary>
public record WeatherSaveData
{
    public double BaseTemperature { get; init; }
    public string CurrentCondition { get; init; } = "Clear";
    public double Precipitation { get; init; }
    public double WindSpeed { get; init; }
    public double CloudCover { get; init; }
    public string CurrentSeason { get; init; } = "Fall";
}

/// <summary>
/// Location state including features and connections.
/// </summary>
public record LocationSaveData
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Tags { get; init; } = "";
    public bool Explored { get; init; }
    public List<Guid> ConnectionIds { get; init; } = [];

    // Terrain properties
    public int BaseTraversalMinutes { get; init; }
    public double TerrainHazardLevel { get; init; }
    public double WindFactor { get; init; }
    public double OverheadCoverLevel { get; init; }
    public double VisibilityFactor { get; init; }
    public bool IsDark { get; init; }

    public string? DiscoveryText { get; init; }

    // Features
    public List<FeatureSaveData> Features { get; init; } = [];
}

/// <summary>
/// Feature state - discriminated by FeatureType.
/// </summary>
public record FeatureSaveData
{
    public string FeatureType { get; init; } = "";
    public string Name { get; init; } = "";

    // HeatSourceFeature
    public bool? HasEmbers { get; init; }
    public double? UnburnedMassKg { get; init; }
    public double? BurningMassKg { get; init; }
    public double? MaxFuelCapacityKg { get; init; }
    public double? EmberTimeRemaining { get; init; }
    public double? EmberDuration { get; init; }
    public double? EmberStartTemperature { get; init; }
    public double? LastBurningTemperature { get; init; }
    public Dictionary<string, double>? UnburnedMixture { get; init; }
    public Dictionary<string, double>? BurningMixture { get; init; }

    // ForageFeature
    public double? BaseResourceDensity { get; init; }
    public double? NumberOfHoursForaged { get; init; }
    public double? HoursSinceLastForage { get; init; }
    public bool? HasForagedBefore { get; init; }
    public List<ForageResourceSaveData>? ForageResources { get; init; }

    // AnimalTerritoryFeature
    public double? BaseGameDensity { get; init; }
    public double? GameDensity { get; init; }
    public double? InitialDepletedDensity { get; init; }
    public double? HoursSinceLastHunt { get; init; }
    public bool? HasBeenHunted { get; init; }
    public List<AnimalSpawnSaveData>? PossibleAnimals { get; init; }

    // ShelterFeature
    public double? TemperatureInsulation { get; init; }
    public double? OverheadCoverage { get; init; }
    public double? WindCoverage { get; init; }

    // SnareLineFeature
    public List<SnareSaveData>? Snares { get; init; }

    // CacheFeature
    public string? CacheType { get; init; }
    public double? CacheCapacityKg { get; init; }
    public bool? CacheProtectsFromPredators { get; init; }
    public bool? CacheProtectsFromWeather { get; init; }
    public bool? CachePreservesFood { get; init; }
    public InventorySaveData? CacheStorage { get; init; }

    // CuringRackFeature
    public int? CuringRackCapacity { get; init; }
    public List<CuringItemSaveData>? CuringItems { get; init; }
}

/// <summary>
/// Item curing on a rack.
/// </summary>
public record CuringItemSaveData(string Type, double WeightKg, int MinutesCured, int MinutesRequired);

/// <summary>
/// Forage resource definition.
/// </summary>
public record ForageResourceSaveData(string Type, double Abundance, double MinWeight, double MaxWeight);

/// <summary>
/// Animal spawn entry.
/// </summary>
public record AnimalSpawnSaveData(string AnimalType, double SpawnWeight);

/// <summary>
/// Placed snare state.
/// </summary>
public record SnareSaveData
{
    public string State { get; init; } = "Empty";
    public int MinutesSet { get; init; }
    public string Bait { get; init; } = "None";
    public double BaitFreshness { get; init; }
    public string? CaughtAnimalType { get; init; }
    public double CaughtAnimalWeightKg { get; init; }
    public int MinutesSinceCatch { get; init; }
    public int DurabilityRemaining { get; init; }
    public bool IsReinforced { get; init; }
}

/// <summary>
/// Active tension state.
/// </summary>
public record TensionSaveData
{
    public string Type { get; init; } = "";
    public double Severity { get; init; }
    public double DecayPerHour { get; init; }
    public bool DecaysAtCamp { get; init; }
    public string? RelevantLocationName { get; init; }
    public string? SourceLocationName { get; init; }
    public string? AnimalType { get; init; }
    public string? Direction { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Expedition state - null if player is at camp.
/// </summary>
public record ExpeditionSaveData
{
    public List<string> TravelHistoryLocationNames { get; init; } = [];
    public string State { get; init; } = "";  // "Traveling" or "Working"
    public int MinutesElapsedTotal { get; init; }
    public List<string> CollectionLog { get; init; } = [];
}

/// <summary>
/// Pending predator encounter configuration.
/// </summary>
public record EncounterConfigSaveData
{
    public string AnimalType { get; init; } = "";
    public double InitialDistance { get; init; }
    public double InitialBoldness { get; init; }
    public List<string> Modifiers { get; init; } = [];
}
