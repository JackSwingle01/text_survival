using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Crafting;

/// <summary>
/// Need-based crafting system. Players select a need category,
/// then see what they can make from available materials.
/// </summary>
public class NeedCraftingSystem
{
    private readonly List<CraftOption> _options = [];

    public NeedCraftingSystem()
    {
        InitializeFireStartingOptions();
        InitializeCuttingToolOptions();
        InitializeHuntingWeaponOptions();
        InitializeTrappingOptions();
        InitializeProcessingOptions();
        InitializeTreatmentOptions();
        InitializeEquipmentOptions();
        InitializeLightingOptions();
    }

    /// <summary>
    /// Get all craft options for a need category.
    /// Returns options the player can craft OR has partial materials for.
    /// </summary>
    public List<CraftOption> GetOptionsForNeed(NeedCategory need, Inventory inventory)
    {
        return _options
            .Where(o => o.Category == need)
            .Where(o => o.CanCraft(inventory) || HasPartialMaterials(o, inventory))
            .OrderByDescending(o => o.CanCraft(inventory)) // Craftable first
            .ToList();
    }

    /// <summary>
    /// Get need categories that have at least one option available.
    /// </summary>
    public List<NeedCategory> GetAvailableNeeds(Inventory inventory)
    {
        return Enum.GetValues<NeedCategory>()
            .Where(need => GetOptionsForNeed(need, inventory).Any())
            .ToList();
    }

    /// <summary>
    /// Check if player has at least one material for this option.
    /// </summary>
    private static bool HasPartialMaterials(CraftOption option, Inventory inventory)
    {
        var (_, missing) = option.CheckRequirements(inventory);
        return missing.Count < option.Requirements.Count;
    }

    #region Fire-Starting Options

    private void InitializeFireStartingOptions()
    {
        // Hand Drill: 2 sticks
        _options.Add(new CraftOption
        {
            Name = "Hand Drill",
            Description = "A simple friction fire-starter. Rub a stick against another to create embers.",
            Category = NeedCategory.FireStarting,
            CraftingTimeMinutes = 15,
            Durability = 5,
            Requirements = [new MaterialRequirement("Sticks", 2)],
            ToolFactory = durability => new Tool("Hand Drill", ToolType.HandDrill, 0.2) { Durability = durability }
        });

        // Bow Drill: 3 sticks + 1 plant fiber
        _options.Add(new CraftOption
        {
            Name = "Bow Drill",
            Description = "An improved friction fire-starter. The bow makes spinning easier and faster.",
            Category = NeedCategory.FireStarting,
            CraftingTimeMinutes = 25,
            Durability = 15,
            Requirements = [new MaterialRequirement("Sticks", 3), new MaterialRequirement("PlantFiber", 1)],
            ToolFactory = durability => new Tool("Bow Drill", ToolType.BowDrill, 0.4) { Durability = durability }
        });

        // Strike-a-Light (Flint + Amadou): reliable sparks
        _options.Add(new CraftOption
        {
            Name = "Flint Striker",
            Description = "Strike flint against steel or stone to create sparks. Amadou catches and holds the ember.",
            Category = NeedCategory.FireStarting,
            CraftingTimeMinutes = 15,
            Durability = 20,
            Requirements = [
                new MaterialRequirement("Flint", 1),
                new MaterialRequirement("Amadou", 1)
            ],
            ToolFactory = durability => new Tool("Flint Striker", ToolType.FireStriker, 0.15) { Durability = durability }
        });

        // Pyrite Strike-a-Light: Flint + Pyrite (classic combination, very reliable)
        _options.Add(new CraftOption
        {
            Name = "Pyrite Strike-a-Light",
            Description = "Iron pyrite struck against flint creates hot sparks. The best fire-starting kit.",
            Category = NeedCategory.FireStarting,
            CraftingTimeMinutes = 10,
            Durability = 30,
            Requirements = [
                new MaterialRequirement("Flint", 1),
                new MaterialRequirement("Pyrite", 1)
            ],
            ToolFactory = durability => new Tool("Pyrite Strike-a-Light", ToolType.FireStriker, 0.2) { Durability = durability }
        });

        // Birch bark tinder bundle: improved tinder using amadou and birch bark
        _options.Add(new CraftOption
        {
            Name = "Tinder Bundle",
            Description = "A prepared bundle of birch bark and amadou. Catches sparks and holds an ember.",
            Category = NeedCategory.FireStarting,
            CraftingTimeMinutes = 5,
            Durability = 3,
            Requirements = [
                new MaterialRequirement("BirchBark", 1),
                new MaterialRequirement("Amadou", 1)
            ],
            ToolFactory = durability => new Tool("Tinder Bundle", ToolType.FireStriker, 0.05) { Durability = durability }
        });
    }

