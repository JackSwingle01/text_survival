
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Actors.Player;
using text_survival.Items;

namespace text_survival.Crafting;

public enum CraftingResultType
{
    Item,
    LocationFeature,
    Shelter
}


public class CraftingRecipe(string name, string description = "")
{
    public string Name { get; set; } = name;
    public string Description { get; set; } = description;
    public List<CraftingPropertyRequirement> RequiredProperties { get; set; } = [];
    public CraftingResultType ResultType { get; set; } = CraftingResultType.Item;
    public List<ItemResult> ResultItems { get; set; } = [];
    public LocationFeatureResult? LocationFeatureResult { get; set; }
    public NewLocationResult? NewLocationResult { get; set; }
    public int RequiredSkillLevel { get; set; } = 0;
    public string RequiredSkill { get; set; } = "Crafting";
    public int CraftingTimeMinutes { get; set; } = 10;
    public bool RequiresFire { get; set; } = false;

    public bool CanCraft(Player player, Camp camp)
    {
        // Check skill requirement
        var skill = player.Skills.GetSkill(RequiredSkill);
        if (skill.Level < RequiredSkillLevel)
            return false;

        // Check if player has required properties
        if (!HasRequiredProperties(player))
            return false;

        // Check location requirements
        if (RequiresFire && !camp.HasActiveFire)
            return false;

        // Check specific result type requirements
        if (ResultType == CraftingResultType.Shelter && NewLocationResult != null)
        {
            // Can't build shelter inside another shelter
            if (camp.Shelter != null)
                return false;
        }

        return true;
    }

    private bool HasRequiredProperties(Player player)
    {
        foreach (var requirement in RequiredProperties)
        {
            if (!HasSufficientProperty(player, requirement))
                return false;
        }
        return true;
    }

    private static bool HasSufficientProperty(Player player, CraftingPropertyRequirement requirement)
    {
        // TODO: Rework crafting requirements to check new aggregate Inventory
        // For now, all property requirements are considered met
        // The old discrete item system (inventoryManager) has been removed
        return true;
    }

    private static bool HasFire(Location location)
    {
        return location.GetFeature<HeatSourceFeature>()?.IsActive == true;
    }

    public void ConsumeIngredients(Player player)
    {
        foreach (var requirement in RequiredProperties.Where(r => r.IsConsumed))
        {
            ConsumeProperty(player, requirement);
        }
    }

    /// <summary>
    /// Preview which items will be consumed without actually consuming them
    /// </summary>
    public List<(string ItemName, double Amount)> PreviewConsumption(Player player)
    {
        // TODO: Rework to use new aggregate Inventory system
        // The old discrete item system (inventoryManager) has been removed
        return [];
    }

    private static void ConsumeProperty(Player player, CraftingPropertyRequirement requirement)
    {
        // TODO: Rework crafting consumption to use new aggregate Inventory system
        // The old discrete item system (inventoryManager) has been removed
    }

    public List<Item> GenerateItemResults(Player player)
    {
        if (ResultType != CraftingResultType.Item)
            return [];

        var results = new List<Item>();
        var skill = player.Skills.GetSkill(RequiredSkill);

        foreach (var result in ResultItems)
        {
            for (int i = 0; i < result.Quantity; i++)
            {
                var item = result.ItemFactory();
                results.Add(item);
            }
        }

        return results;
    }

    private double GetInputQuality()
    {
        // Calculate average quality of consumed materials
        // This could be enhanced to be more sophisticated
        return 1; // Placeholder
    }

}


public class ItemResult
{
    public Func<Item> ItemFactory { get; set; }
    public int Quantity { get; set; }

    public ItemResult(Func<Item> factory, int quantity = 1)
    {
        ItemFactory = factory;
        Quantity = quantity;
    }
}

public class LocationFeatureResult
{
    public Func<Location, LocationFeature> FeatureFactory { get; set; }
    public string FeatureName { get; set; }

    public LocationFeatureResult(string featureName, Func<Location, LocationFeature> factory)
    {
        FeatureName = featureName;
        FeatureFactory = factory;
    }
}

public class NewLocationResult
{
    public Func<Zone, Location> LocationFactory { get; set; }
    public string LocationName { get; set; }

    public NewLocationResult(string locationName, Func<Zone, Location> factory)
    {
        LocationName = locationName;
        LocationFactory = factory;
    }
}