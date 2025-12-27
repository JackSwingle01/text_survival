using text_survival.Actors.Player;
using text_survival.Actions.Tensions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Environments.Factories;
using text_survival.Actions.Expeditions;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
namespace text_survival.Actions;

public class GameContext(Player player, Location camp, Weather weather)
{
    public Player player { get; set; } = player;
    public Location CurrentLocation { get; set; } = camp;
    public Location Camp { get; set; } = camp;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAtCamp => CurrentLocation == Camp;

    // Web session support - null means console mode
    public string? SessionId { get; set; }

    // Instance log for web mode (console mode uses static log in GameDisplay)
    public NarrativeLog Log { get; set; } = new();

    // Player's carried inventory (aggregate-based)
    public Inventory Inventory { get; set; } = Inventory.CreatePlayerInventory(15.0);

    // Zone and location tracking (moved from Zone to break circular reference)
    public Weather Weather { get; init; } = weather;
    public List<Location> Locations { get; set; } = new();
    [System.Text.Json.Serialization.JsonInclude]
    private List<Location> _unrevealedLocations { get; set; } = new();
    public IReadOnlyList<Location> UnrevealedLocations => _unrevealedLocations.AsReadOnly();

    // Grid-based world (new tile system)
    /// <summary>
    /// The tile grid. Null if using legacy graph-based locations.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TileGrid? Grid { get; set; }

    /// <summary>
    /// Current tile position (grid mode). Null if using legacy mode.
    /// </summary>
    public Tile? CurrentTile { get; set; }

    /// <summary>
    /// Pending travel target from map click (grid mode).
    /// When set, GridTravelRunner will immediately move to this tile.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public (int X, int Y)? PendingTravelTarget { get; set; }

    /// <summary>
    /// Whether the game is running in grid mode (vs legacy graph mode).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsGridMode => Grid != null && CurrentTile != null;

    /// <summary>
    /// Check if at camp (works in both grid and legacy modes).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAtCampTile => IsGridMode
        ? CurrentTile?.NamedLocation == Camp
        : CurrentLocation == Camp;

    // Mountain pass tracking (not in standard location pool)
    public List<Location> MountainPassLocations { get; private set; } = [];
    public Location? WinLocation { get; private set; }

    public bool HasWon { get; private set; }
    public void TriggerVictory() => HasWon = true;

    public bool IsWinLocation(Location location) => location == WinLocation;

    public int DaysSurvived => (int)(GameTime - new DateTime(2025, 1, 1, 9, 0, 0)).TotalDays;

    /// <summary>
    /// Call this after zone generation to set up the pass.
    /// </summary>
    public void SetupMountainPass(List<Location> passLocations, Location winLocation)
    {
        MountainPassLocations = passLocations;
        WinLocation = winLocation;
    }

    // Tension system for tracking building threats/opportunities
    public TensionRegistry Tensions { get; set; } = new();

    /// <summary>Current activity for event condition checks.</summary>
    public ActivityType CurrentActivity { get; private set; } = ActivityType.Idle;

    /// <summary>Encounter queued by an event, handled internally by Update().</summary>
    private EncounterConfig? _pendingEncounter;

    /// <summary>
    /// Queue an encounter to be spawned on the next Update tick.
    /// Used by event outcomes that spawn predator encounters.
    /// </summary>
    public void QueueEncounter(EncounterConfig config)
    {
        _pendingEncounter = config;
    }

    /// <summary>Flag to prevent events from triggering during event handling.</summary>
    public bool IsHandlingEvent { get; set; } = false;

    /// <summary>Set to true when an event was triggered during the last Update call.</summary>
    public bool EventOccurredLastUpdate { get; private set; } = false;

    // Tutorial message tracking
    private HashSet<string> _shownTutorials = new();

    // For JSON serialization
    public HashSet<string> ShownTutorials
    {
        get => _shownTutorials;
        set => _shownTutorials = value;
    }

    /// <summary>
    /// Shows a tutorial message only once per game. Uses the message itself as the key.
    /// </summary>
    public void ShowTutorialOnce(string message)
    {
        if (_shownTutorials.Contains(message))
            return;

        _shownTutorials.Add(message);
        GameDisplay.AddNarrative(this, message);
    }