    #endregion

    #region Cutting Tool Options

    private void InitializeCuttingToolOptions()
    {
        // Sharp Rock: 2 stones (crude, low durability)
        _options.Add(new CraftOption
        {
            Name = "Sharp Rock",
            Description = "A crude cutting tool. Bash two rocks together to create a sharp edge.",
            Category = NeedCategory.CuttingTool,
            CraftingTimeMinutes = 5,
            Durability = 3,
            Requirements = [new MaterialRequirement("Stone", 2)],
            ToolFactory = durability => new Tool("Sharp Rock", ToolType.Knife, 0.4)
            {
                Durability = durability,
                Damage = 4,
                BlockChance = 0.01,
                WeaponClass = WeaponClass.Blade
            }
        });

        // Stone Knife: 1 stone + 1 stick + 1 plant fiber
        _options.Add(new CraftOption
        {
            Name = "Stone Knife",
            Description = "A proper knife with a handle. More durable and easier to use.",
            Category = NeedCategory.CuttingTool,
            CraftingTimeMinutes = 20,
            Durability = 10,
            Requirements = [
                new MaterialRequirement("Stone", 1),
                new MaterialRequirement("Sticks", 1),
                new MaterialRequirement("PlantFiber", 1)
            ],
            ToolFactory = durability => new Tool("Stone Knife", ToolType.Knife, 0.3)
            {
                Durability = durability,
                Damage = 6,
                BlockChance = 0.02,
                WeaponClass = WeaponClass.Blade
            }
        });

        // Bone Knife: 1 bone + 1 plant fiber
        _options.Add(new CraftOption
        {
            Name = "Bone Knife",
            Description = "A knife made from sharpened bone. Good edge retention.",
            Category = NeedCategory.CuttingTool,
            CraftingTimeMinutes = 15,
            Durability = 8,
            Requirements = [
                new MaterialRequirement("Bone", 1),
                new MaterialRequirement("PlantFiber", 1)
            ],
            ToolFactory = durability => new Tool("Bone Knife", ToolType.Knife, 0.25)
            {
                Durability = durability,
                Damage = 5,
                BlockChance = 0.02,
                WeaponClass = WeaponClass.Blade
            }
        });

        // Shale Knife: 1 shale + 1 stick + 1 plant fiber (easy to make, fragile)
        _options.Add(new CraftOption
        {
            Name = "Shale Knife",
            Description = "A knife made from shale. Easy to knap but fragile.",
            Category = NeedCategory.CuttingTool,
            CraftingTimeMinutes = 10,
            Durability = 4,
            Requirements = [
                new MaterialRequirement("Shale", 1),
                new MaterialRequirement("Sticks", 1),
                new MaterialRequirement("PlantFiber", 1)
            ],
            ToolFactory = durability => new Tool("Shale Knife", ToolType.Knife, 0.3)
            {
                Durability = durability,
                Damage = 5,
                BlockChance = 0.02,
                WeaponClass = WeaponClass.Blade
            }
        });

        // Flint Knife: 1 flint + 1 stick + 1 plant fiber (durable, better edge)
        _options.Add(new CraftOption
        {
            Name = "Flint Knife",
            Description = "A knife made from flint. Holds a razor-sharp edge and lasts longer.",
            Category = NeedCategory.CuttingTool,
            CraftingTimeMinutes = 25,
            Durability = 15,
            Requirements = [
                new MaterialRequirement("Flint", 1),
                new MaterialRequirement("Sticks", 1),
                new MaterialRequirement("PlantFiber", 1)
            ],
            ToolFactory = durability => new Tool("Flint Knife", ToolType.Knife, 0.3)
            {
                Durability = durability,
                Damage = 8,
                BlockChance = 0.03,
                WeaponClass = WeaponClass.Blade
            }
        });
    }

    #endregion

    #region Hunting Weapon Options

