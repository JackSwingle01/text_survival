using text_survival.Actors.Animals;
using text_survival.Actors.Player;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Tensions;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.Discovery;
using text_survival.Environments;
using text_survival.Environments.Grid;
using text_survival.Environments.Factories;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;
using text_survival.Actors;

namespace text_survival.Actions;

public class GameContext(Player player, Location camp, Weather weather)
{
    public Player player { get; set; } = player;

    [System.Text.Json.Serialization.JsonIgnore]
    public Location CurrentLocation => player.CurrentLocation;
    public Location Camp { get; set; } = camp;
    public bool IsAtCamp => CurrentLocation == Camp;



    // Web session identifier for this game instance
    public string? SessionId { get; set; }
    public NarrativeLog Log { get; set; } = new();

    // Player's carried inventory - references player.Inventory
    public Inventory Inventory => player.Inventory!;

    // Zone and location tracking
    public Weather Weather { get; init; } = weather;
    public GameMap? Map { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public (int X, int Y)? PendingTravelTarget { get; set; }
    public int DaysSurvived => (int)(GameTime - new DateTime(2025, 1, 1, 9, 0, 0)).TotalDays;

    // Tension system for tracking building threats/opportunities
    public TensionRegistry Tensions { get; set; } = new();

    // Herd registry for tracking persistent animals
    public HerdRegistry Herds { get; set; } = new();
    public List<NPC> NPCs { get; set; } = new();
    public List<NPC> GetNPCsAt(GridPosition pos)
    {
        if (Map == null) return [];
        return NPCs.Where(n => Map.GetPosition(n.CurrentLocation) == pos).ToList();
    }

    // Unified actor tracking - enables combat detection across all actor types
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<Actor> AllActors
    {
        get
        {
            yield return player;
            foreach (var npc in NPCs)
                yield return npc;
            foreach (var animal in Herds.GetAllAnimals())
                yield return animal;
        }
    }

    public GridPosition? GetActorPosition(Actor actor)
    {
        if (actor == player)
            return Map?.CurrentPosition;

        if (actor is NPC npc)
            return Map?.GetPosition(npc.CurrentLocation);

        if (actor is Animal animal)
            return Herds.GetHerdContaining(animal)?.Position;

        return null;
    }

    public List<Actor> GetActorsAt(GridPosition pos)
    {
        return AllActors.Where(a => GetActorPosition(a) == pos).ToList();
    }

    public ActivityType CurrentActivity { get; private set; } = ActivityType.Idle;

    [System.Text.Json.Serialization.JsonIgnore]
    public (double Energy, double Calories, double Hydration, double Temp)? StatsBeforeWork { get; set; }

    private EncounterConfig? _pendingEncounter;
    private Herd? _pendingEncounterHerd;

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

        var predator = EncounterRunner.CreateAnimalFromConfig(_pendingEncounter, CurrentLocation, Map);
        _pendingEncounter = null;

        if (predator != null)
        {
            // CombatOrchestrator handles allies automatically via SetupCombat
            var outcome = CombatOrchestrator.RunCombat(this, predator);

            LastEventAborted = true;  // Encounters abort the current action

            // Set fear on source herd based on encounter outcome
            if (_pendingEncounterHerd != null)
            {
                var herd = _pendingEncounterHerd;
                double fear = outcome switch
                {
                    CombatResult.Victory => 0.9,           // Player killed predator - high fear
                    CombatResult.AnimalFled => 0.7,        // Predator retreated - moderate fear
                    CombatResult.DistractedWithMeat => 0.5, // Got food, mild wariness
                    CombatResult.Fled => 0.2,              // Player fled - predator "won", low fear
                    CombatResult.AnimalDisengaged => 0.4,  // Mutual disengage - mild fear
                    _ => 0.5
                };
                herd.Fear = Math.Max(herd.Fear, fear);

                // Handle post-combat behavior based on outcome
                if (outcome == CombatResult.AnimalFled || outcome == CombatResult.AnimalDisengaged)
                {
                    // Trigger herd to flee the area
                    if (herd.Behavior != null && Map != null)
                    {
                        herd.Behavior.TriggerFlee(herd, Map.CurrentPosition, this);
                    }
                }
                else if (outcome == CombatResult.Fled)
                {
                    // Mark combat time for cooldown (player fled, predator stays)
                    herd.LastCombatMinutes = TotalMinutesElapsed;
                }
                _pendingEncounterHerd = null;
            }
        }
    }

