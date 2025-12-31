namespace text_survival.Crafting;

public static class NeedCategoryDisplay
{
    private static readonly Dictionary<NeedCategory, string> DisplayNames = new()
    {
        [NeedCategory.FireStarting] = "Fire Starting",
        [NeedCategory.CuttingTool] = "Tools",
        [NeedCategory.HuntingWeapon] = "Weapons",
        [NeedCategory.Trapping] = "Trapping",
        [NeedCategory.Processing] = "Materials",
        [NeedCategory.Treatment] = "Medicine",
        [NeedCategory.Equipment] = "Clothing",
        [NeedCategory.Lighting] = "Lighting",
        [NeedCategory.Carrying] = "Storage",
        [NeedCategory.CampInfrastructure] = "Camp",
        [NeedCategory.Mending] = "Repair"
    };

    public static string GetDisplayName(NeedCategory category) =>
        DisplayNames.TryGetValue(category, out var name) ? name : category.ToString();
}
