using text_survival.Actions;
using text_survival.Actions.Events.Variants;
using text_survival.Crafting;
using text_survival.Desktop.Dto;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;

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
        => throw new NotImplementedException("Desktop discovery UI not yet implemented");

    public static void ShowWeatherChange(GameContext ctx)
        => throw new NotImplementedException("Desktop weather UI not yet implemented");

    public static void ShowDiscoveryLogAndWait(GameContext ctx)
        => throw new NotImplementedException("Desktop discovery log UI not yet implemented");

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
        => throw new NotImplementedException("Desktop hazard UI not yet implemented");

    // Forage methods
    public static (ForageFocus? focus, int minutes) SelectForageOptions(GameContext ctx, ForageDto forageData)
        => throw new NotImplementedException("Desktop forage UI not yet implemented");

    // Butcher methods
    public static string? SelectButcherOptions(GameContext ctx, ButcherDto butcherData)
        => throw new NotImplementedException("Desktop butcher UI not yet implemented");

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