    public bool IsHandlingEvent { get; set; } = false;
    public bool EventOccurredLastUpdate { get; private set; } = false;
    public bool LastEventAborted { get; private set; } = false;

    // Tutorial message tracking
    private HashSet<string> _shownTutorials = new();

    // Discovery Log - tracks player discoveries for progression display
    public DiscoveryLog Discoveries { get; set; } = new();

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

    // Discovery Log helper methods - record discovery and show notification if new
    public void RecordLocationDiscovery(string locationName)
    {
        if (Discoveries.DiscoverLocation(locationName))
            GameDisplay.AddDiscovery(this, $"New discovery: {locationName}");
    }

    public void RecordAnimalEncounter(AnimalType animalType)
    {
        if (Discoveries.EncounterAnimal(animalType))
            GameDisplay.AddDiscovery(this, $"New discovery: {animalType.DisplayName()}");
    }

    public void RecordFoodEaten(string foodName)
    {
        if (Discoveries.EatFood(foodName))
            GameDisplay.AddDiscovery(this, $"New discovery: {foodName}");
    }

    public void RecordMedicineUsed(string medicineName)
    {
        if (Discoveries.UseMedicine(medicineName))
            GameDisplay.AddDiscovery(this, $"New discovery: {medicineName}");
    }

    public void RecordItemCrafted(string itemName)
    {
        if (Discoveries.CraftItem(itemName))
            GameDisplay.AddDiscovery(this, $"New discovery: {itemName}");
    }

    // Parameterless constructor for JSON deserialization
    [System.Text.Json.Serialization.JsonConstructor]
    public GameContext() : this(null!, null!, null!) { }
    // public void RestoreAfterDeserialization()
    // {
    //     // Map handles its own restoration via LocationData property
    //     // Weather reference needs to be restored on the map
    //     if (Map != null)
    //     {
    //         Map.Weather = Weather;

    //         // Restore actor Map references (CurrentLocation restored via $ref)
    //         player.Map = Map;
    //         foreach (var npc in NPCs)
    //         {
    //             npc.Map = Map;
    //         }
    //     }

    //     // Recreate non-serialized animal members for herds
    //     Herds.RecreateAllMembers(Map);
    // }

    public static GameContext CreateNewGame()
    {
        // Clear event cooldowns for fresh game
        GameEventRegistry.ClearTriggerTimes();
        Weather weather = new Weather(-10);

        // Generate world map (uses defaults: 48x48 with 150 locations)
        var worldGen = new GridWorldGenerator();

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
        player.CurrentLocation = camp;
        player.Map = map;

        GameContext ctx = new GameContext(player, camp, weather);
        ctx.Map = map;

        // Populate world with persistent animal herds
        HerdPopulator.Populate(ctx.Herds, map);

        // Spawn test NPC near camp
        var testNPC = NPCFactory.SpawnNearCamp(map, camp);
        if (testNPC != null) ctx.NPCs.Add(testNPC);

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
        // 4 pieces of tinder for ~4 fire-starting attempts (was 2)
        ctx.Inventory.Add(Resource.Tinder, 0.05);
        ctx.Inventory.Add(Resource.Tinder, 0.04);
        ctx.Inventory.Add(Resource.Tinder, 0.05);
        ctx.Inventory.Add(Resource.Tinder, 0.04);

        return ctx;
    }

    public bool Check(EventCondition condition) => ConditionChecker.Check(this, condition);

