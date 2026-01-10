using text_survival.Actions;
using text_survival.UI;

namespace text_survival.Items;

/// <summary>
/// Helper for capacity-aware inventory operations with UI feedback.
/// </summary>
public static class InventoryCapacityHelper
{
    /// <summary>
    /// Combines items into player inventory respecting capacity limits.
    /// Displays a message if any items were left behind.
    /// Returns the leftovers that didn't fit.
    /// </summary>
    public static Inventory CombineAndReport(GameContext ctx, Inventory source)
    {
        var leftovers = ctx.Inventory.CombineWithCapacity(source);

        if (!leftovers.IsEmpty)
        {
            GameDisplay.AddWarning(ctx, $"Your pack is full. You left behind: {leftovers.GetDescription()}");
            ctx.ShowTutorialOnce("You can store extra items at camp to free up space.");
        }

        return leftovers;
    }
}