    /// <summary>
    /// For complex tutorials with multiple messages or custom display.
    /// Returns true if this is the first time (and marks as shown), false if already shown.
    /// </summary>
    public bool TryShowTutorial(string key)
    {
        if (_shownTutorials.Contains(key))
            return false;

        _shownTutorials.Add(key);
        return true;
    }

    // Parameterless constructor for JSON deserialization
    [System.Text.Json.Serialization.JsonConstructor]
    public GameContext() : this(null!, null!, null!)
    {
    }

    /// <summary>
    /// Call this after deserialization to restore transient state.
    /// CurrentLocation is not serialized, so it must be restored to Camp.
    /// </summary>
    public void RestoreAfterDeserialization()
    {
        if (CurrentLocation == null)
        {
            CurrentLocation = Camp;
        }
    }

    public static GameContext CreateNewGame()
    {
        // Clear event cooldowns for fresh game
        GameEventRegistry.ClearTriggerTimes();
        Weather weather = new Weather(-10);
        var (locations, unrevealed, passLocations) = ZoneFactory.MakeForestZone(weather);

        // Initialize weather for game start time (9:00 AM, Jan 1)
        var gameStartTime = new DateTime(2025, 1, 1, 9, 0, 0);
        weather.Update(gameStartTime);

        Location startingArea = locations.First(s => s.Name == "Forest Camp");

        // Add campfire (unlit - player must start it)
        HeatSourceFeature campfire = new HeatSourceFeature();
        campfire.AddFuel(2, FuelType.Kindling);
        startingArea.Features.Add(campfire);

        // Add camp storage cache
        startingArea.Features.Add(CacheFeature.CreateCampCache());

        var player = new Player();

        GameContext ctx = new GameContext(player, startingArea, weather);
        ctx.Locations.AddRange(locations);
        ctx._unrevealedLocations.AddRange(unrevealed);

        // Setup mountain pass - last location in chain is the win location
        ctx.SetupMountainPass(passLocations, passLocations[^1]);

        // Equip starting clothing
        ctx.Inventory.Equip(Gear.WornFurChestWrap());
        ctx.Inventory.Equip(Gear.FurLegWraps());
        ctx.Inventory.Equip(Gear.WornHideBoots());
        ctx.Inventory.Equip(Gear.HideHandwraps());

        // Add starting supplies
        ctx.Inventory.Tools.Add(Gear.HandDrill());
        ctx.Inventory.Add(Resource.Stick, 0.3);
        ctx.Inventory.Add(Resource.Stick, 0.25);
        ctx.Inventory.Add(Resource.Stick, 0.35);
        ctx.Inventory.Add(Resource.Tinder, 0.05);
        ctx.Inventory.Add(Resource.Tinder, 0.04);

        return ctx;
    }

    /// <summary>
    /// Create a new game using the grid-based tile world.
    /// </summary>
    public static GameContext CreateNewGridGame()
    {
        // Clear event cooldowns for fresh game
        GameEventRegistry.ClearTriggerTimes();
        Weather weather = new Weather(-10);

        // Generate grid-based world
        var worldGen = new GridWorldGenerator
        {
            Width = 32,
            Height = 32,
            TargetNamedLocations = 40,
            MinLocationSpacing = 3
        };

        var (grid, campTile, camp) = worldGen.Generate(weather);

        // Initialize weather for game start time (9:00 AM, Jan 1)
        var gameStartTime = new DateTime(2025, 1, 1, 9, 0, 0);
        weather.Update(gameStartTime);

        // Add campfire (unlit - player must start it)
        HeatSourceFeature campfire = new HeatSourceFeature();
        campfire.AddFuel(2, FuelType.Kindling);
        camp.Features.Add(campfire);

        // Add camp storage cache
        camp.Features.Add(CacheFeature.CreateCampCache());

        var player = new Player();

        GameContext ctx = new GameContext(player, camp, weather);

        // Set up grid mode
        ctx.Grid = grid;
        ctx.CurrentTile = campTile;
        ctx.CurrentLocation = camp;

        // Add all named locations to the Locations list for compatibility
        foreach (var tile in grid.NamedLocationTiles)
        {
            ctx.Locations.Add(tile.NamedLocation!);
        }

        // Equip starting clothing
        ctx.Inventory.Equip(Gear.WornFurChestWrap());
        ctx.Inventory.Equip(Gear.FurLegWraps());
        ctx.Inventory.Equip(Gear.WornHideBoots());
        ctx.Inventory.Equip(Gear.HideHandwraps());

        // Add starting supplies
        ctx.Inventory.Tools.Add(Gear.HandDrill());
        ctx.Inventory.Add(Resource.Stick, 0.3);
        ctx.Inventory.Add(Resource.Stick, 0.25);
        ctx.Inventory.Add(Resource.Stick, 0.35);
        ctx.Inventory.Add(Resource.Tinder, 0.05);
        ctx.Inventory.Add(Resource.Tinder, 0.04);

        return ctx;
    }

