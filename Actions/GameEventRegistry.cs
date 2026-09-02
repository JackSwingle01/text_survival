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
    // Reduced from 0.25 to 0.15 after implementing intentional triggers for
    // weather transitions, tension stage changes, and survival thresholds
    private const double EventsPerHour = .15;
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
    // [ThreadStatic] so parallel simulation runs cannot race on this dictionary or clear
    // each other's cooldowns. In the real game there is one thread, so behavior is unchanged.
    [ThreadStatic] private static Dictionary<string, DateTime>? _eventTriggerTimes;
    private static Dictionary<string, DateTime> EventTriggerTimes => _eventTriggerTimes ??= new();

    public static void ClearTriggerTimes() => EventTriggerTimes.Clear();

    private static bool IsOnCooldown(string eventName, int cooldownHours, DateTime gameTime)
    {
        if (!EventTriggerTimes.TryGetValue(eventName, out var lastTrigger))
            return false;
        return (gameTime - lastTrigger).TotalHours < cooldownHours;
    }

    public static List<Func<GameContext, GameEvent>> AllEventFactories { get; } =
    [
        // Weather events (GameEventRegistry.Weather.cs)
        // Note: Whiteout, LostInFog, SuddenClearing, MassiveStormApproaching
        // are now intentional triggers via WeatherEventFactory (fire on transitions)
        StormApproaching,
        ColdExposure,  // Unified: FrostbiteWarning, ColdRainSoaking, BitterWind
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
        OldCampsite,
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
        BeehiveSpotted,

        // Equipment wear events (GameEventRegistry.Equipment.cs)
        BootFailure,
        GlovesFraying,
        KnifeDulling,
        ChestWrapTearing,
        FirestarterFailing,

        // Fishing events (GameEventRegistry.Fishing.cs)
        IceGivesWay,
        BearAtFishingHole,
        WolvesCirclingNets,
        FumbleOnIce,
        LuckyCatch,

        // Scavenger events (GameEventRegistry.Scavenger.cs)
        CirclingScavengers,
        ContestedCarcass,
        ThePacksLeavings,
        Opportunists,
        ScavengersGambit,

        // Small game sightings (GameEventRegistry.SmallGame.cs)
        RabbitFreeze,
        BirdsRoosting,
        FishVisible,
        TrackIntersection,
        GrouseFlushed,

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
        RebuildTheShelter,
        TheSilence
    ];

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
    public static async Task<EventResult> HandleEvent(GameContext ctx, GameEvent evt)
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
            var choiceId = await ctx.Ui.ShowEventChoices(eventDto);

            int choiceIndex = eventDto.Choices.FindIndex(c => c.Id == choiceId);
            if (choiceIndex < 0)
                throw new InvalidOperationException($"Event '{evt.Name}' got back an unknown choice id: {choiceId}");

            var choice = availableChoices[choiceIndex];

            var outcome = choice.DetermineResult();
            var outcomeData = await HandleOutcome(ctx, outcome);

            // Phase 2: Show outcome in same popup
            var outcomeDto = new EventDto(
                evt.Name,
                choice.Description,
                [],
                outcomeData
            );
            await ctx.Ui.ShowEventOutcome(outcomeDto);

            // Queue encounter if needed
            if (outcome.SpawnEncounter != null)
                ctx.QueueEncounter(outcome.SpawnEncounter);

            // Chain to follow-up event if specified
            if (outcome.ChainEvent != null)
            {
                var chainedEvent = outcome.ChainEvent(ctx);
                await HandleEvent(ctx, chainedEvent);
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
    public static async Task<EventOutcomeDto> HandleOutcome(GameContext ctx, EventResult outcome)
    {
        // Let the time cost pass on screen before applying the rest of the outcome, so
        // the player feels it. No event check - we are already handling one.
        if (outcome.TimeAddedMinutes > 0)
        {
            string statusText = outcome.Message.Length <= 60
                ? outcome.Message
                : "Time passes...";

            using var view = ctx.Ui.BeginProgress(ProgressKind.Activity, statusText);
            await Pacing.PassTime(ctx, outcome.TimeAddedMinutes, ctx.CurrentActivity, view, allowEvents: false);
        }

        // The time cost has already passed on screen; Apply only does the rest.
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
            SemanticId(choice.Label, index),
            choice.Label,
            choice.Description,
            hasResources,
            costString
        );
    }

    /// <summary>
    /// A stable, readable id for a choice. The index keeps it unique when two choices
    /// share a label.
    /// </summary>
    private static string SemanticId(string label, int index)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(label.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        if (slug.Length > 30) slug = slug[..30];
        return $"{slug}_{index}";
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
            ResourceType.Water => inv.Weight(Resource.Water) >= cost.Amount * 0.25,
            ResourceType.PlantFiber => inv.Count(Resource.PlantFiber) >= cost.Amount,
            ResourceType.Medicine => inv.GetCount(ResourceCategory.Medicine) >= cost.Amount,
            _ => true
        };
    }
}
