using text_survival.Actions;
using text_survival.UI;

namespace text_survival.Items;

/// <summary>
/// Helper for capacity-aware inventory operations with UI feedback.
/// </summary>
public static class InventoryCapacityHelper
{
    private const string TutorialKey = "capacity_full";

    /// <summary>
    /// Combines items into player inventory respecting capacity limits.
    /// Displays a message if any items were left behind.
    /// Returns true if any items were dropped.
    /// </summary>
    public static bool CombineAndReport(GameContext ctx, Inventory source)
    {
        var leftovers = ctx.Inventory.CombineWithCapacity(source);

        if (!leftovers.IsEmpty)
        {
            GameDisplay.AddWarning(ctx, $"Your pack is full. You left behind: {leftovers.GetDescription()}");

            if (!ctx.HasShownTutorial(TutorialKey))
            {
                GameDisplay.AddNarrative(ctx, "You can store extra items at camp to free up space.");
                ctx.MarkTutorialShown(TutorialKey);
            }

            return true;
        }

        return false;
    }
}