    private void InitializeHuntingWeaponOptions()
    {
        // Wooden Spear: 1 log OR 3 sticks
        _options.Add(new CraftOption
        {
            Name = "Wooden Spear",
            Description = "A sharpened wooden pole. Simple but effective for hunting.",
            Category = NeedCategory.HuntingWeapon,
            CraftingTimeMinutes = 15,
            Durability = 5,
            Requirements = [new MaterialRequirement("Sticks", 3)],
            ToolFactory = durability => new Tool("Wooden Spear", ToolType.Spear, 1.5)
            {
                Durability = durability,
                Damage = 7,
                BlockChance = 0.10,
                WeaponClass = WeaponClass.Pierce
            }
        });

        // Alternative: Log-based spear
        _options.Add(new CraftOption
        {
            Name = "Heavy Spear",
            Description = "A hefty spear carved from a log. More durable but heavier.",
            Category = NeedCategory.HuntingWeapon,
            CraftingTimeMinutes = 25,
            Durability = 8,
            Requirements = [new MaterialRequirement("Logs", 1)],
            ToolFactory = durability => new Tool("Heavy Spear", ToolType.Spear, 2.5)
            {
                Durability = durability,
                Damage = 9,
                BlockChance = 0.12,
                WeaponClass = WeaponClass.Pierce
            }
        });

        // Stone-Tipped Spear: 1 log + 1 stone + 1 plant fiber
        _options.Add(new CraftOption
        {
            Name = "Stone-Tipped Spear",
            Description = "A spear with a sharp stone point. Significantly more lethal.",
            Category = NeedCategory.HuntingWeapon,
            CraftingTimeMinutes = 35,
            Durability = 12,
            Requirements = [
                new MaterialRequirement("Logs", 1),
                new MaterialRequirement("Stone", 1),
                new MaterialRequirement("PlantFiber", 1)
            ],
            ToolFactory = durability => new Tool("Stone-Tipped Spear", ToolType.Spear, 2.0)
            {
                Durability = durability,
                Damage = 12,
                BlockChance = 0.12,
                WeaponClass = WeaponClass.Pierce
            }
        });
    }

    #endregion

    #region Trapping Options

    private void InitializeTrappingOptions()
    {
        // Simple Snare: 2 sticks + 2 plant fiber
        _options.Add(new CraftOption
        {
            Name = "Simple Snare",
            Description = "A basic loop trap for catching small game. Set it on an animal trail and wait.",
            Category = NeedCategory.Trapping,
            CraftingTimeMinutes = 10,
            Durability = 5,
            Requirements = [
                new MaterialRequirement("Sticks", 2),
                new MaterialRequirement("PlantFiber", 2)
            ],
            ToolFactory = durability => new Tool("Simple Snare", ToolType.Snare, 0.2) { Durability = durability }
        });

        // Reinforced Snare: 2 sticks + 1 sinew + 1 plant fiber
        _options.Add(new CraftOption
        {
            Name = "Reinforced Snare",
            Description = "A stronger snare using sinew cordage. Lasts longer and holds larger prey.",
            Category = NeedCategory.Trapping,
            CraftingTimeMinutes = 15,
            Durability = 10,
            Requirements = [
                new MaterialRequirement("Sticks", 2),
                new MaterialRequirement("Sinew", 1),
                new MaterialRequirement("PlantFiber", 1)
            ],
            ToolFactory = durability => new Tool("Reinforced Snare", ToolType.Snare, 0.25) { Durability = durability }
        });
    }

    #endregion

    #region Processing Options

    private void InitializeProcessingOptions()
    {
        // Scrape Hide: Raw hide → Scraped hide (requires cutting tool)
        _options.Add(new CraftOption
        {
            Name = "Scrape Hide",
            Description = "Scrape the fat and flesh from a hide. Prepares it for curing.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 30,
            Durability = 0, // Not a tool
            Requirements = [new MaterialRequirement("Hide", 1)],
            MaterialOutputs = [new MaterialOutput("ScrapedHide", 1, 0.8)] // Slightly lighter after scraping
        });

        // Render Fat: Raw fat → Tallow (requires fire)
        _options.Add(new CraftOption
        {
            Name = "Render Fat",
            Description = "Slowly heat animal fat over a fire to render it into tallow. Used for waterproofing and lamp fuel.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 20,
            Durability = 0,
            Requirements = [new MaterialRequirement("RawFat", 1)],
            MaterialOutputs = [new MaterialOutput("Tallow", 1, 0.15)]
        });

        // Process Fiber: Raw plant material → Usable cordage fiber
        _options.Add(new CraftOption
        {
            Name = "Process Fiber",
            Description = "Strip and twist plant fibers into usable cordage. Essential for binding and lashing.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 15,
            Durability = 0,
            Requirements = [new MaterialRequirement("RawFiber", 1)],
            MaterialOutputs = [new MaterialOutput("PlantFiber", 2, 0.05)] // Get 2 units of fiber per raw
        });

        // Curing Rack: Build a rack at camp for curing hides and drying food
        _options.Add(new CraftOption
        {
            Name = "Curing Rack",
            Description = "A wooden rack for curing hides and drying meat. Essential for leather-working and food preservation.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 45,
            Durability = 0,
            Requirements = [
                new MaterialRequirement("Logs", 2),
                new MaterialRequirement("Sticks", 4),
                new MaterialRequirement("PlantFiber", 2)
            ],
            FeatureFactory = () => new CuringRackFeature()
        });

        // Bone Shovel: Digging tool for camp improvements
        _options.Add(new CraftOption
        {
            Name = "Bone Shovel",
            Description = "A flat bone lashed to a sturdy stick. Speeds up digging for fire pits, snow shelters, and camp setup.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 20,
            Durability = 15,
            Requirements = [
                new MaterialRequirement("Bone", 2),
                new MaterialRequirement("Sticks", 1),
                new MaterialRequirement("PlantFiber", 1)
            ],
            ToolFactory = durability => new Tool("Bone Shovel", ToolType.Shovel, 1.2) { Durability = durability }
        });
    }

