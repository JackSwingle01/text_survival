using text_survival.Actors.Player;
using text_survival.Actions.Tensions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Actions.Expeditions;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using Microsoft.AspNetCore.Mvc.Diagnostics;
namespace text_survival.Actions;

public class GameContext(Player player, Location camp, Weather weather)
{
    public Player player { get; set; } = player;
    [System.Text.Json.Serialization.JsonIgnore]
    public Location CurrentLocation => Expedition?.CurrentLocation ?? Camp;
    public Location Camp { get; set; } = camp;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAtCamp => CurrentLocation == Camp;

    // Web session support - null means console mode
    public string? SessionId { get; set; }

    // Instance log for web mode (console mode uses static log in GameDisplay)
    public NarrativeLog Log { get; set; } = new();

    // Player's carried inventory (aggregate-based)
    public Inventory Inventory { get; set; } = Inventory.CreatePlayerInventory(15.0);
    public Expedition? Expedition;

    // Zone and location tracking (moved from Zone to break circular reference)
    public Weather Weather { get; init; } = weather;
    public List<Location> Locations { get; set; } = new();
    private List<Location> _unrevealedLocations { get; set; } = new();
    public IReadOnlyList<Location> UnrevealedLocations => _unrevealedLocations.AsReadOnly();

    // Tension system for tracking building threats/opportunities
    public TensionRegistry Tensions { get; set; } = new();

    /// <summary>Current activity for event condition checks.</summary>
    public ActivityType CurrentActivity { get; private set; } = ActivityType.Idle;

    /// <summary>Encounter spawned by an event, to be handled by the caller.</summary>
    public EncounterConfig? PendingEncounter { get; set; }

    /// <summary>Flag to prevent events from triggering during event handling.</summary>
    public bool IsHandlingEvent { get; set; } = false;

    /// <summary>Set to true when an event was triggered during the last Update call.</summary>
    public bool EventOccurredLastUpdate { get; private set; } = false;

    // Parameterless constructor for JSON deserialization
    [System.Text.Json.Serialization.JsonConstructor]
    public GameContext() : this(null!, null!, null!)
    {
    }

    public static GameContext CreateNewGame()
    {
        // Clear event cooldowns for fresh game
        GameEventRegistry.ClearTriggerTimes();
        Weather weather = new Weather(-10);
        var (locations, unrevealed) = ZoneFactory.MakeForestZone(weather);

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

        // Equip starting clothing
        ctx.Inventory.Equip(Equipment.WornFurChestWrap());
        ctx.Inventory.Equip(Equipment.FurLegWraps());
        ctx.Inventory.Equip(Equipment.FurBoots());

        // Add starting supplies
        ctx.Inventory.Tools.Add(Tool.HandDrill());
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
    public SurvivalContext GetSurvivalContext(bool isStationary = true) => new SurvivalContext
    {
        ActivityLevel = 1.5,
        LocationTemperature = CurrentLocation.GetTemperature(isStationary),
        ClothingInsulation = Inventory.TotalInsulation,
    };

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
        if (PendingEncounter != null)
        {
            var predator = EncounterRunner.CreateAnimalFromConfig(PendingEncounter);
            PendingEncounter = null;
            if (predator != null)
            {
                EncounterRunner.HandlePredatorEncounter(predator, this);
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
    HighOverheadCover,  // Location or shelter has overhead coverage >= 0.6 (traps smoke)
    AtDisturbedSource,  // At the location where Disturbed tension originated

    // Water/Ice conditions
    FrozenWater,        // Water feature is frozen
    OnThinIce,          // Frozen water with ice thickness < 0.4 (dangerous)
    HasIceHole,         // An ice hole has been cut in frozen water
}