using text_survival.UI;
using text_survival.Desktop;
using DesktopIO = text_survival.Desktop.DesktopIO;
using text_survival.Desktop.Dto;

namespace text_survival.Actions;

/// <summary>
/// Event system registry and execution.
/// Event factory methods are defined in partial class files under Actions/Events/
/// </summary>
public static partial class GameEventRegistry
{
    public record TickResult(int MinutesElapsed, GameEvent? TriggeredEvent);

    // Single knob to control overall event frequency
    private const double EventsPerHour = .25;
    private static readonly double BaseChancePerMinute = RateToChancePerMinute(EventsPerHour);

    private static double RateToChancePerMinute(double eventsPerHour)
    {
        double ratePerMinute = eventsPerHour / 60.0;
        return 1 - Math.Exp(-ratePerMinute);
    }

    /// <summary>
    /// Early game event scaling - reduces event frequency in first hours to let players learn.
    /// </summary>
    private static double GetEarlyGameMultiplier(GameContext ctx)
    {
        var hoursElapsed = ctx.TotalMinutesElapsed / 60.0;
        if (hoursElapsed < 2) return 0.3;   // First 2 hours: 30% of normal
        if (hoursElapsed < 6) return 0.6;   // Hours 2-6: 60% of normal
        return 1.0;                          // After hour 6: full rate
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

        // Expedition events
        TreacherousFooting,
        SomethingCatchesYourEye,
        MinorAccident,
        GlintInAshes,
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
        // Early game aspiration
        TrackingSomething,

        // Water/Ice events 
        FallThroughIce,
        GetFootWet,

        // Camp infrastructure events
        VerminRaid,
        ShelterGroans,
        ChokingSmoke,
        EmbersScatter,
        RustleAtCampEdge,
        MeltingReveal,
        MountainGlimpse,  // Early game goal-setting event

        // Threat events
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

        // Saber-tooth escalation/confrontation (GameEventRegistry.SaberTooth.cs)
        SomethingWatches,
        TheAmbush,

        // Mammoth hunt arc (GameEventRegistry.Megafauna.cs)
        FreshSpoor,
        TheBull,
        ColdSnapDuringHunt,
        WolvesSmellBlood,
        TheHerd,
        TheMatriarchsWarning,
        TheHerdMoves,
        TheCharge,

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

        // Consciousness events
        LostYourBearings,

        // Moving impairment events 
        StrugglingToKeepPace,

        // Manipulation impairment events 
        FumblingHands,

        // Perception impairment events
        DulledSenses,

        // Den arc events 
        TheFind,
        AssessingTheClaim,
        TheConfrontation,
        // ClaimingTheDen is chained from successful eviction outcomes, not random

        // Pack arc events 
        PackSigns,
        EyesInTreeline,
        Circling,
        ThePackCommits,

        // Fever arc events
        SomethingWrong,
        FeverTakesHold,
        TheFireIllusion,
        FootstepsOutside,
        FeverCrisisPoint,

        // Trapping events
        SnareTampered,
        PredatorAtTrapLine,
        GoodCatch,
        TrapLinePlundered,
        TrappingAccident,
        BaitedTrapAttention,

        // Foraging events
        LuckyFind,

        // Location-specific events
        SpottedInOpen,
        LogShifts,
        SmokeBuildsUp,
        MutualVisibility,
        WeatherTurns,

        // Location-specific events
        EscapeIntoThicket,
        CaughtInBrush,
        TwistedAnkle,
        RidgeWindChill,

        // Location-specific events
        BearSigns,
        HibernatingBear,
        DamWeakening,
        DrainedPond,
        ExposedLodge,

        // Location-specific events
        ClimbTheLookout,
        StormOnTheHorizon,
        SpotFromHeight,
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
        // Activity multiplier from config + early game scaling to reduce events in first hours
        double earlyGameMultiplier = GetEarlyGameMultiplier(ctx);
        double chance = BaseChancePerMinute * activityMultiplier * earlyGameMultiplier;
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
            // Phase 1: Show event with choices via overlay
            var availableChoices = evt.GetAvailableChoices(ctx);
            var eventDto = new EventDto(
                evt.Name,
                evt.Description,
                availableChoices
                    .Select((c, i) => BuildChoiceDto(ctx, c, i))
                    .ToList()
            );

            // Block until player makes a choice
            var choiceId = DesktopIO.WaitForEventChoice(ctx, eventDto);

            // Map choice ID back to EventChoice (IDs are "choice_0", "choice_1", etc.)
            int choiceIndex = 0;
            if (choiceId.StartsWith("choice_") && int.TryParse(choiceId[7..], out var idx))
            {
                choiceIndex = idx;
            }
            var choice = availableChoices[Math.Clamp(choiceIndex, 0, availableChoices.Count - 1)];

            var outcome = choice.DetermineResult();
            var outcomeData = HandleOutcome(ctx, outcome);

            // Phase 2: Show outcome in same popup
            var outcomeDto = new EventDto(
                evt.Name,
                choice.Description,
                [],
                outcomeData
            );
            DesktopIO.RenderEvent(ctx, outcomeDto);
            DesktopIO.WaitForEventContinue(ctx);

            // Clear event overlay after user acknowledges
            DesktopIO.ClearEvent(ctx);

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
    /// Apply an event outcome - shows progress bar for time costs, then applies effects.
    /// Returns outcome data for UI display.
    /// </summary>
    public static EventOutcomeDto HandleOutcome(GameContext ctx, EventResult outcome)
    {
        // Show progress bar for time costs before applying outcome
        if (outcome.TimeAddedMinutes > 0)
        {
            // Use a brief summary for the progress bar status text
            string statusText = outcome.Message.Length <= 60
                ? outcome.Message
                : "Time passes...";
            BlockingDialog.ShowEventProgress(ctx, statusText, outcome.TimeAddedMinutes, ctx.CurrentActivity);
        }

        // Apply outcome, skipping time (already handled by progress bar)
        return outcome.Apply(ctx, skipTime: outcome.TimeAddedMinutes > 0);
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
            DesktopIO.GenerateSemanticId(choice.Label, index),
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
