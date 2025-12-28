using text_survival.Environments.Features;
using text_survival.Items;
using static text_survival.Crafting.MaterialSpecifier;

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
        InitializeCarryingOptions();
        InitializeCampInfrastructureOptions();
        InitializeMendingOptions();
    }

    /// <summary>
    /// Get all craft options for a need category.
    /// Returns options the player can craft OR has partial materials for (unless showAll is true).
    /// </summary>
    public List<CraftOption> GetOptionsForNeed(NeedCategory need, Inventory inventory, bool showAll = false)
    {
        var options = _options.Where(o => o.Category == need);

        if (!showAll)
            options = options.Where(o => o.CanCraft(inventory) || HasPartialMaterials(o, inventory));

        return options
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
            Requirements = [new MaterialRequirement(Resource.Stick, 2)],
            RequiredTools = [ToolType.Knife],
            GearFactory = dur => new Gear
            {
                Name = "Hand Drill",
                Category = GearCategory.Tool,
                ToolType = ToolType.HandDrill,
                Weight = 0.2,
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Bow Drill: 3 sticks + 1 plant fiber
        _options.Add(new CraftOption
        {
            Name = "Bow Drill",
            Description = "An improved friction fire-starter. The bow makes spinning easier and faster.",
            Category = NeedCategory.FireStarting,
            CraftingTimeMinutes = 25,
            Durability = 15,
            Requirements = [new MaterialRequirement(Resource.Stick, 3), new MaterialRequirement(Resource.PlantFiber, 1)],
            RequiredTools = [ToolType.Knife],
            GearFactory = dur => new Gear
            {
                Name = "Bow Drill",
                Category = GearCategory.Tool,
                ToolType = ToolType.BowDrill,
                Weight = 0.4,
                Durability = dur,
                MaxDurability = dur
            }
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
                new MaterialRequirement(Resource.Flint, 1),
                new MaterialRequirement(Resource.Amadou, 1)
            ],
            GearFactory = dur => new Gear
            {
                Name = "Flint Striker",
                Category = GearCategory.Tool,
                ToolType = ToolType.FireStriker,
                Weight = 0.15,
                Durability = dur,
                MaxDurability = dur
            }
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
                new MaterialRequirement(Resource.Flint, 1),
                new MaterialRequirement(Resource.Pyrite, 1)
            ],
            GearFactory = dur => new Gear
            {
                Name = "Pyrite Strike-a-Light",
                Category = GearCategory.Tool,
                ToolType = ToolType.FireStriker,
                Weight = 0.2,
                Durability = dur,
                MaxDurability = dur
            }
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
                new MaterialRequirement(Resource.BirchBark, 1),
                new MaterialRequirement(Resource.Amadou, 1)
            ],
            GearFactory = dur => new Gear
            {
                Name = "Tinder Bundle",
                Category = GearCategory.Tool,
                ToolType = ToolType.FireStriker,
                Weight = 0.05,
                Durability = dur,
                MaxDurability = dur
            }
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
            Requirements = [new MaterialRequirement(Resource.Stone, 2)],
            GearFactory = dur => new Gear
            {
                Name = "Sharp Rock",
                Category = GearCategory.Tool,
                ToolType = ToolType.Knife,
                Weight = 0.4,
                Durability = dur,
                MaxDurability = dur,
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
                new MaterialRequirement(Resource.Stone, 1),
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.PlantFiber, 1)
            ],
            RequiredTools = [ToolType.KnappingStone],
            GearFactory = dur => new Gear
            {
                Name = "Stone Knife",
                Category = GearCategory.Tool,
                ToolType = ToolType.Knife,
                Weight = 0.3,
                Durability = dur,
                MaxDurability = dur,
                Damage = 6,
                BlockChance = 0.02,
                WeaponClass = WeaponClass.Blade
            }
        });

        // Bone Knife: 1 bone + 1 stick + 1 plant fiber
        _options.Add(new CraftOption
        {
            Name = "Bone Knife",
            Description = "A knife made from sharpened bone. Good edge retention.",
            Category = NeedCategory.CuttingTool,
            CraftingTimeMinutes = 15,
            Durability = 8,
            Requirements = [
                new MaterialRequirement(Resource.Bone, 1),
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.PlantFiber, 1)
            ],
            RequiredTools = [ToolType.KnappingStone],
            GearFactory = dur => new Gear
            {
                Name = "Bone Knife",
                Category = GearCategory.Tool,
                ToolType = ToolType.Knife,
                Weight = 0.25,
                Durability = dur,
                MaxDurability = dur,
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
                new MaterialRequirement(Resource.Shale, 1),
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.PlantFiber, 1)
            ],
            RequiredTools = [ToolType.KnappingStone],
            GearFactory = dur => new Gear
            {
                Name = "Shale Knife",
                Category = GearCategory.Tool,
                ToolType = ToolType.Knife,
                Weight = 0.3,
                Durability = dur,
                MaxDurability = dur,
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
                new MaterialRequirement(Resource.Flint, 1),
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.PlantFiber, 1)
            ],
            RequiredTools = [ToolType.KnappingStone],
            GearFactory = dur => new Gear
            {
                Name = "Flint Knife",
                Category = GearCategory.Tool,
                ToolType = ToolType.Knife,
                Weight = 0.3,
                Durability = dur,
                MaxDurability = dur,
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
            Requirements = [new MaterialRequirement(Resource.Stick, 3)],
            RequiredTools = [ToolType.Knife],
            GearFactory = dur => new Gear
            {
                Name = "Wooden Spear",
                Category = GearCategory.Tool,
                ToolType = ToolType.Spear,
                Weight = 1.5,
                Durability = dur,
                MaxDurability = dur,
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
            Requirements = [new MaterialRequirement(ResourceCategory.Log, 1)],
            RequiredTools = [ToolType.Knife],
            GearFactory = dur => new Gear
            {
                Name = "Heavy Spear",
                Category = GearCategory.Tool,
                ToolType = ToolType.Spear,
                Weight = 2.5,
                Durability = dur,
                MaxDurability = dur,
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
                new MaterialRequirement(ResourceCategory.Log, 1),
                new MaterialRequirement(Resource.Stone, 1),
                new MaterialRequirement(Resource.PlantFiber, 1)
            ],
            RequiredTools = [ToolType.Knife, ToolType.KnappingStone],
            GearFactory = dur => new Gear
            {
                Name = "Stone-Tipped Spear",
                Category = GearCategory.Tool,
                ToolType = ToolType.Spear,
                Weight = 2.0,
                Durability = dur,
                MaxDurability = dur,
                Damage = 12,
                BlockChance = 0.12,
                WeaponClass = WeaponClass.Pierce
            }
        });

        // Ivory-Tipped Spear: 1 ivory + 1 log + 2 sinew -> best spear
        // Trophy weapon from mammoth hunts
        _options.Add(new CraftOption
        {
            Name = "Ivory-Tipped Spear",
            Description = "A spear with a sharpened ivory point. Mammoth tusk is incredibly hard and holds an edge. Trophy weapon.",
            Category = NeedCategory.HuntingWeapon,
            CraftingTimeMinutes = 60,
            Durability = 25,  // Very durable ivory
            Requirements = [
                new MaterialRequirement(Resource.Ivory, 1),
                new MaterialRequirement(ResourceCategory.Log, 1),
                new MaterialRequirement(Resource.Sinew, 2)
            ],
            RequiredTools = [ToolType.Knife],
            GearFactory = dur => new Gear
            {
                Name = "Ivory-Tipped Spear",
                Category = GearCategory.Tool,
                ToolType = ToolType.Spear,
                Weight = 2.2,
                Durability = dur,
                MaxDurability = dur,
                Damage = 16,  // Best spear damage
                BlockChance = 0.15,
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
                new MaterialRequirement(Resource.Stick, 2),
                new MaterialRequirement(Resource.PlantFiber, 2)
            ],
            GearFactory = dur => new Gear
            {
                Name = "Simple Snare",
                Category = GearCategory.Tool,
                ToolType = ToolType.Snare,
                Weight = 0.2,
                Durability = dur,
                MaxDurability = dur
            }
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
                new MaterialRequirement(Resource.Stick, 2),
                new MaterialRequirement(Resource.Sinew, 1),
                new MaterialRequirement(Resource.PlantFiber, 1)
            ],
            GearFactory = dur => new Gear
            {
                Name = "Reinforced Snare",
                Category = GearCategory.Tool,
                ToolType = ToolType.Snare,
                Weight = 0.25,
                Durability = dur,
                MaxDurability = dur
            }
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
            Requirements = [new MaterialRequirement(Resource.Hide, 1)],
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
            Requirements = [new MaterialRequirement(Resource.RawFat, 1)],
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
            Requirements = [new MaterialRequirement(Resource.RawFiber, 1)],
            MaterialOutputs = [new MaterialOutput("PlantFiber", 2, 0.05)] // Get 2 units of fiber per raw
        });

        // Make Rope: 3 plant fiber → 1 rope
        _options.Add(new CraftOption
        {
            Name = "Rope",
            Description = "Twisted plant fiber cordage. Strong enough for carrying gear and lashing.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 15,
            Durability = 0,
            Requirements = [new MaterialRequirement(Resource.PlantFiber, 3)],
            MaterialOutputs = [new MaterialOutput("Rope", 1, 0.2)]
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
                new MaterialRequirement(Resource.Bone, 2),
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.PlantFiber, 1)
            ],
            RequiredTools = [ToolType.KnappingStone],
            GearFactory = dur => new Gear
            {
                Name = "Bone Shovel",
                Category = GearCategory.Tool,
                ToolType = ToolType.Shovel,
                Weight = 1.2,
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Stone Axe: Chopping tool for felling trees
        _options.Add(new CraftOption
        {
            Name = "Stone Axe",
            Description = "A heavy stone head lashed to a wooden handle. Required for felling standing trees.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 25,
            Durability = 12,
            Requirements = [
                new MaterialRequirement(Resource.Stone, 1),
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.PlantFiber, 1)
            ],
            RequiredTools = [ToolType.KnappingStone],
            GearFactory = dur => new Gear
            {
                Name = "Stone Axe",
                Category = GearCategory.Tool,
                ToolType = ToolType.Axe,
                Weight = 1.5,
                Durability = dur,
                MaxDurability = dur,
                Damage = 12,
                BlockChance = 0.05,
                WeaponClass = WeaponClass.Blade
            }
        });

        // Knapping Stone: Essential tool for shaping flint, shale, and bone
        _options.Add(new CraftOption
        {
            Name = "Knapping Stone",
            Description = "A hard stone used for knapping. Strike flint, shale, or bone to shape tools.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 10,
            Durability = 30,
            Requirements = [new MaterialRequirement(Resource.Stone, 3)],
            GearFactory = dur => new Gear
            {
                Name = "Knapping Stone",
                Category = GearCategory.Tool,
                ToolType = ToolType.KnappingStone,
                Weight = 1.0,
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Bone Needle: Essential for sewing equipment and mending
        _options.Add(new CraftOption
        {
            Name = "Bone Needle",
            Description = "A fine bone needle for stitching hide. Required for crafting and mending equipment.",
            Category = NeedCategory.Processing,
            CraftingTimeMinutes = 20,
            Durability = 20,
            Requirements = [new MaterialRequirement(Resource.Bone, 1)],
            RequiredTools = [ToolType.Knife],
            GearFactory = dur => Gear.BoneNeedle(durability: dur)
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
            Requirements = [new MaterialRequirement(Resource.WillowBark, 1)],
            GearFactory = dur => new Gear
            {
                Name = "Willow Bark Tea",
                Category = GearCategory.Tool,
                ToolType = ToolType.Treatment,
                Weight = 0.3,
                Durability = dur,
                MaxDurability = dur,
                TreatsEffect = "Pain",
                EffectReduction = 0.6,
                TreatmentDescription = "You drink the bitter tea. The ache begins to fade."
            }
        });

        // Pine Needle Tea: Vitamin C, respiratory relief
        _options.Add(new CraftOption
        {
            Name = "Pine Needle Tea",
            Description = "Sharp-tasting tea rich in vitamins. Helps clear breathing.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 10,
            Durability = 1,
            Requirements = [new MaterialRequirement(Resource.PineNeedles, 1)],
            GearFactory = dur => new Gear
            {
                Name = "Pine Needle Tea",
                Category = GearCategory.Tool,
                ToolType = ToolType.Treatment,
                Weight = 0.3,
                Durability = dur,
                MaxDurability = dur,
                TreatsEffect = "Coughing",
                EffectReduction = 0.5,
                TreatmentDescription = "You drink the sharp, resinous tea. Your breathing eases."
            }
        });

        // Rose Hip Tea: Vitamin C boost, immune support
        _options.Add(new CraftOption
        {
            Name = "Rose Hip Tea",
            Description = "Tangy red tea packed with vitamins. Strengthens the body against sickness.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 10,
            Durability = 1,
            Requirements = [new MaterialRequirement(Resource.RoseHip, 2)],
            GearFactory = dur => new Gear
            {
                Name = "Rose Hip Tea",
                Category = GearCategory.Tool,
                ToolType = ToolType.Treatment,
                Weight = 0.3,
                Durability = dur,
                MaxDurability = dur,
                TreatsEffect = "Pain",
                EffectReduction = 0.5,
                TreatmentDescription = "You drink the tangy, sweet tea. The pain dulls and warmth spreads through you.",
                GrantsEffect = "Nourished"
            }
        });

        // Chaga Tea: Anti-inflammatory, general health
        _options.Add(new CraftOption
        {
            Name = "Chaga Tea",
            Description = "Dark, earthy tea from birch fungus. Reduces inflammation and aids healing.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 15,
            Durability = 1,
            Requirements = [new MaterialRequirement(Resource.Chaga, 1)],
            GearFactory = dur => new Gear
            {
                Name = "Chaga Tea",
                Category = GearCategory.Tool,
                ToolType = ToolType.Treatment,
                Weight = 0.3,
                Durability = dur,
                MaxDurability = dur,
                TreatsEffect = "Fever",
                EffectReduction = 0.35,
                TreatmentDescription = "You drink the dark, earthy tea. The burning heat in your body begins to ease."
            }
        });

        // Polypore Poultice: External infection treatment
        _options.Add(new CraftOption
        {
            Name = "Polypore Poultice",
            Description = "A damp compress of birch polypore. Draws out infection from wounds.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 10,
            Durability = 1,
            Requirements = [new MaterialRequirement(Resource.BirchPolypore, 1)],
            GearFactory = dur => new Gear
            {
                Name = "Polypore Poultice",
                Category = GearCategory.Tool,
                ToolType = ToolType.Treatment,
                Weight = 0.2,
                Durability = dur,
                MaxDurability = dur,
                TreatsEffect = "Fever",
                EffectReduction = 0.3,
                TreatmentDescription = "You press the damp poultice against your skin. It draws out the heat."
            }
        });

        // Usnea Dressing: Antimicrobial wound packing
        _options.Add(new CraftOption
        {
            Name = "Usnea Dressing",
            Description = "Old man's beard lichen prepared as wound packing. Naturally antimicrobial.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 5,
            Durability = 1,
            Requirements = [new MaterialRequirement(Resource.Usnea, 1)],
            GearFactory = dur => new Gear
            {
                Name = "Usnea Dressing",
                Category = GearCategory.Tool,
                ToolType = ToolType.Treatment,
                Weight = 0.1,
                Durability = dur,
                MaxDurability = dur,
                TreatsEffect = "Bleeding",
                EffectReduction = 0.6,
                TreatmentDescription = "You pack the wound with usnea. The lichen absorbs the blood and seals the wound."
            }
        });

        // Sphagnum Bandage: Absorbent, antiseptic dressing
        _options.Add(new CraftOption
        {
            Name = "Sphagnum Bandage",
            Description = "Dried peat moss prepared as a bandage. Highly absorbent and naturally antiseptic.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 5,
            Durability = 1,
            Requirements = [new MaterialRequirement(Resource.SphagnumMoss, 2)],
            GearFactory = dur => new Gear
            {
                Name = "Sphagnum Bandage",
                Category = GearCategory.Tool,
                ToolType = ToolType.Treatment,
                Weight = 0.15,
                Durability = dur,
                MaxDurability = dur,
                TreatsEffect = "Bleeding",
                EffectReduction = 0.55,
                TreatmentDescription = "You wrap the wound with the sphagnum bandage. It absorbs the blood quickly."
            }
        });

        // Resin Seal: Wound sealing
        _options.Add(new CraftOption
        {
            Name = "Resin Seal",
            Description = "Pine resin warmed and applied to seal wounds. Waterproof and mildly antiseptic.",
            Category = NeedCategory.Treatment,
            CraftingTimeMinutes = 5,
            Durability = 1,
            Requirements = [new MaterialRequirement(Resource.PineResin, 1)],
            GearFactory = dur => new Gear
            {
                Name = "Resin Seal",
                Category = GearCategory.Tool,
                ToolType = ToolType.Treatment,
                Weight = 0.05,
                Durability = dur,
                MaxDurability = dur,
                TreatsEffect = "Bleeding",
                EffectReduction = 0.4,
                TreatmentDescription = "You apply the warm resin to the wound. It hardens into a protective seal."
            }
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
            Durability = 80,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 1),
                new MaterialRequirement(Resource.Sinew, 1)
            ],
            RequiredTools = [ToolType.Needle],
            GearFactory = dur => new Gear
            {
                Name = "Hide Gloves",
                Category = GearCategory.Equipment,
                Slot = EquipSlot.Hands,
                Weight = 0.3,
                BaseInsulation = 0.15,
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Hide Cap: Head protection
        _options.Add(new CraftOption
        {
            Name = "Hide Cap",
            Description = "A fitted cap of cured hide. Keeps your head warm.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 25,
            Durability = 80,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 1),
                new MaterialRequirement(Resource.Sinew, 1)
            ],
            RequiredTools = [ToolType.Needle],
            GearFactory = dur => new Gear
            {
                Name = "Hide Cap",
                Category = GearCategory.Equipment,
                Slot = EquipSlot.Head,
                Weight = 0.25,
                BaseInsulation = 0.12,
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Hide Wrap: Chest protection (larger piece)
        _options.Add(new CraftOption
        {
            Name = "Hide Wrap",
            Description = "A large wrap of cured hide worn around the torso. Essential cold protection.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 45,
            Durability = 80,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 2),
                new MaterialRequirement(Resource.Sinew, 2)
            ],
            RequiredTools = [ToolType.Needle],
            GearFactory = dur => new Gear
            {
                Name = "Hide Wrap",
                Category = GearCategory.Equipment,
                Slot = EquipSlot.Chest,
                Weight = 1.5,
                BaseInsulation = 0.25,
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Hide Leggings: Leg protection
        _options.Add(new CraftOption
        {
            Name = "Hide Leggings",
            Description = "Cured hide wraps for the legs. Protects against cold and brush.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 40,
            Durability = 80,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 2),
                new MaterialRequirement(Resource.Sinew, 1)
            ],
            RequiredTools = [ToolType.Needle],
            GearFactory = dur => new Gear
            {
                Name = "Hide Leggings",
                Category = GearCategory.Equipment,
                Slot = EquipSlot.Legs,
                Weight = 1.0,
                BaseInsulation = 0.20,
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Hide Boots: Foot protection
        _options.Add(new CraftOption
        {
            Name = "Hide Boots",
            Description = "Sturdy boots of cured hide. Protects feet from cold and rough terrain.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 35,
            Durability = 80,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 1),
                new MaterialRequirement(Resource.Sinew, 1)
            ],
            RequiredTools = [ToolType.Needle],
            GearFactory = dur => new Gear
            {
                Name = "Hide Boots",
                Category = GearCategory.Equipment,
                Slot = EquipSlot.Feet,
                Weight = 0.6,
                BaseInsulation = 0.18,
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Mammoth Hide Coat: Trophy equipment from megafauna hunts
        // Uses all 3 hides from one mammoth - the all-in warmth choice
        _options.Add(new CraftOption
        {
            Name = "Mammoth Hide Coat",
            Description = "A massive coat of woolly mammoth hide. The thick fur and dense leather block wind completely. Trophy gear.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 180,  // 3 hours - serious undertaking
            Durability = 40,
            Requirements = [
                new MaterialRequirement(Resource.MammothHide, 3),
                new MaterialRequirement(Resource.Sinew, 4),
                new MaterialRequirement(Resource.CuredHide, 2)  // For lining
            ],
            RequiredTools = [ToolType.Needle],
            GearFactory = dur => new Gear
            {
                Name = "Mammoth Hide Coat",
                Category = GearCategory.Equipment,
                Slot = EquipSlot.Chest,
                Weight = 4.0,
                BaseInsulation = 0.45,  // Best chest insulation in game
                Durability = dur,
                MaxDurability = dur
            }
        });

        // Mammoth Hood: Uses 1 hide, leaving 2 for utility items
        _options.Add(new CraftOption
        {
            Name = "Mammoth Hood",
            Description = "A hood of mammoth hide with thick fur lining. Covers head and neck completely.",
            Category = NeedCategory.Equipment,
            CraftingTimeMinutes = 90,
            Durability = 30,
            Requirements = [
                new MaterialRequirement(Resource.MammothHide, 1),
                new MaterialRequirement(Resource.Sinew, 2)
            ],
            RequiredTools = [ToolType.Needle],
            GearFactory = dur => new Gear
            {
                Name = "Mammoth Hood",
                Category = GearCategory.Equipment,
                Slot = EquipSlot.Head,
                Weight = 0.8,
                BaseInsulation = 0.22,  // Best head insulation
                Durability = dur,
                MaxDurability = dur
            }
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
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.Tinder, 2)
            ],
            GearFactory = dur => Gear.Torch("Simple Torch")
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
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.BirchBark, 1)
            ],
            GearFactory = dur => Gear.Torch("Birch Bark Torch")
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
                new MaterialRequirement(Resource.Stick, 1),
                new MaterialRequirement(Resource.Tinder, 1),
                new MaterialRequirement(Resource.PineResin, 1)
            ],
            GearFactory = dur => Gear.Torch("Resin Torch")
        });
    }

    #endregion

    #region Carrying Options

    private void InitializeCarryingOptions()
    {
        // Small Pouch: PlantFiber + BirchBark -> +0.5kg
        _options.Add(new CraftOption
        {
            Name = "Small Pouch",
            Description = "A simple pouch woven from plant fiber and birch bark. Clips to your belt.",
            Category = NeedCategory.Carrying,
            CraftingTimeMinutes = 15,
            Durability = 100,
            Requirements = [
                new MaterialRequirement(Resource.PlantFiber, 1),
                new MaterialRequirement(Resource.BirchBark, 1)
            ],
            GearFactory = dur => Gear.SmallPouch(dur)
        });

        // Rope Belt: 2 Rope -> +3kg
        _options.Add(new CraftOption
        {
            Name = "Rope Belt",
            Description = "A sturdy belt of woven rope. Lets you hang tools and pouches.",
            Category = NeedCategory.Carrying,
            CraftingTimeMinutes = 20,
            Durability = 100,
            Requirements = [new MaterialRequirement(Resource.Rope, 2)],
            GearFactory = dur => Gear.RopeBelt(dur)
        });

        // Proper Belt: CuredHide + Sinew -> +4kg
        _options.Add(new CraftOption
        {
            Name = "Proper Belt",
            Description = "A proper leather belt with loops and attachment points.",
            Category = NeedCategory.Carrying,
            CraftingTimeMinutes = 30,
            Durability = 150,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 1),
                new MaterialRequirement(Resource.Sinew, 1)
            ],
            GearFactory = dur => Gear.ProperBelt(dur)
        });

        // Large Bag: 3 CuredHide + 2 Sinew + 2 Rope -> +10kg
        _options.Add(new CraftOption
        {
            Name = "Large Bag",
            Description = "A large hide bag with rope shoulder straps. Serious carrying capacity.",
            Category = NeedCategory.Carrying,
            CraftingTimeMinutes = 60,
            Durability = 100,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 3),
                new MaterialRequirement(Resource.Sinew, 2),
                new MaterialRequirement(Resource.Rope, 2)
            ],
            GearFactory = dur => Gear.LargeBag(dur)
        });

        // Mammoth Hide Pack: 2 MammothHide + 2 Sinew + 1 Rope -> +15kg
        // Uses 2 of 3 mammoth hides - alternative to the coat
        _options.Add(new CraftOption
        {
            Name = "Mammoth Hide Pack",
            Description = "A massive pack of mammoth hide. The thick leather is nearly indestructible. Trophy gear.",
            Category = NeedCategory.Carrying,
            CraftingTimeMinutes = 120,
            Durability = 200,  // Very durable
            Requirements = [
                new MaterialRequirement(Resource.MammothHide, 2),
                new MaterialRequirement(Resource.Sinew, 2),
                new MaterialRequirement(Resource.Rope, 1)
            ],
            GearFactory = dur => new Gear
            {
                Name = "Mammoth Hide Pack",
                Category = GearCategory.Accessory,
                Weight = 2.0,
                CapacityBonusKg = 15.0,  // Best capacity in game
                Durability = dur,
                MaxDurability = dur
            }
        });
    }

    #endregion

    #region Camp Infrastructure Options

    private void InitializeCampInfrastructureOptions()
    {
        // 1. Padded Bedding (Instant Improvement)
        _options.Add(new CraftOption
        {
            Name = "Padded Bedding",
            Description = "A comfortable sleeping mat with plant fiber padding and a hide blanket. Better rest quality and ground insulation.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 45,
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.PlantFiber, 10),
                new MaterialRequirement(Resource.Hide, 1)
            ],
            FeatureFactory = () => BeddingFeature.CreatePaddedBedding()
        });

        // 2. Curing Rack (Instant Improvement)
        _options.Add(new CraftOption
        {
            Name = "Curing Rack",
            Description = "A wooden rack for curing hides and drying meat. Essential for leather-working and food preservation.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 45,
            Durability = 0,
            Requirements = [
                new MaterialRequirement(ResourceCategory.Log, 2),
                new MaterialRequirement(Resource.Stick, 4),
                new MaterialRequirement(Resource.PlantFiber, 2)
            ],
            FeatureFactory = () => new CuringRackFeature()
        });

        // 3. Mound Fire Pit (Multi-Session Project)
        _options.Add(new CraftOption
        {
            Name = "Mound Fire Pit (Project)",
            Description = "A shaped depression lined with stone. Provides wind protection and larger fuel capacity. Requires digging work (benefits from shovel).",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 15, // Setup time
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Stone, 15),
                new MaterialRequirement(Resource.Stick, 5)
            ],
            FeatureFactory = () => new FirePitUpgradeProject(
                "Mound Fire Pit",
                FirePitType.Mound,
                180 // 3 hours of work
            )
        });

        // 3. Stone Fire Pit (Multi-Session Project with Prerequisite)
        _options.Add(new CraftOption
        {
            Name = "Stone Fire Pit (Project)",
            Description = "A stone-lined pit with excellent wind protection and fuel efficiency. Requires digging work (benefits from shovel). Requires Mound Pit first.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 15, // Setup time
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Stone, 30),
                new MaterialRequirement(Resource.Stick, 10)
            ],
            Prerequisite = ctx => {
                var fire = ctx.Camp.GetFeature<HeatSourceFeature>();
                if (fire == null || fire.PitType != FirePitType.Mound)
                    return "Requires Mound Fire Pit first";
                return null;
            },
            FeatureFactory = () => new FirePitUpgradeProject(
                "Stone Fire Pit",
                FirePitType.Stone,
                300 // 5 hours of work
            )
        });

        // 4. Lean-to Shelter (Multi-Session Project)
        _options.Add(new CraftOption
        {
            Name = "Lean-to Shelter (Project)",
            Description = "A simple angled roof shelter. Good overhead protection from rain and snow. Takes several hours to build.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 15, // Setup time
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Stick, 15),
                new MaterialRequirement(ResourceCategory.Log, 10),
                new MaterialRequirement(Resource.PlantFiber, 5)
            ],
            Prerequisite = ctx => {
                if (ctx.Camp.HasFeature<ShelterFeature>())
                    return "Camp already has a shelter";
                return null;
            },
            FeatureFactory = () => new CraftingProjectFeature(
                "Lean-to Shelter",
                ShelterFeature.CreateLeanTo(),
                240 // 4 hours of work
            )
        });

        // 5. Snow Shelter (Multi-Session Project with Environmental Prerequisite)
        _options.Add(new CraftOption
        {
            Name = "Snow Shelter (Project)",
            Description = "A carved snow shelter with excellent insulation. Requires cold weather (below 32°F). Benefits from shovel. Melts in warm temperatures.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 15, // Setup time
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Stick, 5)
            ],
            Prerequisite = ctx => {
                if (ctx.Camp.HasFeature<ShelterFeature>())
                    return "Camp already has a shelter";
                // Check temperature - snow shelters need cold weather (below freezing)
                if (ctx.Weather.BaseTemperature > 0)
                    return "Too warm to build a snow shelter (requires below freezing)";
                return null;
            },
            FeatureFactory = () => new CraftingProjectFeature(
                "Snow Shelter",
                ShelterFeature.CreateSnowShelter(),
                120 // 2 hours of work
            ) { BenefitsFromShovel = true }
        });

        // 6. Portable Hide Tent - crafted item you can deploy anywhere
        _options.Add(new CraftOption
        {
            Name = "Hide Tent",
            Description = "A portable tent made from cured hides and a collapsible frame. Carry it with you and deploy shelter anywhere.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 180,  // 3 hours to craft
            Durability = 50,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 4),
                new MaterialRequirement(Resource.Stick, 4),
                new MaterialRequirement(Resource.Rope, 2)
            ],
            GearFactory = dur => Gear.HideTent(dur)
        });

        // 7. Mammoth Hide Tent - superior portable shelter from trophy materials
        _options.Add(new CraftOption
        {
            Name = "Mammoth Hide Tent",
            Description = "A heavy-duty tent of mammoth hide. Superior wind and cold protection. Trophy gear.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 240,  // 4 hours to craft
            Durability = 80,
            Requirements = [
                new MaterialRequirement(Resource.MammothHide, 2),
                new MaterialRequirement(Resource.Sinew, 4),
                new MaterialRequirement(Resource.Stick, 2)
            ],
            GearFactory = dur => Gear.MammothHideTent(dur)
        });

        // 8. Cabin (Multi-Session Project - Major Undertaking)
        _options.Add(new CraftOption
        {
            Name = "Cabin (Project)",
            Description = "A permanent log cabin with stone foundation. Best protection from elements. Major construction project requiring many hours of work. Benefits from shovel for foundation digging.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 30, // Setup time
            Durability = 0,
            Requirements = [
                new MaterialRequirement(ResourceCategory.Log, 80),
                new MaterialRequirement(Resource.Stone, 40),
                new MaterialRequirement(Resource.PlantFiber, 20),
                new MaterialRequirement(Resource.Rope, 10)
            ],
            Prerequisite = ctx => {
                if (ctx.Camp.HasFeature<ShelterFeature>())
                    return "Camp already has a shelter";
                return null;
            },
            FeatureFactory = () => new CraftingProjectFeature(
                "Cabin",
                ShelterFeature.CreateCabin(),
                1200 // 20 hours of work - serious undertaking!
            ) { BenefitsFromShovel = true }
        });

        // 8. Sleeping Bag (Multi-Session Project)
        _options.Add(new CraftOption
        {
            Name = "Sleeping Bag (Project)",
            Description = "Cured hides sewn together into an enclosed sleeping bag. Best bedding quality with warmth bonus. Requires several hours of stitching work.",
            Category = NeedCategory.CampInfrastructure,
            CraftingTimeMinutes = 15, // Setup time
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.CuredHide, 4),
                new MaterialRequirement(Resource.Sinew, 6)
            ],
            FeatureFactory = () => new CraftingProjectFeature(
                "Sleeping Bag",
                BeddingFeature.CreateSleepingBag(),
                180 // 3 hours of stitching work
            )
        });
    }

    #endregion

    #region Mending Options

    private void InitializeMendingOptions()
    {
        // Mend Boots
        _options.Add(new CraftOption
        {
            Name = "Mend Boots",
            Description = "Patch holes and restitch seams on your footwear.",
            Category = NeedCategory.Mending,
            CraftingTimeMinutes = 15,
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Sinew, 1),
                new MaterialRequirement(Resource.Hide, 1)
            ],
            RequiredTools = [ToolType.Needle],
            MendSlot = EquipSlot.Feet,
            Prerequisite = ctx =>
            {
                var boots = ctx.Inventory.GetEquipment(EquipSlot.Feet);
                if (boots == null) return "No boots equipped";
                if (boots.ConditionPct >= 1.0) return "Boots don't need mending";
                return null;
            }
        });

        // Mend Gloves
        _options.Add(new CraftOption
        {
            Name = "Mend Gloves",
            Description = "Patch worn spots and fix loose stitching on your gloves.",
            Category = NeedCategory.Mending,
            CraftingTimeMinutes = 15,
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Sinew, 1),
                new MaterialRequirement(Resource.Hide, 1)
            ],
            RequiredTools = [ToolType.Needle],
            MendSlot = EquipSlot.Hands,
            Prerequisite = ctx =>
            {
                var gloves = ctx.Inventory.GetEquipment(EquipSlot.Hands);
                if (gloves == null) return "No gloves equipped";
                if (gloves.ConditionPct >= 1.0) return "Gloves don't need mending";
                return null;
            }
        });

        // Mend Cap
        _options.Add(new CraftOption
        {
            Name = "Mend Cap",
            Description = "Patch and restitch your head covering.",
            Category = NeedCategory.Mending,
            CraftingTimeMinutes = 15,
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Sinew, 1),
                new MaterialRequirement(Resource.Hide, 1)
            ],
            RequiredTools = [ToolType.Needle],
            MendSlot = EquipSlot.Head,
            Prerequisite = ctx =>
            {
                var cap = ctx.Inventory.GetEquipment(EquipSlot.Head);
                if (cap == null) return "No head covering equipped";
                if (cap.ConditionPct >= 1.0) return "Head covering doesn't need mending";
                return null;
            }
        });

        // Mend Chest Wrap
        _options.Add(new CraftOption
        {
            Name = "Mend Chest Wrap",
            Description = "Patch tears and reinforce seams on your chest covering.",
            Category = NeedCategory.Mending,
            CraftingTimeMinutes = 20,
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Sinew, 1),
                new MaterialRequirement(Resource.Hide, 1)
            ],
            RequiredTools = [ToolType.Needle],
            MendSlot = EquipSlot.Chest,
            Prerequisite = ctx =>
            {
                var chest = ctx.Inventory.GetEquipment(EquipSlot.Chest);
                if (chest == null) return "No chest covering equipped";
                if (chest.ConditionPct >= 1.0) return "Chest covering doesn't need mending";
                return null;
            }
        });

        // Mend Leggings
        _options.Add(new CraftOption
        {
            Name = "Mend Leggings",
            Description = "Patch worn areas and fix loose stitching on your leg wraps.",
            Category = NeedCategory.Mending,
            CraftingTimeMinutes = 18,
            Durability = 0,
            Requirements = [
                new MaterialRequirement(Resource.Sinew, 1),
                new MaterialRequirement(Resource.Hide, 1)
            ],
            RequiredTools = [ToolType.Needle],
            MendSlot = EquipSlot.Legs,
            Prerequisite = ctx =>
            {
                var legs = ctx.Inventory.GetEquipment(EquipSlot.Legs);
                if (legs == null) return "No leg covering equipped";
                if (legs.ConditionPct >= 1.0) return "Leg covering doesn't need mending";
                return null;
            }
        });
    }

    #endregion
}