    /// <summary>
    /// Check event condition. Delegates to ConditionChecker.
    /// </summary>
    public bool Check(EventCondition condition) => ConditionChecker.Check(this, condition);

    /// <summary>
    /// Check if a location meets the requirements to establish camp.
    /// Requires bedding and an active heat source.
    /// </summary>
    public bool CanEstablishCampAt(Location location)
    {
        bool hasBedding = location.HasFeature<BeddingFeature>();
        bool hasActiveFire = location.HasActiveHeatSource();
        return hasBedding && hasActiveFire;
    }

    /// <summary>
    /// Establish camp at the specified location.
    /// Creates storage cache if one doesn't exist.
    /// Updates Camp pointer and ends active expedition.
    /// </summary>
    public void EstablishCamp(Location location)
    {
        // Only show message if actually changing camp location
        bool isNewCamp = Camp != location;

        // Update camp pointer
        Camp = location;

        if (isNewCamp)
            GameDisplay.AddSuccess(this, $"You've established camp at {location.Name}.");
    }

    // === LOCATION MANAGEMENT ===

    /// <summary>
    /// Check if there are any unrevealed locations remaining
    /// </summary>
    public bool HasUnrevealedLocations()
    {
        return _unrevealedLocations.Count > 0;
    }

    /// <summary>
    /// Reveal a random location from the pool and connect it to the specified location
    /// </summary>
    public Location? RevealRandomLocation(Location connectFrom)
    {
        if (_unrevealedLocations.Count == 0)
            return null;

        // Pick a random unrevealed location
        int index = Random.Shared.Next(_unrevealedLocations.Count);
        var newLocation = _unrevealedLocations[index];
        _unrevealedLocations.RemoveAt(index);

        // Connect it to the graph
        connectFrom.AddBidirectionalConnection(newLocation);
        Locations.Add(newLocation);
        newLocation.Explore();

        return newLocation;
    }

    /// <summary>
    /// Get count of unrevealed locations (for UI hints)
    /// </summary>
    public int UnrevealedCount => _unrevealedLocations.Count;

    public DateTime GameTime { get; set; } = new DateTime(2025, 1, 1, 9, 0, 0); // Full date/time for resource respawn tracking

    /// <summary>
    /// Get survival context for a given activity.
    /// </summary>
    /// <param name="isStationary">If true, structural shelter applies (resting, crafting).
    /// If false, only environmental shelter applies (foraging, hunting, traveling).</param>
    public SurvivalContext GetSurvivalContext(bool isStationary = true)
    {
        double clothingInsulation = Inventory.TotalInsulation;

        // Get current wetness
        var wetEffect = player.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault();
        double currentWetness = wetEffect?.Severity ?? 0;

        // Wetness reduces insulation effectiveness
        if (wetEffect != null)
        {
            // At full wetness (severity 1.0), clothing loses 70% effectiveness
            double insulationLossFactor = wetEffect.Severity * 0.70;
            clothingInsulation *= (1 - insulationLossFactor);
        }

        // Calculate overhead cover (environmental + shelter if stationary)
        double overheadCover = CurrentLocation.OverheadCoverLevel;
        if (isStationary)
        {
            var shelter = CurrentLocation.GetFeature<ShelterFeature>();
            if (shelter != null)
                overheadCover = Math.Max(overheadCover, shelter.OverheadCoverage);
        }

        // Extract weather conditions
        bool isRaining = Weather.CurrentCondition == Weather.WeatherCondition.Rainy ||
                         Weather.CurrentCondition == Weather.WeatherCondition.Stormy;
        bool isBlizzard = Weather.CurrentCondition == Weather.WeatherCondition.Blizzard;
        bool isSnowing = Weather.CurrentCondition == Weather.WeatherCondition.LightSnow;

        return new SurvivalContext
        {
            ActivityLevel = 1.5,
            LocationTemperature = CurrentLocation.GetTemperature(isStationary),
            ClothingInsulation = clothingInsulation,

            // Wetness context
            OverheadCover = overheadCover,
            Precipitation = Weather.Precipitation,
            WindSpeed = Weather.WindSpeed,
            IsRaining = isRaining,
            IsSnowing = isSnowing,
            IsBlizzard = isBlizzard,
            CurrentWetnessSeverity = currentWetness,
        };
    }

