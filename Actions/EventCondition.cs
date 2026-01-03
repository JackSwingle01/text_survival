namespace text_survival.Actions;

public enum EventCondition
{
    IsDaytime,
    Traveling,
    Working,
    Foraging,
    HasFood,
    HasMeat,
    Injured,
    Slow,
    FireBurning,
    Outside,
    Inside,
    InAnimalTerritory,
    HasPredators,

    // Weather conditions
    IsSnowing,
    IsBlizzard,
    IsRaining,
    IsStormy,
    HighWind,
    IsClear,
    IsMisty,
    ExtremelyCold,

    // Weather transitions
    WeatherWorsening,
    CalmBeforeTheStorm,     // Prolonged blizzard warning phase

    // Resource availability - basic
    HasFuel,
    HasTinder,

    // Resource availability - thresholds
    HasFuelPlenty,      // 3+ pieces
    HasFoodPlenty,      // 3+ servings

    // Resource scarcity (for weight modifiers - events more likely when desperate)
    LowOnFuel,          // 1 or less
    LowOnFood,
    NoFuel,
    NoFood,

    // Tension conditions
    Stalked,            // Being stalked by a predator
    StalkedHigh,        // Stalked with severity > 0.5
    StalkedCritical,    // Stalked with severity > 0.7
    SmokeSpotted,       // Someone spotted the player's smoke
    Infested,           // Vermin have infested the camp
    WoundUntreated,     // An untreated wound risks infection
    WoundUntreatedHigh, // Untreated wound with severity > 0.6 (infection spreading)
    ShelterWeakened,    // Shelter has been damaged
    FoodScentStrong,    // Strong food scent attracting predators
    Hunted,             // Actively being hunted by a predator
    Disturbed,          // Player witnessed disturbing content (death, remains)
    DisturbedHigh,      // Disturbed with severity > 0.5
    DisturbedCritical,  // Disturbed with severity > 0.7

    // WoundedPrey arc
    WoundedPrey,           // Any WoundedPrey tension exists
    WoundedPreyHigh,       // WoundedPrey severity > 0.5
    WoundedPreyCritical,   // WoundedPrey severity > 0.7

    // Pack arc
    PackNearby,            // Any PackNearby tension exists
    PackNearbyHigh,        // PackNearby severity > 0.4
    PackNearbyCritical,    // PackNearby severity > 0.7

    // Den arc
    ClaimedTerritory,      // Any ClaimedTerritory tension exists
    ClaimedTerritoryHigh,  // ClaimedTerritory severity > 0.5

    // Herd arc
    HerdNearby,            // Any HerdNearby tension exists
    HerdNearbyUrgent,      // HerdNearby severity > 0.6 (window closing)
    HerdOnTile,            // A prey herd is on the player's current tile

    // Cold Snap arc
    DeadlyCold,            // Any DeadlyCold tension exists
    DeadlyColdCritical,    // DeadlyCold severity > 0.6

    // Fever arc
    FeverRising,           // Any FeverRising tension exists
    FeverHigh,             // FeverRising severity > 0.4
    FeverCritical,         // FeverRising severity > 0.7

    // Mammoth hunt arc
    MammothTracked,        // Any MammothTracked tension exists
    MammothTrackedHigh,    // MammothTracked severity > 0.6 (ready for confrontation)

    // Camp/expedition state
    AtCamp,             // Player is at camp (not on expedition)
    OnExpedition,       // Player is on an expedition
    NearFire,           // Current location has active fire
    HasShelter,         // Current location has shelter feature
    NoShelter,          // Current location has NO shelter feature

    // Time of day
    Night,              // Nighttime (before dawn or after dusk)

    // Additional resource conditions
    HasWater,           // Has any water
    HasPlantFiber,      // Has plant fiber for crafting/traps
    HasMedicine,        // Has any medicine for treatment
    HasSticks,          // Has sticks for building/fire
    HasCookedMeat,      // Has cooked meat for bait/food

