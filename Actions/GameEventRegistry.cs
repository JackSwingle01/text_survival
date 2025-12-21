using text_survival.Actions.Tensions;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

/// <summary>
/// Event system registry and execution.
/// Event factory methods are defined in partial class files under Actions/Events/
/// </summary>
public static partial class GameEventRegistry
{
    public record TickResult(int MinutesElapsed, GameEvent? TriggeredEvent);

    // Single knob to control overall event frequency
    private const double EventsPerHour = 1.0;
    private static readonly double BaseChancePerMinute = RateToChancePerMinute(EventsPerHour);

    private static double RateToChancePerMinute(double eventsPerHour)
    {
        double ratePerMinute = eventsPerHour / 60.0;
        return 1 - Math.Exp(-ratePerMinute);
    }

    /// <summary>
    /// Event factories that create fresh events with context baked in.
    /// </summary>
    public static List<Func<GameContext, GameEvent>> AllEventFactories { get; } =
    [
        // Weather events (GameEventRegistry.Weather.cs)
        StormApproaching,
        Whiteout,
        FrostbiteWarning,
        ColdRainSoaking,
        LostInFog,
        BitterWind,
        SuddenClearing,

        // Expedition events (GameEventRegistry.Expedition.cs)
        TreacherousFooting,
        SomethingCatchesYourEye,
        MinorAccident,
        GlintInAshes,
        OldCampsite,
        WaterSource,
        UnexpectedYield,
        ExposedPosition,
        NaturalShelterSpotted,
        DriftwoodDebris,

        // Camp infrastructure events (GameEventRegistry.Camp.cs)
        VerminRaid,
        ShelterGroans,
        ChokingSmoke,
        EmbersScatter,
        RustleAtCampEdge,
        MeltingReveal,

        // Threat events (GameEventRegistry.Threats.cs)
        // Wildlife
        FreshCarcass,
        Tracks,
        SomethingWatching,
        RavenCall,
        DistantCarcassStench,
        // Stalker Arc
        StalkerCircling,
        PredatorRevealed,
        Ambush,
        // Body Events
        TheShakes,
        GutWrench,
        MuscleCramp,
        VisionBlur,
        FrozenFingers,
        OldAche,
        Toothbreaker,
        // Psychological
        ParanoiaEvent,
        MomentOfClarity,
        FugueState,
        // Wound/Infection Arc
        WoundFesters,
        FeverSetsIn,

        // Consciousness events (GameEventRegistry.Consciousness.cs)
        LostYourBearings
    ];

    /// <summary>
    /// Runs minute-by-minute ticks, checking for events each minute.
    /// Returns when targetMinutes is reached OR an event triggers.
    /// Caller is responsible for calling ctx.Update() with the elapsed time.
    /// </summary>
    public static TickResult RunTicks(GameContext ctx, int targetMinutes)
    {
        int elapsed = 0;
        GameEvent? evt = null;

        while (elapsed < targetMinutes)
        {
            elapsed++;
            evt = GetEventOnTick(ctx);
            if (evt is not null)
                break;
        }

        return new TickResult(elapsed, evt);
    }

    public static GameEvent? GetEventOnTick(GameContext ctx, double activityMultiplier = 1.0)
    {
        // Stage 1: Base roll - does ANY event trigger?
        // Activity multiplier is now passed in from the activity config
        double chance = BaseChancePerMinute * activityMultiplier;
        if (!Utils.DetermineSuccess(chance))
            return null;

        GameDisplay.AddNarrative($"Debug: chance {chance:F3}/min, {(chance * 60):F3}/hr");
        // Stage 2: Build eligible pool with weights
        var eligible = new Dictionary<GameEvent, double>();

        foreach (var factory in AllEventFactories)
        {
            var evt = factory(ctx);

            // Filter: skip if required conditions not met
            if (!evt.RequiredConditions.All(ctx.Check))
                continue;

            // Calculate weight with modifiers
            double weight = evt.BaseWeight;
            foreach (var (condition, modifier) in evt.WeightModifiers)
            {
                if (ctx.Check(condition))
                    weight *= modifier;
            }

            eligible[evt] = weight;
        }

        // If no eligible events, no event triggers
        if (eligible.Count == 0)
            return null;

        // Stage 3: Weighted random selection
        return Utils.GetRandomWeighted(eligible);
    }