    public void EstablishCamp(Location location)
    {
        // Only show message if actually changing camp location
        bool isNewCamp = Camp != location;

        // Update camp pointer
        Camp = location;

        if (isNewCamp)
            GameDisplay.AddSuccess(this, $"You've established camp at {location.Name}.");
    }

    public DateTime GameTime { get; set; } = new DateTime(2025, 1, 1, 9, 0, 0); // Full date/time for resource respawn tracking

    /// <summary>Total minutes elapsed since game start (for cooldown comparisons).</summary>
    public int TotalMinutesElapsed => (int)(GameTime - new DateTime(2025, 1, 1, 9, 0, 0)).TotalMinutes;

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
            UpdateInternal(1);

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

    private void UpdateInternal(int minutes)
    {
        // Tick torch burn time and handle chaining logic
        Handlers.TorchHandler.UpdateTorchBurnTime(this, minutes, CurrentLocation.GetFeature<HeatSourceFeature>());

        // Tick ember carrier burn time and check for wetness extinguishing
        UpdateEmberCarriers(minutes);

        // Tick down waterproofing treatment during precipitation exposure
        UpdateWaterproofing(minutes);

        var context = SurvivalContext.GetSurvivalContext(player, Inventory, CurrentActivity, GetTimeOfDay());
        player.Update(minutes, context);

        // Update zone weather and all named locations (terrain-only don't need updates)
        Weather.Update(GameTime);

        // Show weather change popup
        if (Weather.WeatherJustChanged && SessionId != null)
        {
            Web.WebIO.ShowWeatherChange(this);
        }

        if (Map != null)
        {
            foreach (var location in Map.NamedLocations)
            {
                location.Update(minutes);
            }
        }

        Tensions.Update(minutes, IsAtCamp);

        // Update herds - they move and can detect player

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
                var encounterHerd = result.EncounterRequest.Herd;
                var predator = encounterHerd.GetRandomMember();
                if (predator != null)
                {
                    _pendingEncounterHerd = encounterHerd;  // Track source herd for fear setting
                    encounterHerd.LastCombatMinutes = TotalMinutesElapsed;  // Mark combat start for cooldown

                    _pendingEncounter = new EncounterConfig(
                        encounterHerd.AnimalType,
                        InitialDistance: result.EncounterRequest.IsDefendingKill ? 10 : 20,
                        InitialBoldness: result.EncounterRequest.IsDefendingKill ? 0.8 : 0.6
                    );
                }
            }
        }

        // Update NPCs
        for (int i = 0; i < minutes; i++)
        {
            foreach (NPC npc in NPCs.ToList())
            {
                var npcContext = SurvivalContext.GetSurvivalContext(npc, npc.Inventory, npc.CurrentAction?.ActivityType ?? ActivityType.Idle, GetTimeOfDay());
                npc.Update(1, npcContext);
            }
        }

        // Handle NPC deaths
        var deadNPCs = NPCs.Where(npc => !npc.IsAlive).ToList();
        foreach (var npc in deadNPCs)
        {
            var cause = NPCBodyFeature.DetermineDeathCause(npc);
            var body = new NPCBodyFeature(npc.Name, cause, GameTime, npc.Inventory);
            npc.CurrentLocation.AddFeature(body);

            // If player is present, witness the death immediately
            if (npc.CurrentLocation == CurrentLocation)
            {
                body.IsDiscovered = true;
                string text = $"{body.NPCName} collapses. {body.DeathCause}";
                Web.WebIO.ShowDiscovery(this, body.NPCName, text);
            }
            // Otherwise, they'll discover it when they return to this tile
        }
        NPCs.RemoveAll(npc => !npc.IsAlive);

        // Check for undiscovered bodies at current location
        var undiscoveredBody = CurrentLocation.GetFeature<NPCBodyFeature>();
        if (undiscoveredBody != null && !undiscoveredBody.IsDiscovered)
        {
            undiscoveredBody.IsDiscovered = true;
            string text = undiscoveredBody.DeathDiscoveryText();
            Web.WebIO.ShowDiscovery(this, undiscoveredBody.NPCName, text);
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
