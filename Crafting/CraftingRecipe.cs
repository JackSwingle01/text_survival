
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

    public bool CanCraft(Player player)
    {
        // Check skill requirement
        var skill = player.Skills.GetSkill(RequiredSkill);
        if (skill.Level < RequiredSkillLevel)
            return false;

        // Check if player has required properties
        if (!HasRequiredProperties(player))
            return false;

        // Check location requirements
        if (RequiresFire && !HasFire(player.CurrentLocation))
            return false;

        // Check specific result type requirements
        if (ResultType == CraftingResultType.Shelter && NewLocationResult != null)
        {
            // Can't build shelter inside another shelter
            if (player.CurrentLocation.GetFeature<ShelterFeature>() != null)
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
        double totalAmount = 0;
        int itemCount = 0;

        foreach (var stack in player.inventoryManager.Items)
        {
            var item = stack.FirstItem;
            var property = item.GetProperty(requirement.Property);

            if (property != null)
            {
                totalAmount += stack.TotalWeight;
                itemCount += stack.Count;
            }
        }
        return totalAmount >= requirement.MinQuantity;
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
        var preview = new List<(string, double)>();

        foreach (var requirement in RequiredProperties.Where(r => r.IsConsumed))
        {
            double remainingNeeded = requirement.MinQuantity;
            var eligibleStacks = player.inventoryManager.Items
                .Where(stack => stack.FirstItem.HasProperty(requirement.Property, 0))
                .ToList();

            foreach (var stack in eligibleStacks)
            {
                // Create a copy of the stack to iterate without modifying
                var stackCopy = new List<Item>(stack.Items);
                int stackIndex = 0;

                while (stackIndex < stackCopy.Count && remainingNeeded > 0)
                {
                    var item = stackCopy[stackIndex];
                    var property = item.GetProperty(requirement.Property);

                    if (property != null && item.Weight <= remainingNeeded)
                    {
                        // Would consume entire item
                        preview.Add((item.Name, item.Weight));
                        remainingNeeded -= item.Weight;
                        stackIndex++;
                    }
                    else if (property != null)
                    {
                        // Would partially consume item
                        preview.Add((item.Name, remainingNeeded));
                        remainingNeeded = 0;
                    }
                    else
                    {
                        // Item doesn't have the property, skip it
                        stackIndex++;
                    }
                }

                if (remainingNeeded <= 0) break;
            }
        }

        return preview;
    }

    private static void ConsumeProperty(Player player, CraftingPropertyRequirement requirement)
    {
        double remainingNeeded = requirement.MinQuantity;
        var eligibleStacks = player.inventoryManager.Items
            .Where(stack => stack.FirstItem.HasProperty(requirement.Property, 0))
            .ToList();

        foreach (var stack in eligibleStacks)
        {
            while (stack.Count > 0 && remainingNeeded > 0)
            {
                var item = stack.FirstItem;
                var property = item.GetProperty(requirement.Property);

                if (property != null && item.Weight <= remainingNeeded)
                {
                    // Consume entire item
                    remainingNeeded -= item.Weight;
                    var consumedItem = stack.Pop();
                    player.inventoryManager.RemoveFromInventory(consumedItem);
                }
                else if (property != null)
                {
                    // Partially consume item (reduce its property amount)
                    item.Weight -= remainingNeeded;
                    remainingNeeded = 0;
                }
            }

            if (remainingNeeded <= 0) break;
        }
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