    #endregion

    #region Treatment Options

    private void InitializeTreatmentOptions()
    {
        // Willow Tea: Pain relief, reduces fever and inflammation
        _options.Add(new CraftOption
        {
            Name = "Willow Bark Tea",
            Description = "Bitter tea that eases pain and reduces fever. Natural aspirin.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 10,
            Durability = 1, // Single use
            Requirements = [new MaterialRequirement("WillowBark", 1)],
            ToolFactory = durability => new Tool("Willow Bark Tea", ToolType.Treatment, 0.3) { Durability = durability }
        });

        // Pine Needle Tea: Vitamin C, respiratory relief
        _options.Add(new CraftOption
        {
            Name = "Pine Needle Tea",
            Description = "Sharp-tasting tea rich in vitamins. Helps clear breathing.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 10,
            Durability = 1,
            Requirements = [new MaterialRequirement("PineNeedles", 1)],
            ToolFactory = durability => new Tool("Pine Needle Tea", ToolType.Treatment, 0.3) { Durability = durability }
        });

        // Rose Hip Tea: Vitamin C boost, immune support
        _options.Add(new CraftOption
        {
            Name = "Rose Hip Tea",
            Description = "Tangy red tea packed with vitamins. Strengthens the body against sickness.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 10,
            Durability = 1,
            Requirements = [new MaterialRequirement("RoseHips", 2)],
            ToolFactory = durability => new Tool("Rose Hip Tea", ToolType.Treatment, 0.3) { Durability = durability }
        });

        // Chaga Tea: Anti-inflammatory, general health
        _options.Add(new CraftOption
        {
            Name = "Chaga Tea",
            Description = "Dark, earthy tea from birch fungus. Reduces inflammation and aids healing.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 15,
            Durability = 1,
            Requirements = [new MaterialRequirement("Chaga", 1)],
            ToolFactory = durability => new Tool("Chaga Tea", ToolType.Treatment, 0.3) { Durability = durability }
        });

        // Polypore Poultice: External infection treatment
        _options.Add(new CraftOption
        {
            Name = "Polypore Poultice",
            Description = "A damp compress of birch polypore. Draws out infection from wounds.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 10,
            Durability = 1,
            Requirements = [new MaterialRequirement("BirchPolypore", 1)],
            ToolFactory = durability => new Tool("Polypore Poultice", ToolType.Treatment, 0.2) { Durability = durability }
        });

        // Usnea Dressing: Antimicrobial wound packing
        _options.Add(new CraftOption
        {
            Name = "Usnea Dressing",
            Description = "Old man's beard lichen prepared as wound packing. Naturally antimicrobial.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 5,
            Durability = 1,
            Requirements = [new MaterialRequirement("Usnea", 1)],
            ToolFactory = durability => new Tool("Usnea Dressing", ToolType.Treatment, 0.1) { Durability = durability }
        });

        // Sphagnum Bandage: Absorbent, antiseptic dressing
        _options.Add(new CraftOption
        {
            Name = "Sphagnum Bandage",
            Description = "Dried peat moss prepared as a bandage. Highly absorbent and naturally antiseptic.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 5,
            Durability = 1,
            Requirements = [new MaterialRequirement("Sphagnum", 2)],
            ToolFactory = durability => new Tool("Sphagnum Bandage", ToolType.Treatment, 0.15) { Durability = durability }
        });

        // Resin Seal: Wound sealing
        _options.Add(new CraftOption
        {
            Name = "Resin Seal",
            Description = "Pine resin warmed and applied to seal wounds. Waterproof and mildly antiseptic.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 5,
            Durability = 1,
            Requirements = [new MaterialRequirement("PineResin", 1)],
            ToolFactory = durability => new Tool("Resin Seal", ToolType.Treatment, 0.05) { Durability = durability }
        });
    }