    // Body state conditions
    LowCalories,        // Player calories below threshold
    LowHydration,       // Player hydration below threshold
    LowTemperature,     // Player core temperature is low
    Impaired,           // Player consciousness is impaired (< 0.5)
    Limping,            // Player moving capacity is impaired (< 0.5)
    Clumsy,             // Player manipulation capacity is impaired (< 0.5)
    Foggy,              // Player perception is impaired (< 0.5)
    Winded,             // Player breathing is impaired (< 0.75)

    // Activity conditions (for event filtering based on what player is doing)
    IsSleeping,         // Player is sleeping
    Awake,              // Player is NOT sleeping (inverse of IsSleeping)
    IsCampWork,         // Player is doing camp work (tending fire, eating, cooking, crafting)
    IsExpedition,       // Player is on expedition (traveling, foraging, hunting, exploring)
    Eating,
    FieldWork,

    // Trapping conditions
    HasActiveSnares,    // Any location has active snares set by player
    SnareHasCatch,      // Any snare has a catch ready
    SnareBaited,        // Any snare is baited
    TrapLineActive,     // TrapLineActive tension exists

    // Location properties
    HighVisibility,     // Location visibility > 0.7 (exposed, can see far)
    LowVisibility,      // Location visibility < 0.3 (hidden, restricted sightlines)
    InDarkness,         // Location is dark AND no active light source
    HasLightSource,     // Active fire/torch at current location
    NearWater,          // Location has a water feature
    NotNearWater,       // No water feature at current or adjacent locations
    HazardousTerrain,   // Location terrain hazard >= 0.5
    HasFuelForage,      // Location has ForageFeature with fuel resources (deadfall, etc.)
    HighOverheadCover,  // Location or shelter has overhead coverage >= 0.6 (traps smoke)
    AtDisturbedSource,  // At the location where Disturbed tension originated
    AtClaimedTerritory, // At the location where ClaimedTerritory tension originated

    // Water/Ice conditions
    FrozenWater,        // Water feature is frozen
    OnThinIce,          // Frozen water with ice thickness < 0.4 (dangerous)
    HasIceHole,         // An ice hole has been cut in frozen water

    // Spatial/grid conditions
    FarFromCamp,           // Manhattan distance > 8 tiles from camp
    VeryFarFromCamp,       // Manhattan distance > 15 tiles from camp
    NearMountains,         // Adjacent to Mountain terrain
    SurroundedByWater,     // 2+ adjacent Water tiles
    IsForest,              // Current tile is Forest terrain
    DeepInForest,          // Current + 3+ adjacent tiles are Forest
    OnBoundary,            // At terrain boundary (multiple distinct terrain types adjacent)
    Cornered,              // Only 1-2 passable adjacent tiles (limited exits)
    AtTerrainBottleneck,   // Narrow passage far from camp (cornered + far)
    JustRevealedLocation,  // Visibility just revealed new named location

    // Carcass conditions
    HasCarcass,            // Current location has a carcass feature
    HasFreshCarcass,       // Carcass with ScentIntensity > 0.5
    HasStrongScent,        // Carcass with ScentIntensity > 0.6

    // Player scent conditions
    PlayerBloody,          // Player has the Bloody effect
    PlayerBloodyHigh,      // Player has Bloody effect with severity > 0.2

    // Equipment state conditions
    Waterproofed,          // Player has some waterproof equipment (level >= 0.15)
    FullyWaterproofed,     // Player has well-waterproofed equipment (level >= 0.4)

    // Equipment possession
    HasWeapon,             // Player has a weapon equipped
    HasFirestarter,        // Player has a fire-starting tool

    // Saber-tooth arc
    SaberToothStalked,     // Being stalked by a saber-tooth

    // Terrain escape options
    HasEscapeTerrain,      // Location has terrain suitable for escape (trees, rocks)

    // Butchering activity
    IsButchering,          // Player is actively butchering a carcass

    // Visibility (aliases)
    GoodVisibility,        // Alias for HighVisibility - open terrain with clear sightlines
}