    /// <summary>
    /// Determine if current activity is stationary (benefits from structural shelter).
    /// </summary>
    private static bool IsActivityStationary(ActivityType activity) => activity switch
    {
        // Stationary activities - shelter applies
        ActivityType.Idle => true,
        ActivityType.Fighting => true,
        ActivityType.Encounter => true,
        ActivityType.Sleeping => true,
        ActivityType.Resting => true,
        ActivityType.TendingFire => true,
        ActivityType.Eating => true,
        ActivityType.Cooking => true,
        ActivityType.Crafting => true,

        // Moving activities - no structural shelter
        ActivityType.Traveling => false,
        ActivityType.Foraging => false,
        ActivityType.Hunting => false,
        ActivityType.Exploring => false,
        ActivityType.Chopping => false,

        _ => true // Default to stationary
    };

    /// <summary>
    /// Main update with activity type - uses ActivityConfig defaults.
    /// Checks for events each minute based on activity event multiplier.
    /// Returns elapsed time.
    /// </summary>
    public int Update(int targetMinutes, ActivityType activity, bool render = false)
    {
        EventOccurredLastUpdate = false;
        CurrentActivity = activity;
        var config = ActivityConfig.Get(activity);

        int elapsed = 0;
        GameEvent? evt = null;

        while (elapsed < targetMinutes && player.IsAlive)
        {
            elapsed++;

            // Update survival/zone/tensions (always runs)
            UpdateInternal(1, config.ActivityLevel, GetEffectiveFireProximity(config.FireProximity));

            // Check for event (only if activity allows events AND not already handling an event)
            if (config.EventMultiplier > 0 && !IsHandlingEvent)
            {
                evt = GameEventRegistry.GetEventOnTick(this, config.EventMultiplier);
                if (evt != null)
                    break; // Only break when an event actually triggers
            }

            // Optional render with status from config
            if (render && !string.IsNullOrEmpty(config.StatusText))
                GameDisplay.Render(this, statusText: config.StatusText);
        }

        if (!player.IsAlive)
            return elapsed;

        if (evt is not null)
        {
            EventOccurredLastUpdate = true;
            GameEventRegistry.HandleEvent(this, evt);
        }

        // Spawn predator encounter if event outcome requested it
        if (_pendingEncounter != null)
        {
            var predator = EncounterRunner.CreateAnimalFromConfig(_pendingEncounter);
            _pendingEncounter = null;
            if (predator != null)
            {
                EncounterRunner.HandlePredatorEncounter(predator, this);
            }
        }

        // Tutorial: afternoon fuel warning on Day 1
        if (DaysSurvived == 0 && GetTimeOfDay() == TimeOfDay.Afternoon)
        {
            var fire = CurrentLocation.GetFeature<HeatSourceFeature>();
            double fuelKg = fire != null ? fire.TotalMassKg : 0;

            if (fuelKg < 6.0 && TryShowTutorial("afternoon_fuel_warning"))
            {
                GameDisplay.AddWarning(this, "The sun is getting low. Your fire won't last the night.");
                GameDisplay.AddWarning(this, "Gather fuel while there's still light.");
            }
        }

        return elapsed;
    }

