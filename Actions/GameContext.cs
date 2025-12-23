using text_survival.Actors.Player;
using text_survival.Actions.Tensions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Actions.Expeditions;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

/// <summary>
/// Activity types for event frequency and survival context.
/// Each activity carries default config for event rate, activity level, and fire proximity.
/// </summary>
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

public class GameContext(Player player, Camp camp)
{
    public Player player = player;
    public Location CurrentLocation => Expedition?.CurrentLocation ?? Camp.Location;
    public Camp Camp = camp;
    public bool IsAtCamp => CurrentLocation == Camp.Location;

    // Web session support - null means console mode
    public string? SessionId { get; set; }

    // Instance log for web mode (console mode uses static log in GameDisplay)
    public NarrativeLog Log { get; } = new();

    // Player's carried inventory (aggregate-based)
    public Inventory Inventory { get; } = Inventory.CreatePlayerInventory(15.0);
    public Expedition? Expedition;
    public Zone Zone => CurrentLocation.ParentZone;

    // Tension system for tracking building threats/opportunities
    public TensionRegistry Tensions { get; } = new();

    // Activity-based config (eventMultiplier, activityLevel, fireProximity, statusText)
    private static readonly Dictionary<ActivityType, (double Event, double Activity, double Fire, string Status)> ActivityConfig = new()
    {
        // No events
        [ActivityType.Idle] = (0.0, 1.0, 0.0, "Thinking."),
        [ActivityType.Fighting] = (0.0, 2.0, 0.0, "Fighting."),
        [ActivityType.Encounter] = (0.0, 1.5, 0.0, "Alert."),

        // Camp activities (near fire, moderate events)
        [ActivityType.Sleeping] = (0.1, 0.5, 2.0, "Sleeping."),
        [ActivityType.Resting] = (0.3, 1.0, 2.0, "Resting."),
        [ActivityType.TendingFire] = (0.5, 1.0, 2.0, "Tending fire."),
        [ActivityType.Eating] = (0.5, 1.0, 2.0, "Eating."),
        [ActivityType.Cooking] = (0.5, 1.0, 2.0, "Cooking."),
        [ActivityType.Crafting] = (0.5, 1.0, 0.5, "Crafting."),

        // Expedition activities (away from fire, full events)
        [ActivityType.Traveling] = (1.0, 1.5, 0.0, "Traveling."),
        [ActivityType.Foraging] = (1.0, 1.5, 0.0, "Foraging."),
        [ActivityType.Hunting] = (1.0, 1.5, 0.0, "Hunting."),
        [ActivityType.Exploring] = (1.0, 1.5, 0.0, "Exploring."),
    };

    /// <summary>Current activity for event condition checks.</summary>
    public ActivityType CurrentActivity { get; private set; } = ActivityType.Idle;

    /// <summary>Encounter spawned by an event, to be handled by the caller.</summary>
    public EncounterConfig? PendingEncounter { get; set; }

