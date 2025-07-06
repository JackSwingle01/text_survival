using text_survival.IO;
using text_survival.Environments;
using text_survival.Items;

namespace text_survival.Crafting;

public class CraftingSystem
{
    private readonly Dictionary<string, CraftingRecipe> _recipes = [];
    private readonly Player _player;

    public CraftingSystem(Player player)
    {
        _player = player;
        InitializeRecipes();
    }

    public IReadOnlyDictionary<string, CraftingRecipe> Recipes => _recipes;

    public List<CraftingRecipe> GetAvailableRecipes()
    {
        return _recipes.Values.Where(recipe => recipe.CanCraft(_player)).ToList();
    }

    public void Craft(CraftingRecipe recipe)
    {
        if (!recipe.CanCraft(_player))
        {
            Output.WriteWarning("You cannot craft this item right now.");
            return;
        }

        Output.WriteLine($"You begin working on {recipe.Name}...");

        // Consume time
        World.Update(recipe.CraftingTimeMinutes);

        // Consume ingredients
        recipe.ConsumeIngredients(_player);

        // Generate results based on type
        switch (recipe.ResultType)
        {
            case CraftingResultType.Item:
                var items = recipe.GenerateItemResults(_player);
                foreach (var item in items)
                {
                    _player.TakeItem(item);
                    Output.WriteSuccess($"You successfully crafted: {item.Name}");
                }
                break;

            case CraftingResultType.LocationFeature:
                if (recipe.LocationFeatureResult != null)
                {
                    var feature = recipe.LocationFeatureResult.FeatureFactory(_player.CurrentLocation);
                    _player.CurrentLocation.Features.Add(feature);
                    Output.WriteSuccess($"You successfully built: {recipe.LocationFeatureResult.FeatureName}");
                }
                break;

            case CraftingResultType.Shelter:
                if (recipe.NewLocationResult != null)
                {
                    var newLocation = recipe.NewLocationResult.LocationFactory(_player.CurrentZone);
                    _player.CurrentZone.Locations.Add(newLocation);
                    newLocation.IsFound = true;
                    Output.WriteSuccess($"You successfully built: {recipe.NewLocationResult.LocationName}");
                    Output.WriteLine($"The {newLocation.Name} is now accessible from this area.");
                }
                break;
        }

        // Grant experience
        int xpGain = recipe.RequiredSkillLevel + 2;
        _player.Skills.GetSkill(recipe.RequiredSkill).GainExperience(xpGain);
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


    private void InitializeRecipes()
    {
        // Basic Tools
        CreateBasicToolRecipes();

        // Location Features
        CreateLocationFeatureRecipes();

        // Shelter Construction
        CreateShelterRecipes();

        // Cooking Recipes
        CreateCookingRecipes();
    }



    private void CreateBasicToolRecipes()
    {
        var stoneKnife = new RecipeBuilder()
            .Named("Stone Knife")
            .WithDescription("A simple cutting tool made from sharp stone.")
            .RequiringCraftingTime(15)
            .WithPropertyRequirement("Stone", 1, .5)
            .WithPropertyRequirement("Wood", .5, .3)
            .WithPropertyRequirement("Binding", .2, .2)
            .ResultingInItem(ItemFactory.MakeKnife)
            .Build();
        _recipes.Add("stone_knife", stoneKnife);

        // Spear
        var spear = new RecipeBuilder()
            .Named("Wooden Spear")
            .WithDescription("A hunting spear with a sharpened point.")
            .RequiringSkill("Crafting", 1)
            .RequiringCraftingTime(20)
            .WithPropertyRequirement("Wood", 1.5, 0.6)
            .WithPropertyRequirement("Stone", 0.5, 0.7)
            .WithPropertyRequirement("Binding", 0.3, 0.4)
            .ResultingInItem(ItemFactory.MakeSpear)
            .Build();
        _recipes.Add("spear", spear);
    }

    private void CreateLocationFeatureRecipes()
    {
        // Campfire
        var campfire = new RecipeBuilder()
            .Named("Campfire")
            .WithDescription("A fire pit for warmth, light, and cooking.")
            .RequiringSkill("Firecraft")
            .RequiringCraftingTime(20)
            .WithPropertyRequirement("Flammable", 3, 0.4)
            .WithPropertyRequirement("Stone", 2, 0.2) // For fire ring
            .WithPropertyRequirement("Firestarter", .2, .5)
            .ResultingInLocationFeature(new LocationFeatureResult("Campfire", location =>
            {
                var fireFeature = new HeatSourceFeature(location, 20.0);
                fireFeature.AddFuel(.8);
                return fireFeature;
            }))
            .Build();
        _recipes.Add("campfire", campfire);
    }

    private void CreateShelterRecipes()
    {
        // Lean-to Shelter
        var leanTo = new RecipeBuilder()
            .Named("Lean-to Shelter")
            .WithDescription("A simple shelter that provides basic protection.")
            .RequiringSkill("Crafting", 1) //todo: add building skill?
            .RequiringCraftingTime(120)
            .WithPropertyRequirement("Wood", 6, 0.4)
            .WithPropertyRequirement("Binding", 1, 0.3)
            .WithPropertyRequirement("Insulation", 2, 0.2, false) // Leaves, furs, etc.
            .ResultingInStructure("Lean-to Shelter", CreateLeanToShelter)
            .Build();
        _recipes.Add("lean_to", leanTo);

        // Advanced Shelter
        var cabin = new RecipeBuilder()
            .Named("Log Cabin")
            .WithDescription("A sturdy shelter providing excellent protection.")
            .RequiringSkill("Crafting", 3)
            .RequiringCraftingTime(480) // 8 hours
            .WithPropertyRequirement("Wood", 20, 0.7)
            .WithPropertyRequirement("Stone", 5, 0.5) // For foundation
            .WithPropertyRequirement("Binding", 3, 0.5)
            .WithPropertyRequirement("Insulation", 8, 0.4)
            .ResultingInStructure("Log Cabin", CreateLogCabin)
            .Build();
        _recipes.Add("log_cabin", cabin);
    }

    private void CreateCookingRecipes()
    {
        // Cooked Meat
        var cookedMeat = new RecipeBuilder()
            .Named("Cooked Meat")
            .WithDescription("Meat cooked over fire for better nutrition.")
            .RequiringSkill("Firecraft")
            .RequiringCraftingTime(15)
            .RequiringFire(true)
            .WithPropertyRequirement("RawMeat", 1, 0.2)
            .ResultingInItem(() => new FoodItem("Cooked Meat", 800, 0, 1.0)
            {
                CraftingProperties = { new ItemCraftingProperty("CookedMeat", 1.2, 0.9) } // Cooking improves nutrition
            })
            .Build();
        _recipes.Add("cooked_meat", cookedMeat);
    }

    private static Location CreateLeanToShelter(Zone parent)
    {
        var shelter = new Location("Lean-to Shelter", parent);
        shelter.Features.Add(new ShelterFeature(shelter, 0.3, 0.6, 0.4)); // Moderate protection
        shelter.Features.Add(new EnvironmentFeature(shelter, 5.0, 0.6, 0.4)); // 5°F warmer
        return shelter;
    }

    private static Location CreateLogCabin(Zone parent)
    {
        var cabin = new Location("Log Cabin", parent);
        cabin.Features.Add(new ShelterFeature(cabin, 0.7, 0.9, 0.8)); // Excellent protection
        cabin.Features.Add(new EnvironmentFeature(cabin, 15.0, 0.9, 0.8)); // 15°F warmer
        return cabin;
    }
}
