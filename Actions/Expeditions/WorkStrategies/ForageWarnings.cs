using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Generates contextual warnings for the forage overlay.
/// Extracted from ForageStrategy to allow direct API access.
/// </summary>
public static class ForageWarnings
{
    /// <summary>
    /// Generate warnings about conditions that affect foraging.
    /// </summary>
    public static List<string> Generate(GameContext ctx, Location location)
    {
        var warnings = new List<string>();

        // Check darkness
        var previewContext = AbilityContext.FromFullContext(
            ctx.player, ctx.Inventory, location, ctx.GameTime.Hour);
        if (previewContext.DarknessLevel > 0.5 && !previewContext.HasLightSource)
            warnings.Add("It's dark - your yield will be reduced without light.");

        // Check tool bonuses
        var axe = ctx.Inventory.GetTool(ToolType.Axe);
        var shovel = ctx.Inventory.GetTool(ToolType.Shovel);
        if (axe?.Works == true)
            warnings.Add("Your axe will help gather wood.");
        if (shovel?.Works == true)
            warnings.Add("Your shovel will help dig up roots.");

        // Capacity warning when pack is nearly full
        var inv = ctx.Inventory;
        if (inv.MaxWeightKg > 0)
        {
            double capacityPct = inv.CurrentWeightKg / inv.MaxWeightKg;
            if (capacityPct >= 0.8)
            {
                double remaining = inv.RemainingCapacityKg;
                warnings.Add($"Your pack is nearly full ({remaining:F1}kg remaining).");
            }
        }

        return warnings;
    }
}
