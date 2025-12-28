namespace text_survival.Items;

/// <summary>
/// Extension methods for Resource categorization.
/// Used by foraging focus system to filter resources by category.
/// </summary>
public static class ResourceExtensions
{
    public static bool IsFuel(this Resource r) =>
        ResourceCategories.Items[ResourceCategory.Fuel].Contains(r);

    public static bool IsFood(this Resource r) =>
        ResourceCategories.Items[ResourceCategory.Food].Contains(r);

    public static bool IsMedicine(this Resource r) =>
        ResourceCategories.Items[ResourceCategory.Medicine].Contains(r);

    public static bool IsMaterial(this Resource r) =>
        ResourceCategories.Items[ResourceCategory.Material].Contains(r);

    public static bool IsTinder(this Resource r) =>
        ResourceCategories.Items[ResourceCategory.Tinder].Contains(r);

    /// <summary>
    /// Get the primary category for a resource.
    /// Returns null if resource doesn't belong to any category.
    /// </summary>
    public static ResourceCategory? GetCategory(this Resource r)
    {
        foreach (var (category, resources) in ResourceCategories.Items)
            if (resources.Contains(r))
                return category;
        return null;
    }

    /// <summary>
    /// Check if a resource matches a foraging focus category.
    /// </summary>
    public static bool MatchesFocus(this Resource r, ResourceCategory? focus)
    {
        if (focus == null) return true; // General search matches all
        return GetCategory(r) == focus;
    }
}
