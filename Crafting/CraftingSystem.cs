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

        // Phase 3: Fire-making skill check system
        if (recipe.BaseSuccessChance.HasValue)
        {
            // Calculate success chance
            double successChance = recipe.BaseSuccessChance.Value;

            if (recipe.SkillCheckDC.HasValue)
            {
                var skill = _player.Skills.GetSkill(recipe.RequiredSkill);
                double skillModifier = (skill.Level - recipe.SkillCheckDC.Value) * 0.1;
                successChance += skillModifier;
            }

            // Clamp to 5-95% range (never guaranteed, never impossible)
            successChance = Math.Clamp(successChance, 0.05, 0.95);

            // Roll for success
            bool success = Utils.DetermineSuccess(successChance);

            if (!success)
            {
                // Failure - materials consumed, small XP, no result
                Output.WriteWarning($"You failed to craft {recipe.Name}. The materials were consumed in the attempt.");
                _player.Skills.GetSkill(recipe.RequiredSkill).GainExperience(1); // Learning from failure
                return;
            }

            Output.WriteSuccess($"Success! ({successChance:P0} chance)");
        }

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
                    // Partially consume item
                    item.Weight -= remainingNeeded;
                    remainingNeeded = 0;
                }
            }

            if (remainingNeeded <= 0) break;
        }
    }


    private void InitializeRecipes()
    {
        // Phase 3: Fire-Making (critical for survival)
        CreateFireRecipes();

        // Basic Tools
        CreateBasicToolRecipes();

        // Location Features
        CreateLocationFeatureRecipes();

        // Shelter Construction
        CreateShelterRecipes();

        // Cooking Recipes
        CreateCookingRecipes();
    }

    // Phase 3: Fire-Making Recipes with skill checks
    private void CreateFireRecipes()
    {
        // Hand Drill - Primitive friction fire (30% base success, improves with skill)
        var handDrill = new RecipeBuilder()
            .Named("Hand Drill Fire")
            .WithDescription("Primitive fire-making by friction. Difficult but requires only natural materials.")
            .RequiringCraftingTime(20)
            .WithPropertyRequirement(ItemProperty.Wood, 0.5)  // Dry stick
            .WithPropertyRequirement(ItemProperty.Tinder, 0.05) // Tinder bundle
            .WithSuccessChance(0.3)  // 30% base chance
            .WithSkillCheck("Firecraft", 0)  // +10% per skill level
            .ResultingInLocationFeature(new LocationFeatureResult("Campfire", location =>
            {
                var fire = new HeatSourceFeature(location, 10.0);
                fire.AddFuel(0.5);
                return fire;
            }))
            .Build();
        _recipes.Add("hand_drill", handDrill);

        // Bow Drill - Improved friction fire (50% base success)
        var bowDrill = new RecipeBuilder()
            .Named("Bow Drill Fire")
            .WithDescription("An improved friction fire method using a bow mechanism for consistent pressure.")
            .RequiringCraftingTime(45)
            .WithPropertyRequirement(ItemProperty.Wood, 1.0)  // 2 sticks for bow + drill
            .WithPropertyRequirement(ItemProperty.Binding, 0.1)  // Sinew or plant fiber
            .WithPropertyRequirement(ItemProperty.Tinder, 0.05)
            .WithSuccessChance(0.5)  // 50% base chance
            .WithSkillCheck("Firecraft", 1)  // Requires skill 1+, +10% per level above
            .ResultingInLocationFeature(new LocationFeatureResult("Campfire", location =>
            {
                var fire = new HeatSourceFeature(location, 10.0);
                fire.AddFuel(0.5);
                return fire;
            }))
            .Build();
        _recipes.Add("bow_drill", bowDrill);

        // Flint & Steel - Reliable fire (90% success, no skill required)
        var flintSteel = new RecipeBuilder()
            .Named("Flint & Steel Fire")
            .WithDescription("Strike flint against stone to create sparks. Reliable once you have the materials.")
            .RequiringCraftingTime(5)
            .WithPropertyRequirement(ItemProperty.Firestarter, 0.2)  // Flint
            .WithPropertyRequirement(ItemProperty.Stone, 0.3)  // Striker stone
            .WithPropertyRequirement(ItemProperty.Tinder, 0.05)
            .WithSuccessChance(0.9)  // 90% success, no skill modifier
            .ResultingInLocationFeature(new LocationFeatureResult("Campfire", location =>
            {
                var fire = new HeatSourceFeature(location, 10.0);
                fire.AddFuel(0.5);
                return fire;
            }))
            .Build();
        _recipes.Add("flint_steel", flintSteel);
    }

    private void CreateBasicToolRecipes()
    {
        var stoneKnife = new RecipeBuilder()
            .Named("Stone Knife")
            .WithDescription("A simple cutting tool made from sharp stone.")
            .RequiringCraftingTime(15)
            .WithPropertyRequirement(ItemProperty.Stone, 1)
            .WithPropertyRequirement(ItemProperty.Wood, .5)
            .WithPropertyRequirement(ItemProperty.Binding, .2)
            .ResultingInItem(ItemFactory.MakeKnife)
            .Build();
        _recipes.Add("stone_knife", stoneKnife);

        // Spear
        var spear = new RecipeBuilder()
            .Named("Wooden Spear")
            .WithDescription("A hunting spear with a sharpened point.")
            .RequiringSkill("Crafting", 1)
            .RequiringCraftingTime(20)
            .WithPropertyRequirement(ItemProperty.Wood, 1.5)
            .WithPropertyRequirement(ItemProperty.Stone, 0.5)
            .WithPropertyRequirement(ItemProperty.Binding, 0.3)
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
            .WithPropertyRequirement(ItemProperty.Flammable, 3)
            .WithPropertyRequirement(ItemProperty.Stone, 2) // For fire ring
            .WithPropertyRequirement(ItemProperty.Firestarter, .2)
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
            .WithPropertyRequirement(ItemProperty.Wood, 6)
            .WithPropertyRequirement(ItemProperty.Binding)
            .WithPropertyRequirement(ItemProperty.Insulation, 2, false) // Leaves, furs, etc.
            .ResultingInStructure("Lean-to Shelter", CreateLeanToShelter)
            .Build();
        _recipes.Add("lean_to", leanTo);

        // Advanced Shelter
        var cabin = new RecipeBuilder()
            .Named("Log Cabin")
            .WithDescription("A sturdy shelter providing excellent protection.")
            .RequiringSkill("Crafting", 3)
            .RequiringCraftingTime(480) // 8 hours
            .WithPropertyRequirement(ItemProperty.Wood, 20)
            .WithPropertyRequirement(ItemProperty.Stone, 5) // For foundation
            .WithPropertyRequirement(ItemProperty.Binding, 3)
            .WithPropertyRequirement(ItemProperty.Insulation, 8)
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
            .WithPropertyRequirement(ItemProperty.RawMeat, 1)
            .ResultingInItem(() => new FoodItem("Cooked Meat", 800, 0, 1.0)
            {
                CraftingProperties = [ItemProperty.CookedMeat]
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
