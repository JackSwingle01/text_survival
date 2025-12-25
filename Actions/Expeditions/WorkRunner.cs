using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
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
    private bool CheckDarknessBlocking(Location location)
    {
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
        Input.WaitForKey(_ctx);
        return true;
    }

    /// <summary>
    /// Execute work using a strategy pattern. Handles validation, timing, impairments, and execution.
    /// </summary>
    private WorkResult ExecuteWork(Location location, IWorkStrategy strategy)
    {
        if (CheckDarknessBlocking(location))
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
        Input.WaitForKey(_ctx);

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

    public WorkResult DoExplore(Location location)
    {
        return ExecuteWork(location, new ExploreStrategy());
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
                int remainingAfterEvent = workMinutes - totalElapsed;
                GameDisplay.Render(_ctx, statusText: "Interrupted");

                var choice = new Choice<bool>($"Continue {activityName}? ({remainingAfterEvent} min remaining)");
                choice.AddOption("Continue", true);
                choice.AddOption("Stop", false);

                if (!choice.GetPlayerChoice(_ctx))
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
        int elapsed = 0;

        while (elapsed < workMinutes)
        {
            GameDisplay.Render(
                _ctx,
                addSeparator: false,
                statusText: location.Name,
                progress: elapsed,
                progressTotal: workMinutes
            );

            int min = _ctx.Update(1, activity);
            elapsed += min;

            if (PlayerDied)
                return (true, elapsed);

            // Break on event so caller can prompt for continuation
            if (_ctx.EventOccurredLastUpdate)
                break;

            Thread.Sleep(100);
        }

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

    // === WORK OPTIONS (used by GameRunner and ExpeditionRunner) ===

    /// <summary>
    /// Check if any work is available at a location.
    /// </summary>
    public static bool HasWorkOptions(GameContext ctx, Location location, bool includeHunt = false)
    {
        if (location.HasFeature<ForageFeature>())
            return true;

        var harvestables = location
            .Features.OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources());
        if (harvestables.Any())
            return true;

        // Salvage sites
        var salvage = location.GetFeature<SalvageFeature>();
        if (salvage != null && salvage.HasLoot)
            return true;

        // Caches
        var cache = location.GetFeature<CacheFeature>();
        if (cache != null)
            return true;

        if (includeHunt && location.HasFeature<AnimalTerritoryFeature>())
            return true;

        // Trapping options
        if (CanSetTrap(ctx, location) || CanCheckTraps(location))
            return true;

        if (ctx.HasUnrevealedLocations())
            return true;

        return false;
    }

    /// <summary>
    /// Check if player can set a trap at this location.
    /// </summary>
    private static bool CanSetTrap(GameContext ctx, Location location)
    {
        return location.HasFeature<AnimalTerritoryFeature>() &&
               ctx.Inventory.Tools.Any(t => t.Type == ToolType.Snare && t.Works);
    }

    /// <summary>
    /// Check if there are traps to check at this location.
    /// </summary>
    private static bool CanCheckTraps(Location location)
    {
        var snareLine = location.GetFeature<SnareLineFeature>();
        return snareLine != null && snareLine.SnareCount > 0;
    }

    /// <summary>
    /// Get work options menu for a location. Returns null if no options available.
    /// </summary>
    public static Choice<string>? GetWorkOptions(
        GameContext ctx,
        Location location,
        bool includeHunt = false
    )
    {
        var choice = new Choice<string>("What work do you want to do?");
        bool hasOptions = false;

        if (location.HasFeature<ForageFeature>())
        {
            var forage = location.GetFeature<ForageFeature>()!;
            choice.AddOption($"Forage for resources ({forage.GetQualityDescription()})", "forage");
            hasOptions = true;
        }

        var harvestables = location
            .Features.OfType<HarvestableFeature>()
            .Where(h => h.IsDiscovered && h.HasAvailableResources());
        if (harvestables.Any())
        {
            choice.AddOption("Harvest resources", "harvest");
            hasOptions = true;
        }

        // Salvage sites
        var salvage = location.GetFeature<SalvageFeature>();
        if (salvage != null && salvage.HasLoot)
        {
            choice.AddOption($"Salvage {salvage.DisplayName} ({salvage.GetLootHint()})", "salvage");
            hasOptions = true;
        }

        // Caches
        var cache = location.GetFeature<CacheFeature>();
        if (cache != null)
        {
            string status = cache.HasItems ? $"{cache.Storage.CurrentWeightKg:F1}kg stored" : "empty";
            choice.AddOption($"Access {cache.Name} ({status})", "cache");
            hasOptions = true;
        }

        if (includeHunt && location.HasFeature<AnimalTerritoryFeature>())
        {
            var territory = location.GetFeature<AnimalTerritoryFeature>()!;
            choice.AddOption($"Hunt ({territory.GetQualityDescription()})", "hunt");
            hasOptions = true;
        }

        // Trapping options
        if (CanSetTrap(ctx, location))
        {
            var snareCount = ctx.Inventory.Tools.Count(t => t.Type == ToolType.Snare && t.Works);
            choice.AddOption($"Set snare ({snareCount} available)", "set_trap");
            hasOptions = true;
        }

        if (CanCheckTraps(location))
        {
            var snareLine = location.GetFeature<SnareLineFeature>()!;
            choice.AddOption($"Check traps ({snareLine.GetDescription()})", "check_traps");
            hasOptions = true;
        }

        if (ctx.HasUnrevealedLocations())
        {
            choice.AddOption("Explore the area (discover new locations)", "explore");
            hasOptions = true;
        }

        choice.AddOption("Cancel", "cancel");
        return hasOptions ? choice : null;
    }

    /// <summary>
    /// Get labels for available work types (for menu display).
    /// </summary>
    public static List<string> GetWorkLabels(GameContext ctx, Location location)
    {
        var labels = new List<string>();

        if (location.HasFeature<ForageFeature>())
            labels.Add("Forage");

        if (ctx.HasUnrevealedLocations())
            labels.Add("Scout");

        return labels;
    }

    // === HELPERS ===

    /// <summary>
    /// Calculate chance to discover a new location.
    /// Decreases exponentially with existing connections.
    /// High visibility locations are better for scouting.
    /// </summary>
    public static double CalculateExploreChance(Location location)
    {
        int connections = location.ConnectionNames.Count;
        double baseChance = 0.90;
        double decayFactor = 0.55;
        double chance = baseChance * Math.Pow(decayFactor, connections);

        // High visibility improves scouting (up to +20% at visibility 2.0)
        chance += location.VisibilityFactor * 0.10;

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
        Input.WaitForKey(_ctx);

        // Create a dummy "drop target" that just discards items
        var dropTarget = new Inventory { MaxWeightKg = -1 };

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
        Input.WaitForKey(_ctx);
    }
}
