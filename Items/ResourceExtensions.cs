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
    /// Key into assets/icons/ui/ for this resource. Matches domain types, not display
    /// names, so renamed items keep their icons.
    /// </summary>
    public static string GetIconKey(this Resource r) => r switch
    {
        Resource.RawMeat or Resource.CookedMeat or Resource.DriedMeat => "meat",
        Resource.RawFish or Resource.CookedFish or Resource.DriedFish => "fish",
        Resource.Roots => "roots",
        Resource.Hide or Resource.ScrapedHide or Resource.CuredHide or Resource.MammothHide => "hide",
        Resource.Bone or Resource.Ivory => "bone",
        Resource.Rope or Resource.PlantFiber or Resource.Sinew => "rope",
        _ => r.GetCategory() switch
        {
            ResourceCategory.Material => "materials",
            ResourceCategory.Medicine => "medicine",
            ResourceCategory.Food => "food",
            ResourceCategory.Fuel or ResourceCategory.Tinder or ResourceCategory.Log => "fuel",
            ResourceCategory.Water => "water",
            _ => "foraging"
        }
    };

}
