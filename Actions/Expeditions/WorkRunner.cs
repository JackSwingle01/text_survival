using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.UI;

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
        return true;
    }

    /// <summary>
    /// Execute work using a strategy pattern. Handles validation, timing, impairments, and execution.
    /// </summary>
    private async Task<WorkResult> ExecuteWork(Location location, IWorkStrategy strategy)
    {
        if (CheckDarknessBlocking(location, strategy))
            return WorkResult.Empty(0);

        // Validate location
        string? validationError = await strategy.ValidateLocation(_ctx, location);
        if (validationError != null)
        {
            GameDisplay.AddNarrative(_ctx, validationError);
            return WorkResult.Empty(0);
        }

        // Get time options (may be null for fixed-time work)
        var timeChoice = await strategy.GetTimeOptions(_ctx, location);
        int workTime = 0;
        if (timeChoice != null)
        {
            workTime = await timeChoice.GetPlayerChoice(_ctx);

            if (workTime == 0) // Player cancelled
                return WorkResult.Empty(0);
        }

        // Apply impairments (warnings will be shown in overlay by strategy)
        var (adjustedTime, warnings) = strategy.ApplyImpairments(_ctx, location, workTime);

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
            // Check for custom progress handler (e.g., foraging with loot reveals)
            var customResult = await strategy.RunCustomProgress(_ctx, location, adjustedTime);
            if (customResult.HasValue)
            {
                var (elapsed, interrupted) = customResult.Value;
                actualTime = elapsed;

                if (interrupted || PlayerDied)
                    return WorkResult.Interrupted(actualTime);
            }
            else
            {
                string statusText = $"{char.ToUpper(strategy.GetActivityName()[0])}{strategy.GetActivityName()[1..]}...";
                using var view = _ctx.Ui.BeginProgress(ProgressKind.Activity, statusText);
                var (elapsed, _) = await Pacing.PassTime(_ctx, adjustedTime, strategy.GetActivityType(), view);
                actualTime = elapsed;
            }

            if (PlayerDied)
                return WorkResult.Died(actualTime);
        }

        // Execute the strategy to get results
        var result = await strategy.Execute(_ctx, location, actualTime);

        await ForceDropIfOverweight();

        return result;
    }


    // === TRAPPING ===

    // === WORK OPTIONS (used by ExpeditionRunner) ===

    /// <summary>
    /// Execute a work strategy directly (no lookup needed).
    /// </summary>
    public Task<WorkResult> Execute(Location location, IWorkStrategy strategy)
    {
        return ExecuteWork(location, strategy);
    }

    // === HELPERS ===

    /// <summary>
    /// Prompts player to travel to a newly discovered location.
    /// </summary>
    public static Task<bool> PromptTravelToDiscovery(GameContext ctx, Location discovered)
    {
        int travelMinutes = TravelProcessor.GetTraversalMinutes(ctx.CurrentLocation, discovered, ctx.player, ctx.Inventory);
        GameDisplay.AddNarrative(ctx, $"You've found a path to {discovered.Name}.");

        return ctx.Ui.Confirm($"Go to {discovered.Name} now? (~{travelMinutes} min)");
    }

    public static string GetForageFailureMessage(string quality)
    {
        string[] messages = quality switch
        {
            "exceptional" or "abundant" or "premium" or "lush" =>
            [
                "Fresh snow. Everything's buried.",
                "What you spot is rotten through.",
                "Frozen solid to the ground. Can't pry it loose.",
                "A sound nearby. You wait it out, lose your momentum.",
                "Ice crust over everything. Takes too long to break through.",
            ],
            "plentiful" or "rich" or "good" or "decent" =>
            [
                "Hollow log, empty inside. Wasted time.",
                "Wind-scoured ground. Bare rock in every crevice.",
                "Drifts deeper than they looked. Hard to search properly.",
                "What you find crumbles apart in your hands.",
                "Steep terrain. You cover less ground than planned.",
            ],
            "standard" or "fair" or "moderate" or "modest" =>
            [
                "Slim pickings today. The area's not bad, just unlucky.",
                "You search thoroughly but come up empty.",
                "Frozen ground makes digging pointless.",
                "Wind scattered what was here. Nothing to show for it.",
                "You find traces but nothing worth collecting.",
            ],
            "light" or "sparse" or "thin" =>
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
    private async Task ForceDropIfOverweight()
    {
        var inv = _ctx.Inventory;

        if (inv.RemainingCapacityKg >= 0)
            return;

        GameDisplay.ClearNarrative(_ctx);
        GameDisplay.AddWarning(_ctx,
            $"You're carrying too much! ({inv.CurrentWeightKg:F1}/{inv.MaxWeightKg:F0} kg)"
        );
        GameDisplay.AddNarrative(_ctx, "You must drop some items.");

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

            string selected = await _ctx.Ui.Select("Drop which item?", options, label => label);
            int idx = options.IndexOf(selected);

            items[idx].TransferTo();
            GameDisplay.AddNarrative(_ctx, $"Dropped {items[idx].Description}");
        }

        GameDisplay.AddNarrative(_ctx, "You adjust your load and continue.");
    }
}