    /// <summary>
    /// Handle a triggered event - display, get player choice, apply outcome.
    /// Sets ctx.PendingEncounter if the outcome spawns a predator encounter.
    /// </summary>
    public static void HandleEvent(GameContext ctx, GameEvent evt)
    {
        List<ActivityType> excluded = [ActivityType.Sleeping, ActivityType.Fighting, ActivityType.Encounter];
        if (excluded.Contains(ctx.CurrentActivity))
            return;

        if (ctx.CurrentActivity == ActivityType.Sleeping)
        {
            GameDisplay.AddNarrative("You wake suddenly!");
        }
        
        GameDisplay.Render(ctx, statusText: "Event!");
        GameDisplay.AddNarrative($"{evt.Name}", LogLevel.Warning);
        GameDisplay.AddNarrative(evt.Description);
        GameDisplay.Render(ctx, statusText: "Thinking.");

        var choice = evt.GetChoice(ctx);
        GameDisplay.AddNarrative(choice.Description + "\n");

        var outcome = choice.DetermineResult();

        HandleOutcome(ctx, outcome);
        GameDisplay.Render(ctx);
        Input.WaitForKey();

        // Store encounter for caller to handle
        if (outcome.SpawnEncounter != null)
            ctx.PendingEncounter = outcome.SpawnEncounter;
    }

    /// <summary>
    /// Apply an event outcome - time, effects, rewards.
    /// </summary>
    public static void HandleOutcome(GameContext ctx, EventResult outcome)
    {
        if (outcome.TimeAddedMinutes != 0)
        {
            GameDisplay.AddNarrative($"(+{outcome.TimeAddedMinutes} minutes)");
            GameDisplay.UpdateAndRenderProgress(ctx, "Acting", outcome.TimeAddedMinutes, updateTime: false);
        }

        GameDisplay.AddNarrative(outcome.Message);

        foreach (var effect in outcome.Effects)
        {
            ctx.player.EffectRegistry.AddEffect(effect);
        }

        if (outcome.NewDamage is not null)
        {
            var dmgResult = ctx.player.Body.Damage(outcome.NewDamage);
            foreach (var effect in dmgResult.TriggeredEffects)
            {
                ctx.player.EffectRegistry.AddEffect(effect);
            }
        }

        if (outcome.RewardPool != RewardPool.None)
        {
            var resources = RewardGenerator.Generate(outcome.RewardPool);
            if (!resources.IsEmpty)
            {
                ctx.Inventory.Add(resources);
                foreach (var desc in resources.Descriptions)
                {
                    GameDisplay.AddNarrative($"You found {desc}");
                }
            }
        }

        if (outcome.Cost is not null)
        {
            DeductResources(ctx.Inventory, outcome.Cost);
        }

        // Tension processing
        if (outcome.CreatesTension is not null)
        {
            var tc = outcome.CreatesTension;
            var tension = tc.Type switch
            {
                "Stalked" => ActiveTension.Stalked(tc.Severity, tc.AnimalType, tc.RelevantLocation),
                "SmokeSpotted" => ActiveTension.SmokeSpotted(tc.Severity, tc.Direction, tc.RelevantLocation),
                "Infested" => ActiveTension.Infested(tc.Severity, tc.RelevantLocation),
                "WoundUntreated" => ActiveTension.WoundUntreated(tc.Severity, tc.Description),
                "ShelterWeakened" => ActiveTension.ShelterWeakened(tc.Severity, tc.RelevantLocation),
                "FoodScentStrong" => ActiveTension.FoodScentStrong(tc.Severity),
                "Hunted" => ActiveTension.Hunted(tc.Severity, tc.AnimalType),
                "MarkedDiscovery" => ActiveTension.MarkedDiscovery(tc.Severity, tc.RelevantLocation, tc.Description),
                _ => ActiveTension.Custom(tc.Type, tc.Severity, 0.05, true, tc.RelevantLocation, tc.AnimalType, tc.Direction, tc.Description)
            };
            ctx.Tensions.AddTension(tension);
        }

        if (outcome.ResolvesTension is not null)
        {
            ctx.Tensions.ResolveTension(outcome.ResolvesTension);
        }

        if (outcome.EscalateTension is not null)
        {
            var (type, amount) = outcome.EscalateTension.Value;
            ctx.Tensions.EscalateTension(type, amount);
        }

        // Tool damage - reduce durability
        if (outcome.DamageTool is not null)
        {
            var tool = ctx.Inventory.GetTool(outcome.DamageTool.Type);
            if (tool != null && tool.Durability > 0)
            {
                tool.Durability = Math.Max(0, tool.Durability - outcome.DamageTool.UsesLost);
                if (tool.IsBroken)
                    GameDisplay.AddNarrative($"Your {tool.Name} breaks!");
                else
                    GameDisplay.AddNarrative($"Your {tool.Name} is damaged.");
            }
        }

        // Tool break - completely destroy the tool
        if (outcome.BreakTool is not null)
        {
            var tool = ctx.Inventory.GetTool(outcome.BreakTool.Value);
            if (tool != null)
            {
                tool.Durability = 0;
                GameDisplay.AddNarrative($"Your {tool.Name} breaks!");
            }
        }

        // Clothing damage - reduce insulation
        if (outcome.DamageClothing is not null)
        {
            var equipment = ctx.Inventory.GetEquipment(outcome.DamageClothing.Slot);
            if (equipment != null)
            {
                equipment.Insulation = Math.Max(0, equipment.Insulation - outcome.DamageClothing.InsulationLoss);
                GameDisplay.AddNarrative($"Your {equipment.Name} is damaged.");
            }
        }

        // Feature modifications
        if (outcome.AddFeature is not null)
        {
            var feature = CreateFeatureFromConfig(outcome.AddFeature);
            if (feature != null)
            {
                ctx.CurrentLocation.AddFeature(feature);
                GameDisplay.AddNarrative($"Added {feature.Name} to this location.");
            }
        }

        if (outcome.ModifyFeature is not null)
        {
            if (outcome.ModifyFeature.FeatureType == typeof(ForageFeature) && outcome.ModifyFeature.DepleteAmount is not null)
            {
                var feature = ctx.CurrentLocation.GetFeature<ForageFeature>();
                if (feature != null)
                {
                    feature.Deplete(outcome.ModifyFeature.DepleteAmount.Value);
                }
            }
            else if (outcome.ModifyFeature.FeatureType == typeof(ShelterFeature) && outcome.ModifyFeature.DepleteAmount is not null)
            {
                var feature = ctx.CurrentLocation.GetFeature<ShelterFeature>();
                if (feature != null)
                {
                    feature.Damage(outcome.ModifyFeature.DepleteAmount.Value);
                }
            }
        }

        if (outcome.RemoveFeature is not null)
        {
            if (outcome.RemoveFeature == typeof(ShelterFeature))
            {
                var feature = ctx.CurrentLocation.GetFeature<ShelterFeature>();
                if (feature != null)
                {
                    ctx.CurrentLocation.RemoveFeature(feature);
                    GameDisplay.AddNarrative($"{feature.Name} has been destroyed.");
                }
            }
            else if (outcome.RemoveFeature == typeof(HeatSourceFeature))
            {
                var feature = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
                if (feature != null)
                {
                    ctx.CurrentLocation.RemoveFeature(feature);
                    GameDisplay.AddNarrative($"{feature.Name} has been destroyed.");
                }
            }
        }

        // SpawnEncounter will be processed by the caller (ExpeditionRunner)
        // since it requires transitioning to the encounter system
    }

