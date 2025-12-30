using text_survival.IO;
using text_survival.UI;
using text_survival.Web;
using text_survival.Web.Dto;

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

    // Event cooldown tracking - persisted via save/load
    private static Dictionary<string, DateTime> EventTriggerTimes { get; } = new();

    public static void ClearTriggerTimes() => EventTriggerTimes.Clear();
    public static Dictionary<string, DateTime> GetTriggerTimes() => new(EventTriggerTimes);
    public static void LoadTriggerTimes(Dictionary<string, DateTime> times)
    {
        EventTriggerTimes.Clear();
        foreach (var (name, time) in times)
            EventTriggerTimes[name] = time;
    }

    private static bool IsOnCooldown(string eventName, int cooldownHours, DateTime gameTime)
    {
        if (!EventTriggerTimes.TryGetValue(eventName, out var lastTrigger))
            return false;
        return (gameTime - lastTrigger).TotalHours < cooldownHours;
    }

    /// <summary>
    /// Event factories that create fresh events with context baked in.
    /// </summary>
    public static List<Func<GameContext, GameEvent>> AllEventFactories { get; } =
    [
        // Weather events (GameEventRegistry.Weather.cs)
        StormApproaching,
        Whiteout,
        ColdExposure,  // Unified: FrostbiteWarning, ColdRainSoaking, BitterWind
        LostInFog,
        SuddenClearing,
        MassiveStormApproaching,  // Prolonged blizzard warning
        WaterproofingPayoff,  // Positive feedback when waterproofed gear works
        SoakedThrough,  // Negative feedback when lacking waterproofing

        // Expedition events (GameEventRegistry.Expedition.cs)
        TreacherousFooting,
        SomethingCatchesYourEye,
        MinorAccident,
        GlintInAshes,
        OldCampsite,
        WaterSource,
        UnexpectedYield,
        TrailGoesCold,
        ExposedPosition,
        NaturalShelterSpotted,
        Debris,
        // Location condition events
        DarkPassage,
        WaterCrossing,
        ExposedOnRidge,
        AmbushOpportunity,
        // Environmental signs
        TrailSignEvent,
        // Spatial discovery events
        DistantSmoke,
        EdgeOfTheIce,

        // Water/Ice events (GameEventRegistry.Water.cs)
        FallThroughIce,
        GetFootWet,

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
        // Spatial pressure events
        CutOff,
        // Blood Trail Arc
        BloodInSnow,
        TheDyingAnimal,
        ScavengersConverge,
        // Carcass events
        CarcassInvestigation,
        ScavengerApproach,
        ContestedKill,
        CarcassClaimed,

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
        BaitedTrapAttention,

        // Foraging events (GameEventRegistry.Foraging.cs)
        MushroomPatch,
        SquirrelCache,
        BeehiveSpotted,

        // Location-specific events - Tier 1 (GameEventRegistry.Locations.cs)
        DiscoverFireOrigin,
        SpottedInOpen,
        LogShifts,
        DeadfallDen,
        PreviousUse,
        SmokeBuildsUp,
        SpotMovement,
        MutualVisibility,
        WeatherTurns,

        // Location-specific events - Tier 2 (GameEventRegistry.Locations.cs)
        // TheSilence removed - now handled by FirstVisitAncientGrove
        NeedAnAxe,
        SharpEdges,
        FreshTracks,
        EscapeIntoThicket,
        CaughtInBrush,
        TwistedAnkle,
        SeeForMiles,
        RidgeWindChill,

        // Location-specific events - Tier 3 (GameEventRegistry.Locations.cs)
        // Bear Cave
        BearCache,
        BearSigns,
        HibernatingBear,
        // Beaver Dam
        BeaverActivity,
        DamWeakening,
        DrainedPond,
        ExposedLodge,

        // Location-specific events - Tier 4 (GameEventRegistry.Locations.cs)
        // The Lookout
        ClimbTheLookout,
        StormOnTheHorizon,
        SpotFromHeight,
        // Old Campsite
        InvestigateRemnants,
        FindTheJournal,
        WhatKilledThem,
        RebuildTheShelter
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

            // Filter: skip if any excluded conditions are met
            if (evt.ExcludedConditions.Any(ctx.Check))
                continue;

            // Filter: skip if required situations not met
            if (!evt.RequiredSituations.All(s => s(ctx)))
                continue;

            // Filter: skip if on cooldown
            if (IsOnCooldown(evt.Name, evt.CooldownHours, ctx.GameTime))
                continue;

            // Filter: skip if location name doesn't match
            if (evt.RequiredLocationName != null && ctx.CurrentLocation?.Name != evt.RequiredLocationName)
                continue;

            // Calculate weight with modifiers
            double weight = evt.BaseWeight;
            foreach (var (condition, modifier) in evt.WeightFactors)
            {
                if (ctx.Check(condition))
                    weight *= modifier;
            }
            // Apply Situation-based weight factors
            foreach (var (situation, modifier) in evt.SituationFactors)
            {
                if (situation(ctx))
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
    /// Returns the EventResult so callers can check flags like AbortsExpedition.
    /// </summary>
    public static EventResult HandleEvent(GameContext ctx, GameEvent evt)
    {
        List<ActivityType> excluded = [ActivityType.Sleeping, ActivityType.Fighting, ActivityType.Encounter];
        if (excluded.Contains(ctx.CurrentActivity))
            return new EventResult("", 1.0, 0);  // No-op result

        // Record trigger time for cooldown
        EventTriggerTimes[evt.Name] = ctx.GameTime;

        // Prevent nested events from triggering during this event's outcome processing
        ctx.IsHandlingEvent = true;
        try
        {
            if (ctx.CurrentActivity == ActivityType.Sleeping)
            {
                GameDisplay.AddNarrative(ctx, "You wake suddenly!");
            }

            GameDisplay.AddNarrative(ctx, $"{evt.Name}", LogLevel.Warning);
            GameDisplay.AddNarrative(ctx, evt.Description);

            // Phase 1: Show event with choices
            // Use same ID generation as WebIO.Select to ensure frontend/backend match
            var eventDto = new EventDto(
                evt.Name,
                evt.Description,
                evt.GetAvailableChoices(ctx)
                    .Select((c, i) => BuildChoiceDto(ctx, c, i))
                    .ToList()
            );
            WebIO.RenderEvent(ctx, eventDto);

            var choice = evt.GetChoice(ctx);
            GameDisplay.AddNarrative(ctx, choice.Description + "\n");

            var outcome = choice.DetermineResult();
            var outcomeData = HandleOutcome(ctx, outcome);

            // Phase 2: Show outcome in same popup
            var outcomeDto = new EventDto(
                evt.Name,
                choice.Description,
                [],
                outcomeData
            );
            WebIO.RenderEvent(ctx, outcomeDto);
            WebIO.WaitForEventContinue(ctx);

            // Clear event overlay after user acknowledges
            WebIO.ClearEvent(ctx);

            // Queue encounter if needed
            if (outcome.SpawnEncounter != null)
            {
                ctx.QueueEncounter(outcome.SpawnEncounter);
                // Skip Render() - encounter spawns immediately after HandleEvent returns
                // and will send its own frames via WaitForEncounterChoice
            }
            else
            {
                // Only render if no encounter pending
                GameDisplay.Render(ctx);
            }

            // Chain to follow-up event if specified
            if (outcome.ChainEvent != null)
            {
                var chainedEvent = outcome.ChainEvent(ctx);
                HandleEvent(ctx, chainedEvent);
            }

            return outcome;
        }
        finally
        {
            ctx.IsHandlingEvent = false;
        }
    }

    /// <summary>
    /// Apply an event outcome - delegates to EventResult.Apply().
    /// Returns outcome data for UI display.
    /// </summary>
    public static EventOutcomeDto HandleOutcome(GameContext ctx, EventResult outcome)
    {
        return outcome.Apply(ctx);
    }

    /// <summary>
    /// Build a choice DTO with cost display and availability validation.
    /// </summary>
    private static EventChoiceDto BuildChoiceDto(GameContext ctx, EventChoice choice, int index)
    {
        var maxCost = choice.GetMaxCost();
        var costString = maxCost != null ? FormatCost(maxCost) : null;
        var hasResources = maxCost == null || HasSufficientResources(ctx.Inventory, maxCost);

        return new EventChoiceDto(
            WebIO.GenerateSemanticId(choice.Label, index),
            choice.Label,
            choice.Description,
            hasResources,
            costString
        );
    }

    /// <summary>
    /// Format a resource cost for display.
    /// </summary>
    private static string FormatCost(ResourceCost cost)
    {
        var typeName = cost.Type switch
        {
            ResourceType.Fuel => "fuel",
            ResourceType.Tinder => "tinder",
            ResourceType.Food => "food",
            ResourceType.Water => "water",
            ResourceType.PlantFiber => "plant fiber",
            ResourceType.Medicine => "medicine",
            _ => cost.Type.ToString().ToLower()
        };
        return $"{cost.Amount} {typeName}";
    }

    /// <summary>
    /// Check if inventory has sufficient resources for a cost.
    /// </summary>
    private static bool HasSufficientResources(Inventory inv, ResourceCost cost)
    {
        return cost.Type switch
        {
            ResourceType.Fuel => inv.GetCount(ResourceCategory.Fuel) >= cost.Amount,
            ResourceType.Tinder => inv.GetCount(ResourceCategory.Tinder) >= cost.Amount,
            ResourceType.Food => inv.GetCount(ResourceCategory.Food) >= cost.Amount,
            ResourceType.Water => inv.WaterLiters >= cost.Amount * 0.25,
            ResourceType.PlantFiber => inv.Count(Resource.PlantFiber) >= cost.Amount,
            ResourceType.Medicine => inv.GetCount(ResourceCategory.Medicine) >= cost.Amount,
            _ => true
        };
    }
}
