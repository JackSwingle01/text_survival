using text_survival.Actions;
using text_survival.Actions.Events.Variants;
using text_survival.Crafting;
using text_survival.Desktop.Dto;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using ForageFocus = text_survival.Actions.Events.Variants.ForageFocus;

namespace text_survival.Desktop;

/// <summary>
/// Desktop I/O implementation. Stub for Phase 1 migration.
/// All methods throw NotImplementedException until implemented.
/// </summary>
public static class DesktopIO
{
    /// <summary>
    /// Generate a semantic ID from a label for more debuggable choice matching.
    /// </summary>
    public static string GenerateSemanticId(string label, int index)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(label.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        if (slug.Length > 30) slug = slug[..30];
        return $"{slug}_{index}";
    }

    // Clear methods (no-op for now)
    public static void ClearInventory(GameContext ctx) { }
    public static void ClearCrafting(GameContext ctx) { }
    public static void ClearEvent(GameContext ctx) { }
    public static void ClearHazard(GameContext ctx) { }
    public static void ClearConfirm(GameContext ctx) { }
    public static void ClearForage(GameContext ctx) { }
    public static void ClearHunt(GameContext ctx) { }
    public static void ClearTransfer(GameContext ctx) { }
    public static void ClearFire(GameContext ctx) { }
    public static void ClearCooking(GameContext ctx) { }
    public static void ClearButcher(GameContext ctx) { }
    public static void ClearEncounter(GameContext ctx) { }
    public static void ClearCombat(GameContext ctx) { }
    public static void ClearDiscovery(GameContext ctx) { }
    public static void ClearWeatherChange(GameContext ctx) { }
    public static void ClearDiscoveryLog(GameContext ctx) { }
    public static void ClearAllOverlays(string sessionId) { }

    // Hunt methods
    public static void RenderHunt(GameContext ctx, HuntDto huntData)
        => throw new NotImplementedException("Desktop hunt UI not yet implemented");

    public static string WaitForHuntChoice(GameContext ctx, HuntDto huntData)
        => throw new NotImplementedException("Desktop hunt UI not yet implemented");

    public static void WaitForHuntContinue(GameContext ctx)
        => throw new NotImplementedException("Desktop hunt UI not yet implemented");

    // Encounter methods
    public static void RenderEncounter(GameContext ctx, EncounterDto encounterData)
        => throw new NotImplementedException("Desktop encounter UI not yet implemented");

    public static string WaitForEncounterChoice(GameContext ctx, EncounterDto encounterData)
        => throw new NotImplementedException("Desktop encounter UI not yet implemented");

    public static void WaitForEncounterContinue(GameContext ctx)
        => throw new NotImplementedException("Desktop encounter UI not yet implemented");

    // Combat methods
    public static void RenderCombat(GameContext ctx, CombatDto combatData)
        => throw new NotImplementedException("Desktop combat UI not yet implemented");

    public static string WaitForCombatChoice(GameContext ctx, CombatDto combatData)
        => throw new NotImplementedException("Desktop combat UI not yet implemented");

    public static string WaitForTargetChoice(GameContext ctx, List<CombatActionDto> targetingOptions, string animalName)
        => throw new NotImplementedException("Desktop combat UI not yet implemented");

    public static void WaitForCombatContinue(GameContext ctx)
        => throw new NotImplementedException("Desktop combat UI not yet implemented");

    // Discovery/Weather methods
    public static void ShowDiscovery(GameContext ctx, string locationName, string discoveryText)
    {
        BlockingDialog.ShowMessageAndWait(ctx, "Discovery!", $"{locationName}\n\n{discoveryText}");
    }

    public static void ShowWeatherChange(GameContext ctx)
    {
        var weather = ctx.Weather;
        string message = $"The weather has changed.\n\n" +
            $"Temperature: {weather.Temperature:F0}°F\n" +
            $"Wind: {weather.WindSpeedMph:F0} mph\n" +
            (weather.Precipitation > 0 ? $"Precipitation: {weather.Precipitation:P0}" : "Clear skies");
        BlockingDialog.ShowMessageAndWait(ctx, "Weather", message);
    }

