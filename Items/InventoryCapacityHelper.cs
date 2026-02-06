using text_survival.Actions;
using text_survival.Desktop;
using text_survival.UI;

namespace text_survival.Items;

/// <summary>
/// Helper for capacity-aware inventory operations with UI feedback.
/// </summary>
public static class InventoryCapacityHelper
{
    /// <summary>
    /// Combines items into player inventory respecting capacity limits.
    /// Triggers resource discovery for recipe unlocking.
    /// Displays a message if any items were left behind.
    /// Returns the leftovers that didn't fit.
    /// </summary>
    public static Inventory CombineAndReport(GameContext ctx, Inventory source)
    {
        // Track which resources are new before combining
        var newResources = new List<Resource>();
        foreach (var resourceType in source.GetResourceTypes())
        {
            if (!ctx.Discoveries.DiscoveredResources.Contains(resourceType))
                newResources.Add(resourceType);
        }

        // Discover resources and get newly unlocked recipes
        var unlockedRecipes = ctx.DiscoverResources(source);

        // Show discovery notification if anything new
        if (newResources.Count > 0 || unlockedRecipes.Count > 0)
        {
            DesktopIO.ShowResourceDiscovery(ctx, newResources, unlockedRecipes);
        }

        // Combine with capacity limits
        var leftovers = ctx.Inventory.CombineWithCapacity(source);

        if (!leftovers.IsEmpty)
        {
            ctx.CurrentLocation.AddGroundItems(leftovers);
            GameDisplay.AddWarning(ctx, $"Your pack is full. You dropped: {leftovers.GetDescription()}");
            ctx.ShowTutorialOnce("You can store extra items at camp to free up space.");
        }

        return leftovers;
    }
}
