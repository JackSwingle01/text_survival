using text_survival.Actions.Tensions;
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
    private const double EventsPerHour = .5;
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
        Debris,

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
        // Blood Trail Arc
        BloodInSnow,
        TheDyingAnimal,
        ScavengersConverge,

        // Herd events (GameEventRegistry.Herd.cs)
        DistantThunder,
        EdgeOfHerd,
        Stampede,
        TheFollowers,

        // Cold Snap Arc (GameEventRegistry.ColdSnap.cs)
        TheWindShifts,
        GoingNumb,
        FrostbiteSettingIn,
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
        // Disturbed Arc
        Nightmare,
        NightTerrors,
        ProcessingTrauma,
        IntrusiveThought,
        LostTime,
        FacingTheSource,
        ShadowMovement,

        // Consciousness events (GameEventRegistry.Consciousness.cs)
        LostYourBearings,

        // Moving impairment events (GameEventRegistry.Moving.cs)
        StrugglingToKeepPace,

        // Manipulation impairment events (GameEventRegistry.Manipulation.cs)
        FumblingHands,

        // Perception impairment events (GameEventRegistry.Perception.cs)
        DulledSenses,

        // Den arc events (GameEventRegistry.Den.cs)
        TheFind,
        AssessingTheClaim,
        TheConfrontation,
        // ClaimingTheDen is chained from successful eviction outcomes, not random

        // Pack arc events (GameEventRegistry.Pack.cs)
        PackSigns,
        EyesInTreeline,
        Circling,
        ThePackCommits,

        // Fever arc events (GameEventRegistry.Fever.cs)
        SomethingWrong,
        FeverTakesHold,
        TheFireIllusion,
        FootstepsOutside,
        FeverCrisisPoint,

        // Trapping events (GameEventRegistry.Trapping.cs)
        SnareTampered,
        PredatorAtTrapLine,
        GoodCatch,
        TrapLinePlundered,
        TrappingAccident,
        BaitedTrapAttention
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

        // GameDisplay.AddNarrative(ctx, $"Debug: chance {chance:F3}/min, {(chance * 60):F3}/hr");
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
            foreach (var (condition, modifier) in evt.WeightFactors)
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

        // Prevent nested events from triggering during this event's outcome processing
        ctx.IsHandlingEvent = true;
        try
        {
            if (ctx.CurrentActivity == ActivityType.Sleeping)
            {
                GameDisplay.AddNarrative(ctx, "You wake suddenly!");
            }

            GameDisplay.Render(ctx, statusText: "Event!");
            GameDisplay.AddNarrative(ctx, $"{evt.Name}", LogLevel.Warning);
            GameDisplay.AddNarrative(ctx, evt.Description);
            GameDisplay.Render(ctx, statusText: "Thinking.");

            var choice = evt.GetChoice(ctx);
            GameDisplay.AddNarrative(ctx, choice.Description + "\n");

            var outcome = choice.DetermineResult();

            HandleOutcome(ctx, outcome);
            GameDisplay.Render(ctx);
            Input.WaitForKey(ctx);

            // Store encounter for caller to handle
            if (outcome.SpawnEncounter != null)
                ctx.PendingEncounter = outcome.SpawnEncounter;

            // Chain to follow-up event if specified
            if (outcome.ChainEvent != null)
            {
                var chainedEvent = outcome.ChainEvent(ctx);
                HandleEvent(ctx, chainedEvent);
            }
        }
        finally
        {
            ctx.IsHandlingEvent = false;
        }
    }

    /// <summary>Helper class to track outcome gains and losses for summary display.</summary>
    private class OutcomeSummary
    {
        public List<string> Gains { get; } = [];
        public List<string> Losses { get; } = [];
        public bool HasContent => Gains.Count > 0 || Losses.Count > 0;
    }

    /// <summary>
    /// Apply an event outcome - time, effects, rewards.
    /// </summary>
    public static void HandleOutcome(GameContext ctx, EventResult outcome)
    {
        var summary = new OutcomeSummary();

        if (outcome.TimeAddedMinutes != 0)
        {
            GameDisplay.AddNarrative(ctx, $"(+{outcome.TimeAddedMinutes} minutes)");
            GameDisplay.UpdateAndRenderProgress(ctx, "Acting", outcome.TimeAddedMinutes, ctx.CurrentActivity);
        }

        GameDisplay.AddNarrative(ctx, outcome.Message);

        foreach (var effect in outcome.Effects)
        {
            ctx.player.EffectRegistry.AddEffect(effect);
            GameDisplay.AddDanger(ctx, $"  - {effect.EffectKind}");
            summary.Losses.Add(effect.EffectKind);
        }

        if (outcome.NewDamage is not null)
        {
            var dmgResult = ctx.player.Body.Damage(outcome.NewDamage);
            GameDisplay.AddDanger(ctx, $"  - Injured ({outcome.NewDamage.Source})");
            summary.Losses.Add($"Injury: {outcome.NewDamage.Source}");
            foreach (var effect in dmgResult.TriggeredEffects)
            {
                ctx.player.EffectRegistry.AddEffect(effect);
                GameDisplay.AddDanger(ctx, $"  - {effect.EffectKind}");
                summary.Losses.Add(effect.EffectKind);
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
                    GameDisplay.AddSuccess(ctx, $"  + {desc}");
                    summary.Gains.Add(desc);
                }
            }
        }

        if (outcome.Cost is not null)
        {
            DeductResources(ctx.Inventory, outcome.Cost);
        }

        // Direct stat drains (vomiting, etc)
        if (outcome.StatDrain is not null)
        {
            var (calories, hydration) = outcome.StatDrain.Value;
            if (calories > 0)
            {
                ctx.player.Body.DrainCalories(calories);
                GameDisplay.AddDanger(ctx, $"  - Lost {calories:F0} calories");
                summary.Losses.Add($"{calories:F0} calories");
            }
            if (hydration > 0)
            {
                ctx.player.Body.DrainHydration(hydration);
                GameDisplay.AddDanger(ctx, $"  - Lost {hydration:F0}ml hydration");
                summary.Losses.Add($"{hydration:F0}ml hydration");
            }
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
                "Disturbed" => ActiveTension.Disturbed(tc.Severity, tc.RelevantLocation, tc.Description),
                "WoundedPrey" => ActiveTension.WoundedPrey(tc.Severity, tc.AnimalType, tc.RelevantLocation),
                "PackNearby" => ActiveTension.PackNearby(tc.Severity, tc.AnimalType),
                "ClaimedTerritory" => ActiveTension.ClaimedTerritory(tc.Severity, tc.AnimalType, tc.RelevantLocation),
                "HerdNearby" => ActiveTension.HerdNearby(tc.Severity, tc.AnimalType, tc.Direction),
                "DeadlyCold" => ActiveTension.DeadlyCold(tc.Severity),
                "FeverRising" => ActiveTension.FeverRising(tc.Severity, tc.Description),
                "TrapLineActive" => ActiveTension.TrapLineActive(tc.Severity, tc.RelevantLocation),
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
                {
                    GameDisplay.AddDanger(ctx, $"  - {tool.Name} breaks!");
                    summary.Losses.Add($"{tool.Name} destroyed");
                }
                else
                {
                    GameDisplay.AddWarning(ctx, $"  - {tool.Name} damaged");
                    summary.Losses.Add($"{tool.Name} damaged");
                }
            }
        }

        // Tool break - completely destroy the tool
        if (outcome.BreakTool is not null)
        {
            var tool = ctx.Inventory.GetTool(outcome.BreakTool.Value);
            if (tool != null)
            {
                tool.Durability = 0;
                GameDisplay.AddDanger(ctx, $"  - {tool.Name} breaks!");
                summary.Losses.Add($"{tool.Name} destroyed");
            }
        }

        // Clothing damage - reduce insulation
        if (outcome.DamageClothing is not null)
        {
            var equipment = ctx.Inventory.GetEquipment(outcome.DamageClothing.Slot);
            if (equipment != null)
            {
                equipment.Insulation = Math.Max(0, equipment.Insulation - outcome.DamageClothing.InsulationLoss);
                GameDisplay.AddWarning(ctx, $"  - {equipment.Name} damaged");
                summary.Losses.Add($"{equipment.Name} damaged");
            }
        }

        // Feature modifications
        if (outcome.AddFeature is not null)
        {
            var feature = CreateFeatureFromConfig(outcome.AddFeature);
            if (feature != null)
            {
                ctx.CurrentLocation.AddFeature(feature);
                GameDisplay.AddNarrative(ctx, $"Added {feature.Name} to this location.");
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
                    GameDisplay.AddDanger(ctx, $"  - {feature.Name} destroyed");
                    summary.Losses.Add($"{feature.Name} destroyed");
                }
            }
            else if (outcome.RemoveFeature == typeof(HeatSourceFeature))
            {
                var feature = ctx.CurrentLocation.GetFeature<HeatSourceFeature>();
                if (feature != null)
                {
                    ctx.CurrentLocation.RemoveFeature(feature);
                    GameDisplay.AddDanger(ctx, $"  - {feature.Name} destroyed");
                    summary.Losses.Add($"{feature.Name} destroyed");
                }
            }
        }

        // Display outcome summary
        if (summary.HasContent)
        {
            GameDisplay.AddNarrative(ctx, "");
            GameDisplay.AddNarrative(ctx, "--- Outcome ---", LogLevel.System);
            foreach (var gain in summary.Gains)
                GameDisplay.AddSuccess(ctx, $"  + {gain}");
            foreach (var loss in summary.Losses)
                GameDisplay.AddDanger(ctx, $"  - {loss}");
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