    public bool Check(EventCondition condition)
    {
        return condition switch
        {
            EventCondition.IsDaytime => GetTimeOfDay() == TimeOfDay.Morning ||
                         GetTimeOfDay() == TimeOfDay.Afternoon ||
                         GetTimeOfDay() == TimeOfDay.Evening || GetTimeOfDay() == TimeOfDay.Noon,
            EventCondition.Traveling => CurrentActivity == ActivityType.Traveling,
            EventCondition.HasFood => Inventory.HasFood,
            EventCondition.HasMeat => Inventory.CookedMeat.Count > 0 || Inventory.RawMeat.Count > 0,
            EventCondition.Injured => player.Body.Parts.Any(p => p.Condition < 1.0),
            EventCondition.Slow => CapacityCalculator.GetCapacities(player.Body, player.GetEffectModifiers()).Moving < 0.7,
            EventCondition.FireBurning => Camp.HasActiveFire,
            EventCondition.Inside => CurrentLocation.HasFeature<ShelterFeature>() && !Check(EventCondition.FieldWork),
            EventCondition.Outside => !Check(EventCondition.Inside),
            EventCondition.InAnimalTerritory => CurrentLocation.HasFeature<AnimalTerritoryFeature>(),
            EventCondition.HasPredators => CurrentLocation.GetFeature<AnimalTerritoryFeature>()?.HasPredators() ?? false,

            // Weather conditions
            EventCondition.IsSnowing => Zone.Weather.CurrentCondition is ZoneWeather.WeatherCondition.LightSnow
                                     or ZoneWeather.WeatherCondition.Blizzard,
            EventCondition.IsBlizzard => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Blizzard,
            EventCondition.IsRaining => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Rainy || Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Stormy,
            EventCondition.IsStormy => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Stormy,
            EventCondition.HighWind => Zone.Weather.WindSpeed > 0.6,
            EventCondition.IsClear => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Clear,
            EventCondition.IsMisty => Zone.Weather.CurrentCondition == ZoneWeather.WeatherCondition.Misty,
            EventCondition.ExtremelyCold => Zone.Weather.BaseTemperature < -25,
            EventCondition.WeatherWorsening => Zone.Weather.WeatherJustChanged && IsWeatherWorsening(Zone.Weather),

            // Resource availability
            EventCondition.HasFuel => Inventory.HasFuel,
            EventCondition.HasTinder => Inventory.Tinder.Count > 0,

            EventCondition.HasFuelPlenty => Inventory.FuelWeightKg >= 3,
            EventCondition.HasFoodPlenty => Inventory.FoodWeightKg >= 1.0,

            EventCondition.LowOnFuel => Inventory.FuelWeightKg <= 1.0,
            EventCondition.LowOnFood => Inventory.FoodWeightKg <= 0.5,
            EventCondition.NoFuel => Inventory.FuelWeightKg <= 0.0,
            EventCondition.NoFood => Inventory.FoodWeightKg <= 0.0,

            // Tension conditions
            EventCondition.Stalked => Tensions.HasTension("Stalked"),
            EventCondition.StalkedHigh => Tensions.HasTensionAbove("Stalked", 0.5),
            EventCondition.StalkedCritical => Tensions.HasTensionAbove("Stalked", 0.7),
            EventCondition.SmokeSpotted => Tensions.HasTension("SmokeSpotted"),
            EventCondition.Infested => Tensions.HasTension("Infested"),
            EventCondition.WoundUntreated => Tensions.HasTension("WoundUntreated"),
            EventCondition.WoundUntreatedHigh => Tensions.HasTensionAbove("WoundUntreated", 0.6),
            EventCondition.ShelterWeakened => Tensions.HasTension("ShelterWeakened"),
            EventCondition.FoodScentStrong => Tensions.HasTension("FoodScentStrong"),
            EventCondition.Hunted => Tensions.HasTension("Hunted"),
            EventCondition.Disturbed => Tensions.HasTension("Disturbed"),
            EventCondition.DisturbedHigh => Tensions.HasTensionAbove("Disturbed", 0.5),
            EventCondition.DisturbedCritical => Tensions.HasTensionAbove("Disturbed", 0.7),

            // New tension arc conditions
            EventCondition.WoundedPrey => Tensions.HasTension("WoundedPrey"),
            EventCondition.WoundedPreyHigh => Tensions.HasTensionAbove("WoundedPrey", 0.5),
            EventCondition.WoundedPreyCritical => Tensions.HasTensionAbove("WoundedPrey", 0.7),

            EventCondition.PackNearby => Tensions.HasTension("PackNearby"),
            EventCondition.PackNearbyHigh => Tensions.HasTensionAbove("PackNearby", 0.4),
            EventCondition.PackNearbyCritical => Tensions.HasTensionAbove("PackNearby", 0.7),

            EventCondition.ClaimedTerritory => Tensions.HasTension("ClaimedTerritory"),
            EventCondition.ClaimedTerritoryHigh => Tensions.HasTensionAbove("ClaimedTerritory", 0.5),

            EventCondition.HerdNearby => Tensions.HasTension("HerdNearby"),
            EventCondition.HerdNearbyUrgent => Tensions.HasTensionAbove("HerdNearby", 0.6),

            EventCondition.DeadlyCold => Tensions.HasTension("DeadlyCold"),
            EventCondition.DeadlyColdCritical => Tensions.HasTensionAbove("DeadlyCold", 0.6),

            EventCondition.FeverRising => Tensions.HasTension("FeverRising"),
            EventCondition.FeverHigh => Tensions.HasTensionAbove("FeverRising", 0.4),
            EventCondition.FeverCritical => Tensions.HasTensionAbove("FeverRising", 0.7),

            // Camp/expedition state
            EventCondition.AtCamp => IsAtCamp,
            EventCondition.OnExpedition => Expedition != null,
            EventCondition.NearFire => CurrentLocation.GetFeature<HeatSourceFeature>()?.IsActive ?? false,
            EventCondition.HasShelter => CurrentLocation.HasFeature<ShelterFeature>(),
            EventCondition.NoShelter => !CurrentLocation.HasFeature<ShelterFeature>(),

            // Time of day
            EventCondition.Night => GetTimeOfDay() == TimeOfDay.Night,

            // Additional resource conditions
            EventCondition.HasWater => Inventory.HasWater,
            EventCondition.HasPlantFiber => Inventory.PlantFiber.Count > 0,

            // Body state conditions
            EventCondition.LowCalories => player.Body.CalorieStore < 500,
            EventCondition.LowHydration => player.Body.Hydration < 1000,
            EventCondition.LowTemperature => player.Body.BodyTemperature < 96.0,
            EventCondition.Impaired => AbilityCalculator.IsConsciousnessImpaired(
                player.GetCapacities().Consciousness),
            EventCondition.Limping => AbilityCalculator.IsMovingImpaired(
                player.GetCapacities().Moving),
            EventCondition.Clumsy => AbilityCalculator.IsManipulationImpaired(
                player.GetCapacities().Manipulation),
            EventCondition.Foggy => AbilityCalculator.IsPerceptionImpaired(
                AbilityCalculator.CalculatePerception(player.Body, player.EffectRegistry.GetCapacityModifiers())),
            EventCondition.Winded => AbilityCalculator.IsBreathingImpaired(
                player.GetCapacities().Breathing),

            // Activity conditions (for event filtering)
            EventCondition.IsSleeping => CurrentActivity == ActivityType.Sleeping,
            EventCondition.Awake => CurrentActivity != ActivityType.Sleeping,
            EventCondition.IsCampWork => CurrentActivity is ActivityType.TendingFire or ActivityType.Eating or ActivityType.Cooking or ActivityType.Crafting,
            EventCondition.IsExpedition =>  !IsAtCamp,
            EventCondition.Eating => CurrentActivity == ActivityType.Eating,
            EventCondition.FieldWork => (CurrentActivity is ActivityType.Traveling or ActivityType.Foraging or ActivityType.Hunting or ActivityType.Exploring),
            EventCondition.Working => Check(EventCondition.IsCampWork) || Check(EventCondition.FieldWork),
            _ => false,
        };
    }

