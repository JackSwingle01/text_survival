using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Desktop;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for salvaging equipment and resources from wrecks, ruins, etc.
/// Requires SalvageFeature with remaining loot.
/// Impaired by moving capacity (searching through wreckage).
/// </summary>
public class SalvageStrategy : IWorkStrategy
{
    private List<string> _impairmentWarnings = [];

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var salvage = location.GetFeature<SalvageFeature>();
        if (salvage == null || !salvage.HasLoot)
            return "There's nothing to salvage here.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        // Salvage has fixed time based on feature - no player choice
        return null;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var salvage = location.GetFeature<SalvageFeature>()!;
        int workTime = salvage.MinutesToSalvage;

        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        // Store impairment warnings for later use in Execute
        _impairmentWarnings = warnings;

        return ((int)(workTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => "salvaging";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var salvage = location.GetFeature<SalvageFeature>()!;

        // Collect narrative for overlay display
        var narrative = new List<string>();

        // Add narrative hook if present
        if (!string.IsNullOrEmpty(salvage.NarrativeHook))
        {
            narrative.Add(salvage.NarrativeHook);
        }

        narrative.Add($"You begin searching through the {salvage.DisplayName.ToLower()}...");

        // Check if there are personal belongings (equipment or tools)
        bool hasPersonalItems = salvage.Equipment.Count > 0 || salvage.Tools.Count > 0;

        if (hasPersonalItems)
        {
            // Build preview for confirm dialog
            var previewLines = new List<string> { "You find:" };
            foreach (var equip in salvage.Equipment)
                previewLines.Add($"- {equip.Name}");
            foreach (var tool in salvage.Tools)
                previewLines.Add($"- {tool.Name}");
            if (!salvage.Resources.IsEmpty)
                previewLines.Add($"- {salvage.Resources.GetDescription()}");

            string previewText = string.Join("\n", previewLines);

            // Present moral choice
            if (!DesktopIO.Confirm(ctx, $"{previewText}\n\nTake their belongings?"))
            {
                DesktopIO.ShowWorkResult(ctx, "Salvaging", "You leave everything as you found it.", [], narrative, _impairmentWarnings);
                return new WorkResult([], null, actualTime, false);
            }
        }

        // Get loot (marks site as salvaged)
        var loot = salvage.Salvage();

        if (loot.IsEmpty)
        {
            DesktopIO.ShowWorkResult(ctx, "Salvaging", "You find nothing useful.", [], narrative, _impairmentWarnings);
            return new WorkResult([], null, actualTime, false);
        }

        var collected = new List<string>();

        // Add tools to inventory
        foreach (var tool in loot.Tools)
        {
            ctx.Inventory.Tools.Add(tool);
            collected.Add(tool.Name);
        }

        // Add equipment to inventory (auto-equip if possible)
        foreach (var equip in loot.Equipment)
        {
            var replaced = ctx.Inventory.Equip(equip);
            collected.Add(replaced != null ? $"{equip.Name} (replaced {replaced.Name})" : equip.Name);
        }

        // Add resources to inventory
        if (!loot.Resources.IsEmpty)
        {
            collected.Add(loot.Resources.GetDescription());
            InventoryCapacityHelper.CombineAndReport(ctx, loot.Resources);
        }

        // Show results in popup overlay (include impairment warnings if any)
        DesktopIO.ShowWorkResult(ctx, "Salvaging", "You gather what you can find.", collected, narrative, _impairmentWarnings);

        return new WorkResult(collected, null, actualTime, false);
    }
}