    /// <summary>
    /// Create a feature instance from FeatureCreation config.
    /// </summary>
    private static LocationFeature? CreateFeatureFromConfig(FeatureCreation config)
    {
        if (config.FeatureType == typeof(ShelterFeature))
        {
            // Default shelter stats if no config provided
            if (config.Config is (double temp, double overhead, double wind))
                return new ShelterFeature("Improvised Shelter", temp, overhead, wind);
            else
                return new ShelterFeature("Improvised Shelter", 0.3, 0.4, 0.5);
        }

        // Add other feature types as needed
        return null;
    }

    /// <summary>
    /// Deduct resources from inventory based on cost type.
    /// </summary>
    private static void DeductResources(Items.Inventory inv, ResourceCost cost)
    {
        for (int i = 0; i < cost.Amount; i++)
        {
            switch (cost.Type)
            {
                case ResourceType.Fuel:
                    // Prefer sticks over logs (less wasteful)
                    if (inv.Sticks.Count > 0)
                        inv.TakeSmallestStick();
                    else if (inv.Logs.Count > 0)
                        inv.TakeSmallestLog();
                    break;

                case ResourceType.Tinder:
                    inv.TakeTinder();
                    break;

                case ResourceType.Food:
                    // Prefer berries, then cooked, then raw
                    if (inv.Berries.Count > 0)
                        inv.Berries.RemoveAt(0);
                    else if (inv.CookedMeat.Count > 0)
                        inv.CookedMeat.RemoveAt(0);
                    else if (inv.RawMeat.Count > 0)
                        inv.RawMeat.RemoveAt(0);
                    break;

                case ResourceType.Water:
                    // Deduct 0.25L per unit
                    inv.WaterLiters = Math.Max(0, inv.WaterLiters - 0.25);
                    break;

                case ResourceType.PlantFiber:
                    inv.TakePlantFiber();
                    break;
            }
        }
    }
}
