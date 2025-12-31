using text_survival.Actors.Animals;
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

    [System.Text.Json.Serialization.JsonIgnore]
    public Location CurrentLocation => Map?.CurrentLocation ?? Camp;

    public Location Camp { get; set; } = camp;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAtCamp => CurrentLocation == Camp;

    // Web session identifier for this game instance
    public string? SessionId { get; set; }

    // Instance narrative log for this session
    public NarrativeLog Log { get; set; } = new();

    // Player's carried inventory (aggregate-based)
    public Inventory Inventory { get; set; } = Inventory.CreatePlayerInventory(15.0);

    // Zone and location tracking
    public Weather Weather { get; init; } = weather;

    public GameMap? Map { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public (int X, int Y)? PendingTravelTarget { get; set; }

    // Mountain pass tracking (not in standard location pool)
    public List<Location> MountainPassLocations { get; private set; } = [];
    public Location? WinLocation { get; private set; }

    public bool HasWon { get; private set; }
    public void TriggerVictory() => HasWon = true;

    public bool IsWinLocation(Location location) => location == WinLocation;

    public int DaysSurvived => (int)(GameTime - new DateTime(2025, 1, 1, 9, 0, 0)).TotalDays;

    public void SetupMountainPass(List<Location> passLocations, Location winLocation)
    {
        MountainPassLocations = passLocations;
        WinLocation = winLocation;
    }

    // Tension system for tracking building threats/opportunities
    public TensionRegistry Tensions { get; set; } = new();

    // Herd registry for tracking persistent animals
    public HerdRegistry Herds { get; set; } = new();

    public ActivityType CurrentActivity { get; private set; } = ActivityType.Idle;

    [System.Text.Json.Serialization.JsonIgnore]
    public (double Energy, double Calories, double Hydration, double Temp)? StatsBeforeWork { get; set; }

    private EncounterConfig? _pendingEncounter;
    private Guid? _pendingEncounterHerdId;

    public void QueueEncounter(EncounterConfig config)
    {
        _pendingEncounter = config;
    }

    public bool HasPendingEncounter => _pendingEncounter != null;

    public void HandlePendingEncounter()
    {
        if (_pendingEncounter == null)
            return;

        var activityConfig = ActivityConfig.Get(CurrentActivity);
        if (activityConfig.EventMultiplier == 0)
            return; // Activities that block events also block encounters

        var predator = CombatRunner.CreateAnimalFromConfig(_pendingEncounter);
        _pendingEncounter = null;

        if (predator != null)
        {
            // Use unified combat system - starts at encounter distance
            var outcome = CombatRunner.RunCombat(this, predator);
            LastEventAborted = true;  // Encounters abort the current action

            // Set fear on source herd based on encounter outcome
            if (_pendingEncounterHerdId.HasValue)
            {
                var herd = Herds.GetHerdById(_pendingEncounterHerdId.Value);
                if (herd != null)
                {
                    double fear = outcome switch
                    {
                        CombatResult.Victory => 0.9,           // Player killed predator - high fear
                        CombatResult.AnimalFled => 0.7,        // Predator retreated - moderate fear
                        CombatResult.DistractedWithMeat => 0.5, // Got food, mild wariness
                        CombatResult.Fled => 0.2,              // Player fled - predator "won", low fear
                        CombatResult.AnimalDisengaged => 0.4,  // Mutual disengage - mild fear
                        _ => 0.5
                    };
                    herd.PlayerFear = Math.Max(herd.PlayerFear, fear);
                }
                _pendingEncounterHerdId = null;
            }
        }
    }

    public bool IsHandlingEvent { get; set; } = false;
    public bool EventOccurredLastUpdate { get; private set; } = false;
    public bool LastEventAborted { get; private set; } = false;

    // Tutorial message tracking
    private HashSet<string> _shownTutorials = new();

    // For JSON serialization
    public HashSet<string> ShownTutorials
    {
        get => _shownTutorials;
        set => _shownTutorials = value;
    }

    public void ShowTutorialOnce(string message)
    {
        if (_shownTutorials.Contains(message))
            return;

        _shownTutorials.Add(message);
        GameDisplay.AddNarrative(this, message);
    }

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

    public void RestoreAfterDeserialization()
    {
        // Map handles its own restoration via LocationData property
        // Weather reference needs to be restored on the map
        if (Map != null)
        {
            Map.Weather = Weather;
        }

        // Recreate non-serialized animal members for herds
        Herds.RecreateAllMembers();
    }

    public static GameContext CreateNewGame()
    {
        // Clear event cooldowns for fresh game
        GameEventRegistry.ClearTriggerTimes();
        Weather weather = new Weather(-10);

        // Generate world map
        var worldGen = new GridWorldGenerator
        {
            Width = 32,
            Height = 32,
            TargetNamedLocations = 40,
            MinLocationSpacing = 3
        };

        var (map, camp) = worldGen.Generate(weather);

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
        ctx.Map = map;

        // Populate world with persistent animal herds
        HerdPopulator.Populate(ctx.Herds, map);

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

    public bool Check(EventCondition condition) => ConditionChecker.Check(this, condition);

    public bool CanEstablishCampAt(Location location)
    {
        bool hasBedding = location.HasFeature<BeddingFeature>();
        bool hasActiveFire = location.HasActiveHeatSource();
        return hasBedding && hasActiveFire;
    }

    public void EstablishCamp(Location location)
    {
        // Only show message if actually changing camp location
        bool isNewCamp = Camp != location;

        // Update camp pointer
        Camp = location;

        if (isNewCamp)
            GameDisplay.AddSuccess(this, $"You've established camp at {location.Name}.");
    }

    public bool HasUnrevealedLocations() => Map?.HasUnexploredVisibleLocations ?? false;
    public Location? RevealRandomLocation(Location fromLocation) => Map?.RevealRandomLocation();
    public int UnrevealedCount => Map?.UnexploredVisibleCount ?? 0;

    public DateTime GameTime { get; set; } = new DateTime(2025, 1, 1, 9, 0, 0); // Full date/time for resource respawn tracking

    public SurvivalContext GetSurvivalContext(bool isStationary = true)
    {
        double clothingInsulation = Inventory.TotalInsulation;

        // Get current wetness
        var wetEffect = player.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault();
        double currentWetness = wetEffect?.Severity ?? 0;

        // Get bleeding and bloody severities for bloody accumulation
        double currentBleeding = player.EffectRegistry.GetSeverity("Bleeding");
        double currentBloody = player.EffectRegistry.GetSeverity("Bloody");

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

        // Calculate waterproofing level from resin-treated equipment
        double waterproofingLevel = CalculateWaterproofingLevel();

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
            WaterproofingLevel = waterproofingLevel,

            // Bloody accumulation context
            CurrentBleedingSeverity = currentBleeding,
            CurrentBloodySeverity = currentBloody,

            // Clothing thermal mass
            ClothingWeightKg = Inventory.TotalEquipmentWeightKg,
            ClothingHeatBuffer = player.Body.ClothingHeatBuffer,
        };
    }

    public double CalculateWaterproofingLevel()
    {
        // Slot coverage weights (how much of body each slot covers for wetness)
        var slotWeights = new Dictionary<EquipSlot, double>
        {
            { EquipSlot.Head, 0.1 },
            { EquipSlot.Chest, 0.4 },
            { EquipSlot.Legs, 0.3 },
            { EquipSlot.Hands, 0.1 },
            { EquipSlot.Feet, 0.1 }
        };

        double totalWaterproofing = 0;
        foreach (var slot in slotWeights.Keys)
        {
            var equipment = Inventory.GetEquipment(slot);
            if (equipment != null)
            {
                // Use gear's total waterproof level (base material + treatment bonus)
                totalWaterproofing += slotWeights[slot] * equipment.TotalWaterproofLevel;
            }
        }

        return Math.Min(totalWaterproofing, 1.0);
    }

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

    public int Update(int targetMinutes, ActivityType activity, bool render = false)
    {
        EventOccurredLastUpdate = false;
        LastEventAborted = false;
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
            var result = GameEventRegistry.HandleEvent(this, evt);
            LastEventAborted = result.AbortsAction;
        }

        // Clear visibility reveal flag after event check (transient state, resets each update)
        if (Map != null)
            Map.RevealedNewLocations = false;

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

        // Ember carriers provide smaller warmth bonus (2-3°F vs torch's 3-5°F)
        context.FireProximityBonus += Inventory.GetEmberCarrierHeatBonusF();

        // Tick torch burn time and handle chaining logic
        Handlers.TorchHandler.UpdateTorchBurnTime(this, minutes, fire);

        // Tick ember carrier burn time and check for wetness extinguishing
        UpdateEmberCarriers(minutes);

        // Tick down waterproofing treatment during precipitation exposure
        UpdateWaterproofing(minutes);

        player.Update(minutes, context);

        // Update zone weather and all named locations (terrain-only don't need updates)
        Weather.Update(GameTime);
        if (Map != null)
        {
            foreach (var location in Map.NamedLocations)
            {
                location.Update(minutes);
            }
        }

        Tensions.Update(minutes, IsAtCamp);

        // Update herds - they move and can detect player
        if (Map != null)
        {
            var herdResults = Herds.Update(minutes, this);

            // Process herd update results
            foreach (var result in herdResults)
            {
                // Show narrative messages
                if (result.NarrativeMessage != null)
                {
                    GameDisplay.AddNarrative(this, result.NarrativeMessage);
                }

                // Queue predator encounters
                if (result.EncounterRequest != null && _pendingEncounter == null)
                {
                    var encounterHerd = Herds.GetHerdById(result.EncounterRequest.HerdId);
                    if (encounterHerd != null)
                    {
                        var predator = encounterHerd.GetRandomMember();
                        if (predator != null)
                        {
                            _pendingEncounterHerdId = encounterHerd.Id;  // Track source herd for fear setting

                            _pendingEncounter = new EncounterConfig(
                                encounterHerd.AnimalType.DisplayName(),
                                InitialDistance: result.EncounterRequest.IsDefendingKill ? 10 : 20,
                                InitialBoldness: result.EncounterRequest.IsDefendingKill ? 0.8 : 0.6
                            );
                        }
                    }
                }
            }
        }

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

    private double GetEffectiveFireProximity(double configValue)
    {
        if (!CurrentLocation.HasActiveHeatSource()) return 0;
        return configValue;
    }

    private void UpdateEmberCarriers(int minutes)
    {
        var litCarriers = Inventory.Tools
            .Where(t => t.IsEmberCarrier && t.IsEmberLit)
            .ToList();

        if (litCarriers.Count == 0) return;

        // Check player wetness - embers extinguish if too wet
        double wetness = player.EffectRegistry.GetEffectsByKind("Wet").FirstOrDefault()?.Severity ?? 0;

        foreach (var carrier in litCarriers)
        {
            if (wetness > 0.5)
            {
                // Wetness extinguishes the ember
                carrier.EmberBurnHoursRemaining = 0;
                GameDisplay.AddWarning(this, $"Your {carrier.Name} hisses and goes out. Too wet to keep smoldering.");
            }
            else
            {
                // Tick down burn time (minutes to hours conversion)
                carrier.EmberBurnHoursRemaining -= minutes / 60.0;
                if (carrier.EmberBurnHoursRemaining <= 0)
                {
                    carrier.EmberBurnHoursRemaining = 0;
                    GameDisplay.AddWarning(this, $"Your {carrier.Name} has burned out.");
                }
            }
        }
    }

    private void UpdateWaterproofing(int minutes)
    {
        // Only tick during precipitation
        bool isPrecipitating = Weather.CurrentCondition is
            Weather.WeatherCondition.Rainy or
            Weather.WeatherCondition.Stormy or
            Weather.WeatherCondition.LightSnow or
            Weather.WeatherCondition.Blizzard;

        if (!isPrecipitating) return;

        // Check overhead cover - no degradation if fully covered
        double exposure = 1 - CurrentLocation.OverheadCoverLevel;
        if (exposure <= 0) return;

        foreach (var slot in Inventory.Equipment.Keys)
        {
            var gear = Inventory.GetEquipment(slot);
            if (gear == null || !gear.IsResinTreated) continue;

            int oldDurability = gear.ResinTreatmentDurability;
            for (int i = 0; i < minutes; i++)
                gear.TickResinTreatment();

            // Warnings at thresholds
            if (oldDurability > 12 && gear.ResinTreatmentDurability <= 12)
                GameDisplay.AddWarning(this, $"Your {gear.Name} resin treatment is wearing thin.");
            if (oldDurability > 0 && gear.ResinTreatmentDurability == 0)
                GameDisplay.AddWarning(this, $"The resin treatment on your {gear.Name} has worn off.");
        }
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
    Tracking,       // Full events, following animal signs while foraging
    Butchering,     // Full events, processing a carcass
}
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