    /// <summary>
    /// Determines if weather transitioned to a worse condition.
    /// </summary>
    private static bool IsWeatherWorsening(ZoneWeather w)
    {
        if (w.PreviousCondition == null) return false;

        return (w.PreviousCondition, w.CurrentCondition) switch
        {
            // Any non-clear is worse than clear
            (ZoneWeather.WeatherCondition.Clear, _) when w.CurrentCondition != ZoneWeather.WeatherCondition.Clear => true,
            // Precipitation is worse than cloudy
            (ZoneWeather.WeatherCondition.Cloudy, ZoneWeather.WeatherCondition.LightSnow or ZoneWeather.WeatherCondition.Rainy
                or ZoneWeather.WeatherCondition.Blizzard or ZoneWeather.WeatherCondition.Stormy) => true,
            // Blizzard is worse than light snow
            (ZoneWeather.WeatherCondition.LightSnow, ZoneWeather.WeatherCondition.Blizzard) => true,
            // Storm is worse than rain
            (ZoneWeather.WeatherCondition.Rainy, ZoneWeather.WeatherCondition.Stormy) => true,
            _ => false
        };
    }
    public DateTime GameTime { get; set; } = new DateTime(2025, 1, 1, 9, 0, 0); // Full date/time for resource respawn tracking
    public SurvivalContext GetSurvivalContext() => new SurvivalContext
    {
        ActivityLevel = 1.5,
        LocationTemperature = CurrentLocation.GetTemperature(),
        ClothingInsulation = Inventory.TotalInsulation,
    };

    /// <summary>
    /// Main update with activity type - uses ActivityConfig defaults.
    /// Checks for events each minute based on activity event multiplier.
    /// Returns elapsed time.
    /// </summary>
    public int Update(int targetMinutes, ActivityType activity, bool render = false)
    {
        CurrentActivity = activity;
        var config = ActivityConfig[activity];

        int elapsed = 0;
        GameEvent? evt = null;

        while (elapsed < targetMinutes && player.IsAlive)
        {
            elapsed++;

            // Update survival/zone/tensions (always runs)
            UpdateInternal(1, config.Activity, GetEffectiveFireProximity(config.Fire));

            // Check for event (only if activity allows events)
            if (config.Event > 0)
            {
                evt = GameEventRegistry.GetEventOnTick(this, config.Event);
                if (evt != null)
                    break; // Only break when an event actually triggers
            }

            // Optional render with status from config
            if (render && !string.IsNullOrEmpty(config.Status))
                GameDisplay.Render(this, statusText: config.Status);
        }

        if (!player.IsAlive)
            return elapsed;

        if (evt is not null)
        {
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
        var context = GetSurvivalContext();
        context.ActivityLevel = activityLevel;

        // Calculate fire proximity bonus if there's an active fire
        // Skip if hyperthermic - player would back away from fire
        var fire = CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire != null && fire.IsActive && !player.EffectRegistry.HasEffect("Hyperthermia"))
        {
            double fireHeat = fire.GetEffectiveHeatOutput(CurrentLocation.GetTemperature());
            context.FireProximityBonus = fireHeat * fireProximityMultiplier;
        }

        player.Update(minutes, context);
        CurrentLocation.ParentZone.Update(minutes, GameTime);
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
        var fire = CurrentLocation.GetFeature<HeatSourceFeature>();
        if (fire == null || !fire.IsActive) return 0;
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
}