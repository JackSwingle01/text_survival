namespace text_survival.Crafting;

/// <summary>
/// Single source of truth for NeedCategory display names.
/// Used by both Web UI and console UI.
/// </summary>
public static class NeedCategoryDisplay
{
    private static readonly Dictionary<NeedCategory, string> DisplayNames = new()
    {
        [NeedCategory.FireStarting] = "Fire-Starting",
        [NeedCategory.CuttingTool] = "Cutting Tools",
        [NeedCategory.HuntingWeapon] = "Hunting Weapons",
        [NeedCategory.Trapping] = "Trapping",
        [NeedCategory.Processing] = "Processing & Tools",
        [NeedCategory.Treatment] = "Medical Treatments",
        [NeedCategory.Equipment] = "Clothing & Gear",
        [NeedCategory.Lighting] = "Light Sources",
        [NeedCategory.Carrying] = "Carrying Gear",
        [NeedCategory.CampInfrastructure] = "Camp Improvements"
    };

    public static string GetDisplayName(NeedCategory category) =>
        DisplayNames.TryGetValue(category, out var name) ? name : category.ToString();
}
