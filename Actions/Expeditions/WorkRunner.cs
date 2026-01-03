using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Unified work execution for all locations (camp or expedition).
/// Returns WorkResult - caller handles logging and expedition tracking.
/// </summary>
public class WorkRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private bool PlayerDied => !_ctx.player.IsAlive;

    /// <summary>
    /// Check if location is too dark to work. Returns true if work is blocked.
    /// Darkness can come from: inherent location darkness OR nighttime.
    /// </summary>
    private bool CheckDarknessBlocking(Location location, IWorkStrategy strategy)
    {
        // If strategy allows darkness work, skip blocking
        if (strategy.AllowedInDarkness)
            return false;

        // Check inherent location darkness OR nighttime
        bool isNight = _ctx.GetTimeOfDay() == GameContext.TimeOfDay.Night;
        bool isDark = location.IsDark || isNight;

        if (!isDark)
            return false;

        // Active fire provides light
        if (location.HasActiveHeatSource())
            return false;

        // Active torch provides light
        if (_ctx.Inventory.HasLitTorch)
            return false;

        string reason = isNight ? "It's too dark to work at night." : "It's too dark to work here.";
        GameDisplay.AddWarning(_ctx, $"{reason} You need a light source.");
        GameDisplay.Render(_ctx, statusText: "Darkness.");
        return true;
    }

    /// <summary>
    /// Execute work using a strategy pattern. Handles validation, timing, impairments, and execution.
    /// </summary>
    private WorkResult ExecuteWork(Location location, IWorkStrategy strategy)
    {
        if (CheckDarknessBlocking(location, strategy))
            return WorkResult.Empty(0);

        // Validate location
        string? validationError = strategy.ValidateLocation(_ctx, location);
        if (validationError != null)
        {
            GameDisplay.AddNarrative(_ctx, validationError);
            return WorkResult.Empty(0);
        }

        // Get time options (may be null for fixed-time work)
        var timeChoice = strategy.GetTimeOptions(_ctx, location);
        int workTime = 0;
        if (timeChoice != null)
        {
            GameDisplay.Render(_ctx, statusText: "Planning.");
            workTime = timeChoice.GetPlayerChoice(_ctx);

            if (workTime == 0) // Player cancelled
                return WorkResult.Empty(0);
        }

        // Apply impairments and show warnings
        var (adjustedTime, warnings) = strategy.ApplyImpairments(_ctx, location, workTime);
        foreach (var warning in warnings)
        {
            GameDisplay.AddWarning(_ctx, warning);
        }

        // Capture stats before work for delta display
        _ctx.StatsBeforeWork = (
            _ctx.player.Body.Energy,
            _ctx.player.Body.CalorieStore,
            _ctx.player.Body.Hydration,
            _ctx.player.Body.BodyTemperature
        );

        // Run work with time passage (if time > 0)
        int actualTime = adjustedTime;
        if (adjustedTime > 0)
        {
            var (died, elapsed) = RunWorkWithContinuePrompt(
                location,
                adjustedTime,
                strategy.GetActivityType(),
                strategy.GetActivityName()
            );

            if (died)
                return WorkResult.Died(elapsed);

            actualTime = elapsed;
        }

        // Execute the work
        var result = strategy.Execute(_ctx, location, actualTime);

        // Show UI and check weight
        GameDisplay.Render(_ctx, statusText: "Thinking.");

        ForceDropIfOverweight();

        return result;
    }

    public WorkResult DoForage(Location location)
    {
        return ExecuteWork(location, new ForageStrategy());
    }

    public WorkResult DoHarvest(Location location)
    {
        return ExecuteWork(location, new HarvestStrategy());
    }

    public WorkResult DoSalvage(Location location)
    {
        return ExecuteWork(location, new SalvageStrategy());
    }

    // === PROGRESS AND TIME PASSAGE ===

    /// <summary>
    /// Runs work with progress bar and event checks, prompting to continue after events.
    /// Returns (died, actualMinutesWorked).
    /// </summary>
    private (bool died, int minutesWorked) RunWorkWithContinuePrompt(
        Location location, int workMinutes, ActivityType activity, string activityName)
    {
        int totalElapsed = 0;

        while (totalElapsed < workMinutes)
        {
            int remaining = workMinutes - totalElapsed;
            var (died, segmentElapsed) = RunWorkSegment(location, remaining, activity);
            totalElapsed += segmentElapsed;

            if (died)
                return (true, totalElapsed);

            // Check if an event interrupted us
            if (_ctx.EventOccurredLastUpdate && totalElapsed < workMinutes)
            {
                // If event aborted, stop immediately without prompting
                if (_ctx.LastEventAborted)
                    break;

                int remainingAfterEvent = workMinutes - totalElapsed;
                GameDisplay.Render(_ctx, statusText: "Interrupted");

                if (!WebIO.Confirm(_ctx, $"Continue {activityName}? ({remainingAfterEvent} min remaining)"))
                    break;
            }
        }

        return (false, totalElapsed);
    }

    /// <summary>
    /// Runs a single segment of work until completion, death, or event interruption.
    /// Returns (died, elapsedMinutes).
    /// </summary>
    private (bool died, int elapsed) RunWorkSegment(Location location, int workMinutes, ActivityType activity)
    {
        // Use centralized progress method - handles web animation and processes all time at once
        var (elapsed, interrupted) = GameDisplay.UpdateAndRenderProgress(_ctx, location.Name, workMinutes, activity);

        if (PlayerDied)
            return (true, elapsed);

        return (false, elapsed);
    }

    // === TRAPPING ===

    /// <summary>
    /// Set a snare at this location. Requires AnimalTerritoryFeature.
    /// </summary>
    public WorkResult DoSetTrap(Location location)
    {
        return ExecuteWork(location, new TrapStrategy(TrapStrategy.TrapMode.Set));
    }

    /// <summary>
    /// Check all snares at this location.
    /// </summary>
    public WorkResult DoCheckTraps(Location location)
    {
        return ExecuteWork(location, new TrapStrategy(TrapStrategy.TrapMode.Check));
    }

    /// <summary>
    /// Access a cache at the location to store/retrieve items.
    /// </summary>
    public WorkResult DoCache(Location location)
    {
        return ExecuteWork(location, new CacheStrategy());
    }

    // === WORK OPTIONS (used by ExpeditionRunner) ===
    
    /// <summary>
    /// Execute work by ID. Finds the matching WorkOption and executes its strategy.
    /// </summary>
    public WorkResult ExecuteById(Location location, string workId)
    {
        var option = location.GetWorkOptions(_ctx).FirstOrDefault(o => o.Id == workId);
        if (option == null) return WorkResult.Empty(0);
        return ExecuteWork(location, option.Strategy);
    }

    // === HELPERS ===

    /// <summary>
    /// Prompts player to travel to a newly discovered location.
    /// </summary>
    public static bool PromptTravelToDiscovery(GameContext ctx, Location discovered)
    {
        int travelMinutes = TravelProcessor.GetTraversalMinutes(ctx.CurrentLocation, discovered, ctx.player, ctx.Inventory);
        GameDisplay.AddNarrative(ctx, $"You've found a path to {discovered.Name}.");
        GameDisplay.Render(ctx, statusText: "Discovery!");

        return WebIO.Confirm(ctx, $"Go to {discovered.Name} now? (~{travelMinutes} min)");
    }

    /// <summary>
    /// Calculate chance to discover a new location.
    /// In grid mode, base chance with visibility bonus.
    /// </summary>
    public static double CalculateExploreChance(Location location)
    {
        double baseChance = 0.70;

        // High visibility improves scouting (up to +20% at visibility 2.0)
        double chance = baseChance + location.VisibilityFactor * 0.10;

        return Math.Min(0.95, chance);
    }

    public static string GetForageFailureMessage(string quality)
    {
        string[] messages = quality switch
        {
            "abundant" =>
            [
                "Fresh snow. Everything's buried.",
                "What you spot is rotten through.",
                "Frozen solid to the ground. Can't pry it loose.",
                "A sound nearby. You wait it out, lose your momentum.",
                "Ice crust over everything. Takes too long to break through.",
            ],
            "decent" =>
            [
                "Hollow log, empty inside. Wasted time.",
                "Wind-scoured ground. Bare rock in every crevice.",
                "Drifts deeper than they looked. Hard to search properly.",
                "What you find crumbles apart in your hands.",
                "Steep terrain. You cover less ground than planned.",
            ],
            "sparse" =>
            [
                "Slim pickings. Most of it's already gone.",
                "Traces of what was here. Nearly spent.",
                "Hardly anything left. You'd need luck.",
                "Almost picked clean. Time to look elsewhere.",
                "Scraps and remnants. This place won't last.",
            ],
            _ =>
            [
                "Stripped bare.",
                "It's gone. All of it.",
                "You're wasting time here.",
                "Barren.",
                "Move on.",
            ],
        };

        return messages[Random.Shared.Next(messages.Length)];
    }

    /// <summary>
    /// Check if player is over carry capacity and force them to drop items.
    /// </summary>
    private void ForceDropIfOverweight()
    {
        var inv = _ctx.Inventory;

        if (inv.RemainingCapacityKg >= 0)
            return;

        GameDisplay.ClearNarrative(_ctx);
        GameDisplay.AddWarning(_ctx,
            $"You're carrying too much! ({inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0} kg)"
        );
        GameDisplay.AddNarrative(_ctx, "You must drop some items.");
        GameDisplay.Render(_ctx, statusText: "Overburdened.");

        // Create a dummy "drop target" that just discards items
        var dropTarget = new Inventory { MaxWeightKg = 10000 };

        while (inv.RemainingCapacityKg < 0)
        {
            var items = inv.GetTransferableItems(dropTarget);
            if (items.Count == 0)
                break;

            var options = items.Select(i => $"{i.Description}").ToList();

            GameDisplay.ClearNarrative(_ctx);
            GameDisplay.AddWarning(_ctx,
                $"Over capacity by {-inv.RemainingCapacityKg:F1} kg. Drop something."
            );
            GameDisplay.Render(_ctx, statusText: "Overburdened.");

            string selected = Input.Select(_ctx, "Drop which item?", options);
            int idx = options.IndexOf(selected);

            items[idx].TransferTo();
            GameDisplay.AddNarrative(_ctx, $"Dropped {items[idx].Description}");
        }

        GameDisplay.AddNarrative(_ctx, "You adjust your load and continue.");
        GameDisplay.Render(_ctx, statusText: "Relieved.");
    }
}
