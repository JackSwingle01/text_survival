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
}
