using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for salvaging equipment and resources from wrecks, ruins, etc.
/// Requires SalvageFeature with remaining loot.
/// Impaired by moving capacity (searching through wreckage).
/// </summary>
public class SalvageStrategy : IWorkStrategy
{
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

        return ((int)(workTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => "salvaging";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var salvage = location.GetFeature<SalvageFeature>()!;

        // Show narrative hook if present
        if (!string.IsNullOrEmpty(salvage.NarrativeHook))
        {
            GameDisplay.AddNarrative(ctx, salvage.NarrativeHook);
        }

        GameDisplay.AddNarrative(ctx, $"You begin searching through the {salvage.DisplayName.ToLower()}...");

        // Check if there are personal belongings (equipment or tools)
        bool hasPersonalItems = salvage.Equipment.Count > 0 || salvage.Tools.Count > 0;

        if (hasPersonalItems)
        {
            // Preview what's available before taking
            GameDisplay.AddNarrative(ctx, "");
            GameDisplay.AddNarrative(ctx, "You find:");
            foreach (var equip in salvage.Equipment)
                GameDisplay.AddNarrative(ctx, $"- {equip.Name}");
            foreach (var tool in salvage.Tools)
                GameDisplay.AddNarrative(ctx, $"- {tool.Name}");
            if (!salvage.Resources.IsEmpty)
                GameDisplay.AddNarrative(ctx, $"- {salvage.Resources.GetDescription()}");

            // Present moral choice
            GameDisplay.Render(ctx, statusText: "Deciding.");

            if (!WebIO.Confirm(ctx, "Take their belongings?"))
            {
                GameDisplay.AddNarrative(ctx, "You leave everything as you found it.");
                return new WorkResult([], null, actualTime, false);
            }
        }

        // Get loot (marks site as salvaged)
        var loot = salvage.Salvage();

        if (loot.IsEmpty)
        {
            WebIO.ShowWorkResult(ctx, "Salvaging", "You find nothing useful.", []);
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

        // Show results in popup overlay
        WebIO.ShowWorkResult(ctx, "Salvaging", "You gather what you can find.", collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
