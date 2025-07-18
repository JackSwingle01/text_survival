
using text_survival.Environments;
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
    public List<PropertyRequirement> RequiredProperties { get; set; } = [];
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

    private static bool HasSufficientProperty(Player player, PropertyRequirement requirement)
    {
        double totalAmount = 0;
        double averageQuality = 0;
        int itemCount = 0;

        foreach (var stack in player.inventoryManager.Items)
        {
            var item = stack.FirstItem;
            var property = item.GetProperty(requirement.PropertyName);

            if (property != null && property.Quality >= requirement.MinQuality)
            {
                totalAmount += property.Quantity * stack.Count;
                averageQuality += property.Quality * stack.Count;
                itemCount += stack.Count;
            }
        }

        if (itemCount > 0)
            averageQuality /= itemCount;

        return totalAmount >= requirement.MinQuantity && averageQuality >= requirement.MinQuality;
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

    private static void ConsumeProperty(Player player, PropertyRequirement requirement)
    {
        double remainingNeeded = requirement.MinQuantity;
        var eligibleStacks = player.inventoryManager.Items
            .Where(stack => stack.FirstItem.HasProperty(requirement.PropertyName, 0, requirement.MinQuality))
            .OrderByDescending(stack => stack.FirstItem.GetProperty(requirement.PropertyName)?.Quality)
            .ToList();

        foreach (var stack in eligibleStacks)
        {
            while (stack.Count > 0 && remainingNeeded > 0)
            {
                var item = stack.FirstItem;
                var property = item.GetProperty(requirement.PropertyName);

                if (property != null && property.Quantity <= remainingNeeded)
                {
                    // Consume entire item
                    remainingNeeded -= property.Quantity;
                    var consumedItem = stack.Pop();
                    player.inventoryManager.RemoveFromInventory(consumedItem);
                }
                else if (property != null)
                {
                    // Partially consume item (reduce its property amount)
                    property.Quantity -= remainingNeeded;
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

                // Apply quality bonus based on skill and input quality
                ApplyQualityBonus(item, skill.Level, GetInputQuality());

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

    private void ApplyQualityBonus(Item item, int skillLevel, double inputQuality)
    {
        double skillBonus = skillLevel * 0.02; // 2% per skill level
        double qualityMultiplier = 1 + skillBonus + (inputQuality * 0.1);

        foreach (var property in item.CraftingProperties)
        {
            property.Quality = Math.Min(1.0, property.Quality * qualityMultiplier);
        }
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