    #endregion

    #region Equipment Options

    private void InitializeEquipmentOptions()
    {
        // Hide Gloves: Hand protection, cold resistance
        _options.Add(new CraftOption
        {
            Name = "Hide Gloves",
            Description = "Simple gloves sewn from cured hide. Protects hands from cold and injury.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 30,
            Durability = 0, // Equipment doesn't use durability
            Requirements = [
                new MaterialRequirement("CuredHide", 1),
                new MaterialRequirement("Sinew", 1)
            ],
            EquipmentFactory = () => new Equipment("Hide Gloves", EquipSlot.Hands, 0.3, 0.15)
        });

        // Hide Cap: Head protection
        _options.Add(new CraftOption
        {
            Name = "Hide Cap",
            Description = "A fitted cap of cured hide. Keeps your head warm.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 25,
            Durability = 0,
            Requirements = [
                new MaterialRequirement("CuredHide", 1),
                new MaterialRequirement("Sinew", 1)
            ],
            EquipmentFactory = () => new Equipment("Hide Cap", EquipSlot.Head, 0.25, 0.12)
        });

        // Hide Wrap: Chest protection (larger piece)
        _options.Add(new CraftOption
        {
            Name = "Hide Wrap",
            Description = "A large wrap of cured hide worn around the torso. Essential cold protection.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 45,
            Durability = 0,
            Requirements = [
                new MaterialRequirement("CuredHide", 2),
                new MaterialRequirement("Sinew", 2)
            ],
            EquipmentFactory = () => new Equipment("Hide Wrap", EquipSlot.Chest, 1.5, 0.25)
        });

        // Hide Leggings: Leg protection
        _options.Add(new CraftOption
        {
            Name = "Hide Leggings",
            Description = "Cured hide wraps for the legs. Protects against cold and brush.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 40,
            Durability = 0,
            Requirements = [
                new MaterialRequirement("CuredHide", 2),
                new MaterialRequirement("Sinew", 1)
            ],
            EquipmentFactory = () => new Equipment("Hide Leggings", EquipSlot.Legs, 1.0, 0.20)
        });

        // Hide Boots: Foot protection
        _options.Add(new CraftOption
        {
            Name = "Hide Boots",
            Description = "Sturdy boots of cured hide. Protects feet from cold and rough terrain.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 35,
            Durability = 0,
            Requirements = [
                new MaterialRequirement("CuredHide", 1),
                new MaterialRequirement("Sinew", 1)
            ],
            EquipmentFactory = () => new Equipment("Hide Boots", EquipSlot.Feet, 0.6, 0.18)
        });
    }

    #endregion

    #region Lighting Options

    private void InitializeLightingOptions()
    {
        // Simple Torch: 1 stick + 2 tinder
        _options.Add(new CraftOption
        {
            Name = "Simple Torch",
            Description = "A stick wrapped with tinder. Burns for about an hour, provides light and modest warmth.",
            Category = NeedCategory.Lighting,
            CraftingTimeMinutes = 5,
            Durability = 1,
            Requirements = [
                new MaterialRequirement("Sticks", 1),
                new MaterialRequirement("Tinder", 2)
            ],
            ToolFactory = durability => Tool.Torch("Simple Torch")
        });

        // Birch Bark Torch: 1 stick + 1 birch bark (better starting material)
        _options.Add(new CraftOption
        {
            Name = "Birch Bark Torch",
            Description = "A torch with oily birch bark. Catches fire easily and burns brightly.",
            Category = NeedCategory.Lighting,
            CraftingTimeMinutes = 5,
            Durability = 1,
            Requirements = [
                new MaterialRequirement("Sticks", 1),
                new MaterialRequirement("BirchBark", 1)
            ],
            ToolFactory = durability => Tool.Torch("Birch Bark Torch")
        });

        // Resin Torch: 1 stick + 1 tinder + 1 pine resin (longer burn, weatherproof)
        _options.Add(new CraftOption
        {
            Name = "Resin Torch",
            Description = "A torch coated with pine resin. Burns longer and resists wind and moisture.",
            Category = NeedCategory.Lighting,
            CraftingTimeMinutes = 10,
            Durability = 1,
            Requirements = [
                new MaterialRequirement("Sticks", 1),
                new MaterialRequirement("Tinder", 1),
                new MaterialRequirement("PineResin", 1)
            ],
            ToolFactory = durability => Tool.Torch("Resin Torch")
        });
    }

    #endregion
}