    public static void ShowDiscoveryLogAndWait(GameContext ctx)
    {
        // Build discovery summary
        var discoveries = ctx.DiscoveredLocations;
        string message = discoveries.Count > 0
            ? $"You have discovered {discoveries.Count} locations:\n\n" + string.Join("\n", discoveries.Take(10).Select(d => $"  - {d}"))
            : "You haven't discovered any locations yet.";

        if (discoveries.Count > 10)
            message += $"\n  ... and {discoveries.Count - 10} more";

        BlockingDialog.ShowMessageAndWait(ctx, "Discovery Log", message);
    }

    // Core selection methods
    public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices, Func<T, string> display, Func<T, bool>? isDisabled = null)
        => BlockingDialog.Select(ctx, prompt, choices, display, isDisabled);

    public static bool Confirm(GameContext ctx, string prompt)
        => BlockingDialog.Confirm(ctx, prompt);

    public static string PromptConfirm(GameContext ctx, string message, Dictionary<string, string> buttons)
        => BlockingDialog.PromptConfirm(ctx, message, buttons);

    public static int ReadInt(GameContext ctx, string prompt, int min, int max, bool allowCancel = false)
        => BlockingDialog.ReadInt(ctx, prompt, min, max, allowCancel);

    // Event methods
    public static void WaitForEventContinue(GameContext ctx)
        => BlockingDialog.ShowMessageAndWait(ctx, "Event", "Press continue to proceed.");

    public static void RenderEvent(GameContext ctx, EventDto eventData)
    {
        // For now, just show a message with the event description
        // Full event overlay integration would come later
        BlockingDialog.ShowMessageAndWait(ctx, "Event", eventData.Description ?? "Something happened...");
    }

    // Render methods
    public static void Render(GameContext ctx, string? statusText = null)
    {
        // Single frame render - used for intermediate states
        DesktopRuntime.RenderFrame(ctx);
    }

    public static void RenderWithDuration(GameContext ctx, string statusText, int estimatedMinutes)
    {
        // Show progress bar while time passes
        BlockingDialog.ShowProgress(ctx, statusText, estimatedMinutes);

    public static void RenderTravelProgress(
        GameContext ctx,
        string statusText,
        int estimatedMinutes,
        int startX,
        int startY)
        => throw new NotImplementedException("Desktop travel render not yet implemented");

    // Inventory methods
    public static void RenderInventory(GameContext ctx, Inventory inventory, string title)
        => throw new NotImplementedException("Desktop inventory UI not yet implemented");

    public static void ShowInventoryAndWait(GameContext ctx, Inventory inventory, string title)
        => throw new NotImplementedException("Desktop inventory UI not yet implemented");

    // Crafting methods
    public static void RenderCrafting(GameContext ctx, NeedCraftingSystem crafting, string title = "CRAFTING")
        => throw new NotImplementedException("Desktop crafting UI not yet implemented");

    // Grid/Map methods
    public static PlayerResponse RenderGridAndWaitForInput(GameContext ctx, string? statusText = null)
        => throw new NotImplementedException("Desktop grid UI not yet implemented");

    // Hazard methods
    public static bool PromptHazardChoice(
        GameContext ctx,
        Location targetLocation,
        int targetX,
        int targetY,
        int quickTimeMinutes,
        int carefulTimeMinutes,
        double hazardLevel)
    {
        int riskPercent = (int)(hazardLevel * 100);
        string message = $"Hazardous terrain ahead: {targetLocation.Name}\n\n" +
            $"Risk of injury: {riskPercent}%\n\n" +
            $"Quick crossing: {quickTimeMinutes} minutes (full risk)\n" +
            $"Careful crossing: {carefulTimeMinutes} minutes (reduced risk)";

        var buttons = new Dictionary<string, string>
        {
            { "quick", $"Quick ({quickTimeMinutes}min)" },
            { "careful", $"Careful ({carefulTimeMinutes}min)" },
            { "cancel", "Turn back" }
        };

        string choice = BlockingDialog.PromptConfirm(ctx, message, buttons);
        return choice == "quick";
    }

    // Forage methods
    public static (ForageFocus? focus, int minutes) SelectForageOptions(GameContext ctx, ForageDto forageData)
    {
        // First select focus
        var focusChoices = new List<(string id, string label)>
        {
            ("cancel", "Cancel")
        };
        foreach (var opt in forageData.FocusOptions)
        {
            focusChoices.Add((opt.Id, $"{opt.Label} ({opt.Description})"));
        }

        var focusSelection = BlockingDialog.Select(ctx, $"Foraging at {forageData.LocationQuality} location.\n\nSelect your focus:",
            focusChoices, c => c.label);

        if (focusSelection.id == "cancel")
            return (null, 0);

        ForageFocus focus = focusSelection.id switch
        {
            "fuel" => ForageFocus.Fuel,
            "food" => ForageFocus.Food,
            "medicine" => ForageFocus.Medicine,
            "materials" => ForageFocus.Materials,
            _ => ForageFocus.General
        };

        // Then select time
        var timeChoices = new List<(string id, int minutes, string label)>
        {
            ("cancel", 0, "Cancel")
        };
        foreach (var opt in forageData.TimeOptions)
        {
            timeChoices.Add((opt.Id, opt.Minutes, opt.Label));
        }

        var timeSelection = BlockingDialog.Select(ctx, "How long will you forage?",
            timeChoices, c => c.label);

        if (timeSelection.id == "cancel")
            return (null, 0);

        return (focus, timeSelection.minutes);
    }

    // Butcher methods
    public static string? SelectButcherOptions(GameContext ctx, ButcherDto butcherData)
    {
        var choices = new List<(string id, string label)>
        {
            ("cancel", "Cancel")
        };

        foreach (var mode in butcherData.Modes)
        {
            string timeStr = mode.TimeMinutes > 0 ? $" ({mode.TimeMinutes}min)" : "";
            choices.Add((mode.Id, $"{mode.Label}{timeStr}"));
        }

        string description = $"Butcher: {butcherData.AnimalName}\n" +
            $"Condition: {butcherData.Condition}\n" +
            $"Estimated yield: {butcherData.EstimatedYieldKg:F1}kg";

        var selection = BlockingDialog.Select(ctx, description, choices, c => c.label);

        return selection.id == "cancel" ? null : selection.id;
    }

    // Work result methods
    public static void ShowWorkResult(GameContext ctx, string activityName, string message, List<string> itemsGained)
    {
        string fullMessage = message;
        if (itemsGained.Count > 0)
        {
            fullMessage += "\n\nGained:\n" + string.Join("\n", itemsGained.Select(i => $"  - {i}"));
        }
        BlockingDialog.ShowMessageAndWait(ctx, activityName, fullMessage);
    }

    // Death screen
    public static void ShowDeathScreen(GameContext ctx, DeathScreenDto data)
    {
        string deathMessage = $"You have died.\n\n" +
            $"Cause: {data.CauseOfDeath}\n" +
            $"Days Survived: {data.DaysSurvived}\n" +
            $"Distance Traveled: {data.DistanceTraveled:F1} miles";
        BlockingDialog.ShowMessageAndWait(ctx, "DEATH", deathMessage);
    }

    // Complex UI loops
    public static void RunTransferUI(GameContext ctx, Inventory storage, string storageName)
        => throw new NotImplementedException("Desktop transfer UI not yet implemented");

    public static void RunFireUI(GameContext ctx, HeatSourceFeature fire)
        => throw new NotImplementedException("Desktop fire UI not yet implemented");

    public static void RunEatingUI(GameContext ctx)
        => throw new NotImplementedException("Desktop eating UI not yet implemented");

    public static void RunCookingUI(GameContext ctx)
        => throw new NotImplementedException("Desktop cooking UI not yet implemented");
}
