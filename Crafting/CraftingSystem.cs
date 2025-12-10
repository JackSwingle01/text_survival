using text_survival.Actors.Player;
using text_survival.Core;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
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

        // Crafting always succeeds - RNG belongs in tool/action usage, not crafting

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

                    // Check for duplicate HeatSourceFeature (campfire bug fix)
                    if (feature is HeatSourceFeature newFire)
                    {
                        var existingFire = _player.CurrentLocation.Features.OfType<HeatSourceFeature>().FirstOrDefault();
                        if (existingFire != null)
                        {
                            // Add fuel to existing fire instead of creating duplicate
                            // Transfer fuel mass from newly created fire to existing fire
                            var initialFuel = ItemFactory.MakeFirewood();
                            existingFire.AddFuel(initialFuel, newFire.FuelMassKg); // Transfer the fuel mass
                            Output.WriteSuccess($"You add fuel to the existing {recipe.LocationFeatureResult.FeatureName}.");
                            break;
                        }
                    }

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

        // Phase 4: Tool Progression
        CreateBasicToolRecipes();

        // Phase 5: Shelter Progression
        CreateShelterRecipes();

        // Phase 6/8: Clothing/Armor Progression
        CreateClothingRecipes();

        // Location Features
        CreateLocationFeatureRecipes();

        // Cooking Recipes
        CreateCookingRecipes();

        // MVP Hunting System: Ranged Weapons
        CreateHuntingRecipes();
    }

    // Phase 3: Fire-Making Tool Recipes (100% crafting success, tools are used with skill checks)
    private void CreateFireRecipes()
    {
        // Hand Drill - Primitive friction fire tool (100% crafting success)
        var handDrill = new RecipeBuilder()
            .Named("Hand Drill")
            .WithDescription("Craft a simple friction fire starter. The tool can be used multiple times to attempt fire-making.")
            .RequiringCraftingTime(20)
            .WithPropertyRequirement(ItemProperty.Wood, 0.5)  // Dry stick
            // NO BaseSuccessChance = 100% crafting success
            .ResultingInItem(ItemFactory.MakeHandDrill)
            .Build();
        _recipes.Add("hand_drill", handDrill);

        // Bow Drill - Improved friction fire tool (100% crafting success)
        var bowDrill = new RecipeBuilder()
            .Named("Bow Drill")
            .WithDescription("Craft an improved friction fire starter using a bow mechanism. More reliable than a hand drill.")
            .RequiringCraftingTime(45)
            .WithPropertyRequirement(ItemProperty.Wood, 1.0)  // Wood for bow + drill
            .WithPropertyRequirement(ItemProperty.Binding, 0.1)  // Sinew or plant fiber
            // NO skill requirement - skill check happens in StartFire action
            .ResultingInItem(ItemFactory.MakeBowDrill)
            .Build();
        _recipes.Add("bow_drill", bowDrill);

        // Flint & Steel - Reliable fire tool (100% crafting success)
        var flintSteel = new RecipeBuilder()
            .Named("Flint and Steel")
            .WithDescription("Craft a reliable fire-starting tool from flint and steel. Very durable and effective.")
            .RequiringCraftingTime(5)
            .WithPropertyRequirement(ItemProperty.Flint, 0.2)  // Flint
            .WithPropertyRequirement(ItemProperty.Stone, 0.3)  // Striker stone
            // NO BaseSuccessChance = 100% crafting success
            .ResultingInItem(ItemFactory.MakeFlintAndSteel)
            .Build();
        _recipes.Add("flint_steel", flintSteel);
    }

    // Phase 4: Tool Progression - Knives, Spears, Clubs, Hand Axes
    private void CreateBasicToolRecipes()
    {
        // ===== KNIFE PROGRESSION (Tier 1-4) =====

        // Tier 1: Sharp Rock - Smash 2 stones together (day 1 tool)
        var sharpRock = new RecipeBuilder()
            .Named("Sharp Rock")
            .WithDescription("Crude cutting tool made by smashing stones together.")
            .RequiringCraftingTime(5)
            .WithPropertyRequirement(ItemProperty.Stone, 0.6) // 2 river stones
            .ResultingInItem(ItemFactory.MakeSharpRock)
            .Build();
        _recipes.Add("sharp_rock", sharpRock);

        // Tier 2: Flint Knife - Requires exploration for flint
        var flintKnife = new RecipeBuilder()
            .Named("Flint Knife")
            .WithDescription("Sharp flint blade lashed to a wooden handle.")
            .RequiringSkill("Crafting", 1)
            .RequiringCraftingTime(20)
            .WithPropertyRequirement(ItemProperty.Flint, 0.2)
            .WithPropertyRequirement(ItemProperty.Wood, 0.3)
            .WithPropertyRequirement(ItemProperty.Binding, 0.1)
            .ResultingInItem(ItemFactory.MakeFlintKnife)
            .Build();
        _recipes.Add("flint_knife", flintKnife);

        // Tier 3: Bone Knife - Requires hunting and fire
        var boneKnife = new RecipeBuilder()
            .Named("Bone Knife")
            .WithDescription("Fire-hardened bone blade with excellent edge retention.")
            .RequiringSkill("Crafting", 2)
            .RequiringCraftingTime(45)
            .WithPropertyRequirement(ItemProperty.Bone, 0.5)
            .WithPropertyRequirement(ItemProperty.Binding, 0.1)
            .WithPropertyRequirement(ItemProperty.Charcoal, 0.1) // For fire-hardening
            .ResultingInItem(ItemFactory.MakeBoneKnife)
            .Build();
        _recipes.Add("bone_knife", boneKnife);

        // Tier 4: Obsidian Blade - Rare endgame tool
        var obsidianBlade = new RecipeBuilder()
            .Named("Obsidian Blade")
            .WithDescription("Volcanic glass blade on an antler handle - the finest Ice Age cutting tool.")
            .RequiringSkill("Crafting", 3)
            .RequiringCraftingTime(60)
            .WithPropertyRequirement(ItemProperty.Obsidian, 0.2)
            .WithPropertyRequirement(ItemProperty.Antler, 0.3)
            .WithPropertyRequirement(ItemProperty.Binding, 0.1)
            .ResultingInItem(ItemFactory.MakeObsidianBlade)
            .Build();
        _recipes.Add("obsidian_blade", obsidianBlade);

        // ===== SPEAR PROGRESSION (Tier 1-5) =====

        // Tier 1: Sharpened Stick - Just wood (day 1 weapon)
        var sharpenedStick = new RecipeBuilder()
            .Named("Sharpened Stick")
            .WithDescription("Simple wooden stick sharpened to a point.")
            .RequiringCraftingTime(5)
            .WithPropertyRequirement(ItemProperty.Wood, 0.5)
            .ResultingInItem(ItemFactory.MakeSharpenedStick)
            .Build();
        _recipes.Add("sharpened_stick", sharpenedStick);

        // Tier 2: Fire-Hardened Spear - Requires fire
        var fireHardenedSpear = new RecipeBuilder()
            .Named("Fire-Hardened Spear")
            .WithDescription("Wooden spear with tip hardened in fire for durability.")
            .RequiringCraftingTime(20)
            .WithPropertyRequirement(ItemProperty.Wood, 0.6)
            .WithPropertyRequirement(ItemProperty.Charcoal, 0.05) // Need fire/charcoal to harden
            .ResultingInItem(ItemFactory.MakeFireHardenedSpear)
            .Build();
        _recipes.Add("fire_hardened_spear", fireHardenedSpear);

        // Tier 3: Flint-Tipped Spear - Proper hunting weapon
        var flintSpear = new RecipeBuilder()
            .Named("Flint-Tipped Spear")
            .WithDescription("Sturdy spear with sharp flint point - a proper hunting weapon.")
            .RequiringSkill("Crafting", 1)
            .RequiringCraftingTime(40)
            .WithPropertyRequirement(ItemProperty.Wood, 1.0)
            .WithPropertyRequirement(ItemProperty.Flint, 0.3)
            .WithPropertyRequirement(ItemProperty.Binding, 0.2)
            .ResultingInItem(ItemFactory.MakeFlintTippedSpear)
            .Build();
        _recipes.Add("flint_tipped_spear", flintSpear);

        // Tier 4: Bone-Tipped Spear - Excellent penetration
        var boneSpear = new RecipeBuilder()
            .Named("Bone-Tipped Spear")
            .WithDescription("Fire-hardened bone point on wooden shaft - excellent penetration.")
            .RequiringSkill("Crafting", 2)
            .RequiringCraftingTime(60)
            .WithPropertyRequirement(ItemProperty.Wood, 1.0)
            .WithPropertyRequirement(ItemProperty.Bone, 0.5)
            .WithPropertyRequirement(ItemProperty.Binding, 0.2)
            .WithPropertyRequirement(ItemProperty.Charcoal, 0.1)
            .ResultingInItem(ItemFactory.MakeBoneTippedSpear)
            .Build();
        _recipes.Add("bone_tipped_spear", boneSpear);

        // Tier 5: Obsidian Spear - Legendary hunting weapon
        var obsidianSpear = new RecipeBuilder()
            .Named("Obsidian Spear")
            .WithDescription("Razor-sharp obsidian point on balanced shaft - legendary among hunters.")
            .RequiringSkill("Crafting", 3)
            .RequiringCraftingTime(90)
            .WithPropertyRequirement(ItemProperty.Wood, 1.2)
            .WithPropertyRequirement(ItemProperty.Obsidian, 0.3)
            .WithPropertyRequirement(ItemProperty.Binding, 0.2)
            .ResultingInItem(ItemFactory.MakeObsidianSpear)
            .Build();
        _recipes.Add("obsidian_spear", obsidianSpear);

        // ===== CLUB PROGRESSION (Tier 1-3) =====

        // Tier 1: Heavy Stick - Found naturally (no recipe needed, but adding for completeness)
        // Note: Heavy Stick can be found, but we'll skip the recipe since it's just a thick branch

        // Tier 2: Stone-Weighted Club - Basic crafted club
        var stoneClub = new RecipeBuilder()
            .Named("Stone-Weighted Club")
            .WithDescription("Heavy stick with stone lashed to one end - devastating impact.")
            .RequiringCraftingTime(15)
            .WithPropertyRequirement(ItemProperty.Wood, 1.0)
            .WithPropertyRequirement(ItemProperty.Stone, 0.8)
            .WithPropertyRequirement(ItemProperty.Binding, 0.2)
            .ResultingInItem(ItemFactory.MakeStoneWeightedClub)
            .Build();
        _recipes.Add("stone_weighted_club", stoneClub);

        // Tier 3: Bone-Studded Club - Fearsome weapon
        var boneClub = new RecipeBuilder()
            .Named("Bone-Studded Club")
            .WithDescription("Club with sharpened bone spikes - crushes and pierces.")
            .RequiringSkill("Crafting", 1)
            .RequiringCraftingTime(30)
            .WithPropertyRequirement(ItemProperty.Wood, 1.2)
            .WithPropertyRequirement(ItemProperty.Bone, 0.8)
            .WithPropertyRequirement(ItemProperty.Binding, 0.3)
            .ResultingInItem(ItemFactory.MakeBoneStuddedClub)
            .Build();
        _recipes.Add("bone_studded_club", boneClub);

        // ===== HAND AXE PROGRESSION (Tier 1-2) =====

        // Tier 1: Stone Hand Axe - Basic utility tool
        var stoneAxe = new RecipeBuilder()
            .Named("Stone Hand Axe")
            .WithDescription("Stone blade bound to handle - useful for chopping wood.")
            .RequiringCraftingTime(25)
            .WithPropertyRequirement(ItemProperty.Stone, 0.8)
            .WithPropertyRequirement(ItemProperty.Wood, 0.5)
            .WithPropertyRequirement(ItemProperty.Binding, 0.2)
            .ResultingInItem(ItemFactory.MakeStoneHandAxe)
            .Build();
        _recipes.Add("stone_hand_axe", stoneAxe);

        // Tier 2: Flint Hand Axe - Superior wood harvesting
        var flintAxe = new RecipeBuilder()
            .Named("Flint Hand Axe")
            .WithDescription("Precision-knapped flint blade - superior wood harvesting.")
            .RequiringSkill("Crafting", 1)
            .RequiringCraftingTime(35)
            .WithPropertyRequirement(ItemProperty.Flint, 0.4)
            .WithPropertyRequirement(ItemProperty.Wood, 0.5)
            .WithPropertyRequirement(ItemProperty.Binding, 0.2)
            .ResultingInItem(ItemFactory.MakeFlintHandAxe)
            .Build();
        _recipes.Add("flint_hand_axe", flintAxe);
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
                var fireFeature = new HeatSourceFeature(location);
                var initialFuel = ItemFactory.MakeFirewood(); // 1.5kg softwood
                fireFeature.AddFuel(initialFuel, 0.8); // Add 0.8kg of fuel
                return fireFeature;
            }))
            .Build();
        _recipes.Add("campfire", campfire);
    }

    // Phase 5: Shelter Progression (Tier 1-4)
    private void CreateShelterRecipes()
    {
        // Tier 1: Windbreak - Emergency day-1 shelter (LocationFeature, not separate location)
        var windbreak = new RecipeBuilder()
            .Named("Windbreak")
            .WithDescription("Emergency shelter made from branches and grass. Minimal protection but quick to build.")
            .RequiringCraftingTime(30)
            .WithPropertyRequirement(ItemProperty.Wood, 3) // Branches
            .WithPropertyRequirement(ItemProperty.PlantFiber, 1) // Grass/plant matter
            .ResultingInLocationFeature(new LocationFeatureResult("Windbreak", location =>
            {
                // Creates a simple wind/weather barrier at current location
                return new EnvironmentFeature(location, 2.0, 0.2, 0.3); // +2°F, minimal coverage
            }))
            .Build();
        _recipes.Add("windbreak", windbreak);

        // Tier 2: Lean-to Shelter - Updated recipe
        var leanTo = new RecipeBuilder()
            .Named("Lean-to Shelter")
            .WithDescription("A simple angled shelter providing moderate protection from elements.")
            .RequiringSkill("Crafting", 1)
            .RequiringCraftingTime(120) // 2 hours
            .WithPropertyRequirement(ItemProperty.Wood, 6)
            .WithPropertyRequirement(ItemProperty.Binding, 1)
            .WithPropertyRequirement(ItemProperty.Insulation, 2, false) // Leaves, furs, etc.
            .ResultingInStructure("Lean-to Shelter", CreateLeanToShelter)
            .Build();
        _recipes.Add("lean_to", leanTo);

        // Tier 3: Debris Hut - Mid-tier permanent shelter
        var debrisHut = new RecipeBuilder()
            .Named("Debris Hut")
            .WithDescription("Well-insulated shelter covered with natural debris. Good protection and warmth.")
            .RequiringSkill("Crafting", 2)
            .RequiringCraftingTime(240) // 4 hours
            .WithPropertyRequirement(ItemProperty.Wood, 10)
            .WithPropertyRequirement(ItemProperty.Insulation, 3, false) // Leaves, bark, moss
            .WithPropertyRequirement(ItemProperty.Binding, 1)
            .ResultingInStructure("Debris Hut", CreateDebrisHut)
            .Build();
        _recipes.Add("debris_hut", debrisHut);

        // Tier 4: Log Cabin - Updated recipe (endgame shelter)
        var cabin = new RecipeBuilder()
            .Named("Log Cabin")
            .WithDescription("Sturdy log structure with excellent insulation. The finest Ice Age shelter.")
            .RequiringSkill("Crafting", 3)
            .RequiringCraftingTime(480) // 8 hours
            .WithPropertyRequirement(ItemProperty.Wood, 20)
            .WithPropertyRequirement(ItemProperty.Stone, 5) // For foundation/fireplace
            .WithPropertyRequirement(ItemProperty.Binding, 3)
            .WithPropertyRequirement(ItemProperty.Insulation, 8, false)
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

    // Phase 6/8: Clothing and Armor Recipes
    private void CreateClothingRecipes()
    {
        // ===== TIER 1: EARLY-GAME WRAPPINGS =====

        // Bark Chest Wrap - Day 1 upgrade from tattered rags
        var barkChest = new RecipeBuilder()
            .Named("Bark Chest Wrap")
            .WithDescription("Bark strips bound around torso. Crude but better than rags.")
            .RequiringCraftingTime(15)
            .WithPropertyRequirement(ItemProperty.Wood, 0.5) // Bark strips
            .WithPropertyRequirement(ItemProperty.Binding, 0.1)
            .ResultingInItem(ItemFactory.MakeBarkChestWrap)
            .Build();
        _recipes.Add("bark_chest_wrap", barkChest);

        // Bark Leg Wraps
        var barkLegs = new RecipeBuilder()
            .Named("Bark Leg Wraps")
            .WithDescription("Bark strips for leg protection.")
            .RequiringCraftingTime(15)
            .WithPropertyRequirement(ItemProperty.Wood, 0.4)
            .WithPropertyRequirement(ItemProperty.Binding, 0.1)
            .ResultingInItem(ItemFactory.MakeBarkLegWrap)
            .Build();
        _recipes.Add("bark_leg_wrap", barkLegs);

        // Grass Foot Wraps
        var grassFeet = new RecipeBuilder()
            .Named("Grass Foot Wraps")
            .WithDescription("Bundles of grass for minimal foot protection.")
            .RequiringCraftingTime(10)
            .WithPropertyRequirement(ItemProperty.PlantFiber, 0.3)
            .WithPropertyRequirement(ItemProperty.Binding, 0.05)
            .ResultingInItem(ItemFactory.MakeGrassFootWraps)
            .Build();
        _recipes.Add("grass_foot_wraps", grassFeet);

        // Plant Fiber Bindings
        var fiberHands = new RecipeBuilder()
            .Named("Plant Fiber Bindings")
            .WithDescription("Woven plant fibers for hand protection.")
            .RequiringCraftingTime(10)
            .WithPropertyRequirement(ItemProperty.PlantFiber, 0.2)
            .ResultingInItem(ItemFactory.MakePlantFiberBindings)
            .Build();
        _recipes.Add("plant_fiber_bindings", fiberHands);

        // ===== TIER 3: FUR-LINED ARMOR (requires hunting) =====

        // Fur-Lined Tunic - Best chest protection
        var furTunic = new RecipeBuilder()
            .Named("Fur-Lined Tunic")
            .WithDescription("Hide armor lined with thick fur. Best cold weather protection.")
            .RequiringSkill("Crafting", 2)
            .RequiringCraftingTime(120) // 2 hours
            .WithPropertyRequirement(ItemProperty.Hide, 2.0)
            .WithPropertyRequirement(ItemProperty.Fur, 1.0)
            .WithPropertyRequirement(ItemProperty.Binding, 0.5)
            .ResultingInItem(ItemFactory.MakeFurLinedTunic)
            .Build();
        _recipes.Add("fur_lined_tunic", furTunic);

        // Fur-Lined Leggings
        var furLegs = new RecipeBuilder()
            .Named("Fur-Lined Leggings")
            .WithDescription("Leather leggings with fur lining for superior warmth.")
            .RequiringSkill("Crafting", 2)
            .RequiringCraftingTime(90)
            .WithPropertyRequirement(ItemProperty.Hide, 1.5)
            .WithPropertyRequirement(ItemProperty.Fur, 0.8)
            .WithPropertyRequirement(ItemProperty.Binding, 0.3)
            .ResultingInItem(ItemFactory.MakeFurLinedLeggings)
            .Build();
        _recipes.Add("fur_lined_leggings", furLegs);

        // Fur-Lined Boots
        var furBoots = new RecipeBuilder()
            .Named("Fur-Lined Boots")
            .WithDescription("Sturdy boots with fur lining. Best foot protection.")
            .RequiringSkill("Crafting", 2)
            .RequiringCraftingTime(60)
            .WithPropertyRequirement(ItemProperty.Hide, 1.0)
            .WithPropertyRequirement(ItemProperty.Fur, 0.5)
            .WithPropertyRequirement(ItemProperty.Binding, 0.2)
            .ResultingInItem(ItemFactory.MakeFurLinedBoots)
            .Build();
        _recipes.Add("fur_lined_boots", furBoots);
    }

    // Phase 5: Shelter creation methods (Tier 2-4)

    private static Location CreateLeanToShelter(Zone parent)
    {
        var shelter = new Location("Lean-to Shelter", parent);
        // Tier 2: Moderate protection - angled roof blocks rain/wind from one direction
        shelter.Features.Add(new ShelterFeature(shelter, 0.35, 0.5, 0.5));
        shelter.Features.Add(new EnvironmentFeature(shelter, 5.0, 0.5, 0.5)); // +5°F, moderate weather block
        return shelter;
    }

    private static Location CreateDebrisHut(Zone parent)
    {
        var shelter = new Location("Debris Hut", parent);
        // Tier 3: Good protection - small enclosed space well-insulated with debris
        shelter.Features.Add(new ShelterFeature(shelter, 0.5, 0.7, 0.7));
        shelter.Features.Add(new EnvironmentFeature(shelter, 8.0, 0.7, 0.7)); // +8°F, good protection, dry
        return shelter;
    }

    private static Location CreateLogCabin(Zone parent)
    {
        var cabin = new Location("Log Cabin", parent);
        // Tier 4: Excellent protection - solid structure with fireplace
        cabin.Features.Add(new ShelterFeature(cabin, 0.8, 0.9, 0.9));
        cabin.Features.Add(new EnvironmentFeature(cabin, 15.0, 0.9, 0.9)); // +15°F, excellent protection
        return cabin;
    }

    // MVP Hunting System: Ranged Weapon Recipes
    private void CreateHuntingRecipes()
    {
        // Simple Bow - Basic hunting weapon
        var simpleBow = new RecipeBuilder()
            .Named("Simple Bow")
            .WithDescription("Craft a simple hunting bow from flexible wood and animal sinew. Requires Hunting skill 1.")
            .RequiringSkill("Hunting", 1)
            .RequiringCraftingTime(60) // 1 hour to craft a bow
            .WithPropertyRequirement(ItemProperty.Wood, 1.5) // Flexible wood for bow stave
            .WithPropertyRequirement(ItemProperty.Binding, 0.2) // Sinew or plant fiber for bowstring
            .ResultingInItem(ItemFactory.MakeSimpleBow)
            .Build();
        _recipes.Add("simple_bow", simpleBow);

        // Stone Arrow (Basic ammunition)
        var stoneArrow = new RecipeBuilder()
            .Named("Stone Arrow")
            .WithDescription("Craft a basic arrow with stone tip. Essential ammunition for bow hunting.")
            .RequiringCraftingTime(5) // 5 minutes per arrow
            .WithPropertyRequirement(ItemProperty.Wood, 0.1) // Shaft
            .WithPropertyRequirement(ItemProperty.Stone, 0.05) // Stone tip
            .ResultingInItem(ItemFactory.MakeStoneArrow)
            .Build();
        _recipes.Add("stone_arrow", stoneArrow);

        // Flint Arrow (Improved ammunition)
        var flintArrow = new RecipeBuilder()
            .Named("Flint Arrow")
            .WithDescription("Craft an arrow with sharp flint tip. More reliable and deadly than stone arrows.")
            .RequiringSkill("Hunting", 2)
            .RequiringCraftingTime(7) // 7 minutes per arrow
            .WithPropertyRequirement(ItemProperty.Wood, 0.1) // Shaft
            .WithPropertyRequirement(ItemProperty.Flint, 0.05) // Flint tip
            .ResultingInItem(ItemFactory.MakeFlintArrow)
            .Build();
        _recipes.Add("flint_arrow", flintArrow);

        // Bone Arrow (Advanced ammunition)
        var boneArrow = new RecipeBuilder()
            .Named("Bone Arrow")
            .WithDescription("Craft an arrow with fire-hardened bone tip. Excellent penetration against thick hides.")
            .RequiringSkill("Hunting", 3)
            .RequiringCraftingTime(8) // 8 minutes per arrow
            .WithPropertyRequirement(ItemProperty.Wood, 0.1) // Shaft
            .WithPropertyRequirement(ItemProperty.Bone, 0.08) // Bone tip
            .ResultingInItem(ItemFactory.MakeBoneArrow)
            .Build();
        _recipes.Add("bone_arrow", boneArrow);

        // Obsidian Arrow (Premium ammunition)
        var obsidianArrow = new RecipeBuilder()
            .Named("Obsidian Arrow")
            .WithDescription("Craft an arrow with razor-sharp obsidian tip. The finest hunting ammunition available.")
            .RequiringSkill("Hunting", 4)
            .RequiringCraftingTime(10) // 10 minutes per arrow
            .WithPropertyRequirement(ItemProperty.Wood, 0.1) // Shaft
            .WithPropertyRequirement(ItemProperty.Obsidian, 0.05) // Obsidian tip
            .ResultingInItem(ItemFactory.MakeObsidianArrow)
            .Build();
        _recipes.Add("obsidian_arrow", obsidianArrow);
    }
}