    /// <summary>
    /// Internal update - survival stats, zone, tensions without event checking.
    /// </summary>
    private void UpdateInternal(int minutes, double activityLevel, double fireProximityMultiplier)
    {
        bool isStationary = IsActivityStationary(CurrentActivity);
        var context = GetSurvivalContext(isStationary);
        context.ActivityLevel = activityLevel;

        // Calculate fire proximity bonus if there's an active fire
        // Skip if hyperthermic - player would back away from fire
        var fire = CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire != null && fire.IsActive && !player.EffectRegistry.HasEffect("Hyperthermia"))
        {
            double fireHeat = fire.GetEffectiveHeatOutput(CurrentLocation.GetTemperature(isStationary));
            context.FireProximityBonus = fireHeat * fireProximityMultiplier;
        }

        // Torch provides warmth during expeditions (when away from fire)
        if (Inventory.HasLitTorch)
        {
            context.FireProximityBonus += Inventory.GetTorchHeatBonusF();
        }

        // Tick torch burn time and handle chaining logic
        Handlers.TorchHandler.UpdateTorchBurnTime(this, minutes, fire);

        player.Update(minutes, context);

        // Update zone weather and all locations
        Weather.Update(GameTime);
        foreach (var location in Locations)
        {
            location.Update(minutes);
        }

        Tensions.Update(minutes, IsAtCamp);

        // DeadlyCold auto-resolves when player reaches fire
        if (Tensions.HasTension("DeadlyCold") && Check(EventCondition.NearFire))
        {
            Tensions.ResolveTension("DeadlyCold");
            GameDisplay.AddNarrative(this, "The fire's warmth washes over you. You're going to be okay.");
        }

        GameTime = GameTime.AddMinutes(minutes);

        var logs = player?.GetFlushLogs();
        if (logs is not null && logs.Count != 0)
            GameDisplay.AddNarrative(this, logs);
    }

    /// <summary>
    /// Fire proximity is 0 if no active fire, otherwise config value.
    /// </summary>
    private double GetEffectiveFireProximity(double configValue)
    {
        if (!CurrentLocation.HasActiveHeatSource()) return 0;
        return configValue;
    }

    public enum TimeOfDay
    {
        Night,
        Dawn,
        Morning,
        Noon,
        Afternoon,
        Evening,
        Dusk
    }

    public TimeOfDay GetTimeOfDay()
    {
        return GameTime.Hour switch
        {
            < 5 => TimeOfDay.Night,
            < 6 => TimeOfDay.Dawn,
            < 11 => TimeOfDay.Morning,
            < 13 => TimeOfDay.Noon,
            < 17 => TimeOfDay.Afternoon,
            < 20 => TimeOfDay.Evening,
            < 21 => TimeOfDay.Dusk,
            _ => TimeOfDay.Night
        };
    }
}

public enum ActivityType
{
    // No events
    Idle,           // Menu, thinking - no events
    Fighting,       // Combat - no events
    Encounter,      // Predator standoff - no events

    // Camp activities (near fire, moderate events)
    Sleeping,       // Rare events, low activity
    Resting,        // Occasional events, waiting by fire
    TendingFire,    // Moderate events
    Eating,         // Moderate events
    Cooking,        // Moderate events
    Crafting,       // Moderate events

    // Expedition activities (away from fire, full events)
    Traveling,      // Full events, moving between locations
    Foraging,       // Full events, searching for resources
    Hunting,        // Full events, tracking game
    Exploring,      // Full events, scouting new areas (away from fire)
    Chopping,       // Full events, felling trees (strenuous)
}
public enum EventCondition
{
    IsDaytime,
    Traveling,
    Working,
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

    // Cold Snap arc
    DeadlyCold,            // Any DeadlyCold tension exists
    DeadlyColdCritical,    // DeadlyCold severity > 0.6

    // Fever arc
    FeverRising,           // Any FeverRising tension exists
    FeverHigh,             // FeverRising severity > 0.4
    FeverCritical,         // FeverRising severity > 0.7

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
    HazardousTerrain,   // Location terrain hazard >= 0.5
    HasFuelForage,      // Location has ForageFeature with fuel resources (deadfall, etc.)
    HighOverheadCover,  // Location or shelter has overhead coverage >= 0.6 (traps smoke)
    AtDisturbedSource,  // At the location where Disturbed tension originated

    // Water/Ice conditions
    FrozenWater,        // Water feature is frozen
    OnThinIce,          // Frozen water with ice thickness < 0.4 (dangerous)
    HasIceHole,         // An ice hole has been cut in frozen water
}