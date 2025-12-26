using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Web.Dto;

/// <summary>
/// A frame sent from server to client via WebSocket.
/// Contains current game state and optionally an input request.
/// </summary>
public record WebFrame(
    GameStateDto State,
    InputRequestDto? Input,
    ProgressDto? Progress,
    string? StatusText = null,
    InventoryDto? Inventory = null,
    CraftingDto? Crafting = null
);

/// <summary>
/// Request for player input.
/// </summary>
public record InputRequestDto(
    string Type,           // "select", "confirm", "anykey"
    string Prompt,
    List<string>? Choices  // null for confirm/anykey
);

/// <summary>
/// Progress indicator for long operations.
/// </summary>
public record ProgressDto(int Current, int Total);

/// <summary>
/// Full inventory data for inventory screen.
/// </summary>
public record InventoryDto(
    string Title,
    double CurrentWeightKg,
    double MaxWeightKg,

    // Gear
    string? Weapon,
    double? WeaponDamage,
    List<EquipmentSlotDto> Armor,
    List<ToolDto> Tools,
    List<ToolWarningDto> ToolWarnings,
    double TotalInsulation,

    // Fuel - generic
    int LogCount,
    double LogsKg,
    int StickCount,
    double SticksKg,
    int TinderCount,
    double TinderKg,
    double FuelBurnTimeHours,

    // Fuel - wood types
    int PineCount,
    double PineKg,
    int BirchCount,
    double BirchKg,
    int OakCount,
    double OakKg,
    int BirchBarkCount,
    double BirchBarkKg,

    // Food - cooked/preserved
    int CookedMeatCount,
    double CookedMeatKg,
    int DriedMeatCount,
    double DriedMeatKg,
    int DriedBerriesCount,
    double DriedBerriesKg,

    // Food - raw/foraged
    int RawMeatCount,
    double RawMeatKg,
    int BerryCount,
    double BerriesKg,
    int NutsCount,
    double NutsKg,
    int RootsCount,
    double RootsKg,

    // Water
    double WaterLiters,

    // Materials - stones
    int StoneCount,
    double StoneKg,
    int ShaleCount,
    double ShaleKg,
    int FlintCount,
    double FlintKg,
    double PyriteKg,

    // Materials - organics
    int BoneCount,
    double BoneKg,
    int HideCount,
    double HideKg,
    int PlantFiberCount,
    double PlantFiberKg,
    int SinewCount,
    double SinewKg,

    // Materials - processed
    int ScrapedHideCount,
    int CuredHideCount,
    int RawFiberCount,
    int RawFatCount,
    int TallowCount,
    double CharcoalKg,

    // Medicinals - fungi
    int BirchPolyporeCount,
    int ChagaCount,
    int AmadouCount,

    // Medicinals - plants
    int RoseHipsCount,
    int JuniperBerriesCount,
    int WillowBarkCount,
    int PineNeedlesCount,

    // Medicinals - tree products
    int PineResinCount,
    int UsneaCount,
    int SphagnumCount,

    // Side panel summary
    GearSummaryDto? GearSummary
)
{
    public static InventoryDto FromInventory(Inventory inv, string title = "INVENTORY")
    {
        return new InventoryDto(
            Title: title,
            CurrentWeightKg: inv.CurrentWeightKg,
            MaxWeightKg: inv.MaxWeightKg,

            // Gear
            Weapon: inv.Weapon?.Name,
            WeaponDamage: inv.Weapon?.Damage,
            Armor: ExtractArmor(inv),
            Tools: ExtractTools(inv),
            ToolWarnings: ExtractToolWarnings(inv),
            TotalInsulation: inv.TotalInsulation,

            // Fuel - aggregate logs (Pine + Birch + Oak)
            LogCount: inv.LogCount,
            LogsKg: inv.LogWeight,
            StickCount: inv.Count(Resource.Stick),
            SticksKg: inv.Weight(Resource.Stick),
            TinderCount: inv.Count(Resource.Tinder),
            TinderKg: inv.Weight(Resource.Tinder),
            FuelBurnTimeHours: inv.TorchBurnTimeRemainingMinutes / 60.0,

            // Fuel - wood types
            PineCount: inv.Count(Resource.Pine),
            PineKg: inv.Weight(Resource.Pine),
            BirchCount: inv.Count(Resource.Birch),
            BirchKg: inv.Weight(Resource.Birch),
            OakCount: inv.Count(Resource.Oak),
            OakKg: inv.Weight(Resource.Oak),
            BirchBarkCount: inv.Count(Resource.BirchBark),
            BirchBarkKg: inv.Weight(Resource.BirchBark),

            // Food - cooked/preserved
            CookedMeatCount: inv.Count(Resource.CookedMeat),
            CookedMeatKg: inv.Weight(Resource.CookedMeat),
            DriedMeatCount: inv.Count(Resource.DriedMeat),
            DriedMeatKg: inv.Weight(Resource.DriedMeat),
            DriedBerriesCount: inv.Count(Resource.DriedBerries),
            DriedBerriesKg: inv.Weight(Resource.DriedBerries),
            // Food - raw/foraged
            RawMeatCount: inv.Count(Resource.RawMeat),
            RawMeatKg: inv.Weight(Resource.RawMeat),
            BerryCount: inv.Count(Resource.Berries),
            BerriesKg: inv.Weight(Resource.Berries),
            NutsCount: inv.Count(Resource.Nuts),
            NutsKg: inv.Weight(Resource.Nuts),
            RootsCount: inv.Count(Resource.Roots),
            RootsKg: inv.Weight(Resource.Roots),
            // Water
            WaterLiters: inv.WaterLiters,

            // Materials - stones
            StoneCount: inv.Count(Resource.Stone),
            StoneKg: inv.Weight(Resource.Stone),
            ShaleCount: inv.Count(Resource.Shale),
            ShaleKg: inv.Weight(Resource.Shale),
            FlintCount: inv.Count(Resource.Flint),
            FlintKg: inv.Weight(Resource.Flint),
            PyriteKg: inv.Weight(Resource.Pyrite),

            // Materials - organics
            BoneCount: inv.Count(Resource.Bone),
            BoneKg: inv.Weight(Resource.Bone),
            HideCount: inv.Count(Resource.Hide),
            HideKg: inv.Weight(Resource.Hide),
            PlantFiberCount: inv.Count(Resource.PlantFiber),
            PlantFiberKg: inv.Weight(Resource.PlantFiber),
            SinewCount: inv.Count(Resource.Sinew),
            SinewKg: inv.Weight(Resource.Sinew),

            // Materials - processed
            ScrapedHideCount: inv.Count(Resource.ScrapedHide),
            CuredHideCount: inv.Count(Resource.CuredHide),
            RawFiberCount: inv.Count(Resource.RawFiber),
            RawFatCount: inv.Count(Resource.RawFat),
            TallowCount: inv.Count(Resource.Tallow),
            CharcoalKg: inv.Weight(Resource.Charcoal),
            // Medicinals - fungi
            BirchPolyporeCount: inv.Count(Resource.BirchPolypore),
            ChagaCount: inv.Count(Resource.Chaga),
            AmadouCount: inv.Count(Resource.Amadou),

            // Medicinals - plants
            RoseHipsCount: inv.Count(Resource.RoseHip),
            JuniperBerriesCount: inv.Count(Resource.JuniperBerry),
            WillowBarkCount: inv.Count(Resource.WillowBark),
            PineNeedlesCount: inv.Count(Resource.PineNeedles),

            // Medicinals - tree products
            PineResinCount: inv.Count(Resource.PineResin),
            UsneaCount: inv.Count(Resource.Usnea),
            SphagnumCount: inv.Count(Resource.SphagnumMoss),

            // Side panel summary
            GearSummary: ComputeGearSummary(inv)
        );
    }

    private static List<EquipmentSlotDto> ExtractArmor(Inventory inv)
    {
        var slots = new List<EquipmentSlotDto>();
        if (inv.Head != null) slots.Add(new("Head", inv.Head.Name, inv.Head.Insulation));
        if (inv.Chest != null) slots.Add(new("Chest", inv.Chest.Name, inv.Chest.Insulation));
        if (inv.Legs != null) slots.Add(new("Legs", inv.Legs.Name, inv.Legs.Insulation));
        if (inv.Feet != null) slots.Add(new("Feet", inv.Feet.Name, inv.Feet.Insulation));
        if (inv.Hands != null) slots.Add(new("Hands", inv.Hands.Name, inv.Hands.Insulation));
        return slots;
    }

    private static List<ToolDto> ExtractTools(Inventory inv)
    {
        return inv.Tools.Select(t => new ToolDto(
            t.Name,
            t.IsWeapon ? t.Damage : null,
            t.ToolType.ToString()
        )).ToList();
    }

    private static List<ToolWarningDto> ExtractToolWarnings(Inventory inv)
    {
        var warnings = new List<ToolWarningDto>();

        // Check equipped weapon
        if (inv.Weapon != null && inv.Weapon.Durability > 0)
        {
            // Assume max durability based on typical values (we don't track max, so use heuristic)
            // Show warning when durability <= 2
            if (inv.Weapon.Durability <= 2)
            {
                warnings.Add(new ToolWarningDto(inv.Weapon.Name, inv.Weapon.Durability));
            }
        }

        // Check tools
        foreach (var tool in inv.Tools)
        {
            if (tool.Durability > 0 && tool.Durability <= 2)
            {
                warnings.Add(new ToolWarningDto(tool.Name, tool.Durability));
            }
        }

        return warnings;
    }

    private static GearSummaryDto ComputeGearSummary(Inventory inv)
    {
        // Count tools by category
        var allTools = inv.Tools.ToList();
        if (inv.Weapon != null) allTools.Add(inv.Weapon);

        int cuttingCount = allTools.Count(t => t.ToolType == ToolType.Knife || t.ToolType == ToolType.Axe);
        int fireCount = allTools.Count(t => t.ToolType == ToolType.FireStriker || t.ToolType == ToolType.HandDrill || t.ToolType == ToolType.BowDrill);
        int otherCount = allTools.Count - cuttingCount - fireCount;

        // Food portions (rough estimate - each portion ~0.15-0.3kg)
        int foodPortions = (int)(inv.GetWeight(ResourceCategory.Food) / 0.2);

        // Water portions (~0.25L each)
        int waterPortions = (int)(inv.WaterLiters / 0.25);

        // Total crafting materials
        int craftingCount = inv.GetCount(ResourceCategory.Material);

        // Total medicinals
        int medicinalCount = inv.GetCount(ResourceCategory.Medicine);

        // Has rare materials (flint or pyrite)
        bool hasRare = inv.Count(Resource.Flint) > 0 || inv.Count(Resource.Pyrite) > 0;

        return new GearSummaryDto(
            WeaponName: inv.Weapon?.Name,
            WeaponDamage: inv.Weapon?.Damage,
            CuttingToolCount: cuttingCount,
            FireStarterCount: fireCount,
            OtherToolCount: otherCount,
            FoodPortions: foodPortions,
            WaterPortions: waterPortions,
            CraftingMaterialCount: craftingCount,
            MedicinalCount: medicinalCount,
            HasRareMaterials: hasRare
        );
    }
}

public record EquipmentSlotDto(string Slot, string Name, double Insulation);
public record ToolDto(string Name, double? Damage, string Type);
public record ToolWarningDto(string Name, int DurabilityRemaining);

/// <summary>
/// Compact gear summary for side panel display.
/// </summary>
public record GearSummaryDto(
    string? WeaponName,
    double? WeaponDamage,
    int CuttingToolCount,
    int FireStarterCount,
    int OtherToolCount,
    int FoodPortions,
    int WaterPortions,
    int CraftingMaterialCount,
    int MedicinalCount,
    bool HasRareMaterials
);

/// <summary>
/// Full crafting data for crafting screen.
/// Organized by NeedCategory with craftable/uncraftable separation.
/// </summary>
public record CraftingDto(
    string Title,
    List<CategorySectionDto> Categories,
    MaterialInventoryDto PlayerMaterials
)
{
    public static CraftingDto FromContext(GameContext ctx, NeedCraftingSystem crafting)
    {
        var categories = new List<CategorySectionDto>();

        foreach (var needCategory in Enum.GetValues<NeedCategory>())
        {
            var options = crafting.GetOptionsForNeed(needCategory, ctx.Inventory);

            // Filter out already-built features
            options = options.Where(o => !IsFeatureAlreadyBuilt(o, ctx)).ToList();

            if (options.Count == 0) continue; // Skip empty categories

            var craftable = options.Where(o => o.CanCraft(ctx.Inventory))
                .Select(o => RecipeDto.FromCraftOption(o, ctx.Inventory))
                .ToList();

            var uncraftable = options.Where(o => !o.CanCraft(ctx.Inventory))
                .Select(o => RecipeDto.FromCraftOption(o, ctx.Inventory))
                .ToList();

            categories.Add(new CategorySectionDto(
                CategoryName: GetCategoryDisplayName(needCategory),
                CategoryKey: needCategory.ToString(),
                CraftableRecipes: craftable,
                UncraftableRecipes: uncraftable
            ));
        }

        return new CraftingDto(
            Title: "CRAFTING",
            Categories: categories,
            PlayerMaterials: MaterialInventoryDto.FromInventory(ctx.Inventory)
        );
    }

    private static bool IsFeatureAlreadyBuilt(CraftOption option, GameContext ctx)
    {
        if (!option.ProducesFeature) return false;

        if (option.Name == "Curing Rack")
            return ctx.Camp.GetFeature<CuringRackFeature>() != null;

        return false;
    }

    private static string GetCategoryDisplayName(NeedCategory category) => category switch
    {
        NeedCategory.FireStarting => "Fire-Starting",
        NeedCategory.CuttingTool => "Cutting Tools",
        NeedCategory.HuntingWeapon => "Hunting Weapons",
        NeedCategory.Trapping => "Trapping",
        NeedCategory.Processing => "Processing & Tools",
        NeedCategory.Treatment => "Medical Treatments",
        NeedCategory.Equipment => "Clothing & Gear",
        NeedCategory.Lighting => "Light Sources",
        NeedCategory.Carrying => "Carrying Gear",
        _ => category.ToString()
    };
}

/// <summary>
/// A category section with its recipes.
/// </summary>
public record CategorySectionDto(
    string CategoryName,
    string CategoryKey,
    List<RecipeDto> CraftableRecipes,
    List<RecipeDto> UncraftableRecipes
);

/// <summary>
/// Individual recipe with all display information.
/// </summary>
public record RecipeDto(
    string Name,
    string Description,
    int CraftingTimeMinutes,
    List<MaterialRequirementDto> Requirements,
    bool CanCraft,
    string OutputType  // "Gear", "Feature", "Material"
)
{
    public static RecipeDto FromCraftOption(CraftOption option, Inventory inventory)
    {
        var requirements = option.Requirements.Select(req =>
            new MaterialRequirementDto(
                MaterialName: FormatMaterialName(req.Material),
                Required: req.Count,
                Available: GetMaterialCount(inventory, req.Material),
                IsMet: GetMaterialCount(inventory, req.Material) >= req.Count
            )
        ).ToList();

        string outputType = option.ProducesGear ? "Gear"
            : option.ProducesFeature ? "Feature"
            : "Material";

        return new RecipeDto(
            Name: option.Name,
            Description: option.Description,
            CraftingTimeMinutes: option.CraftingTimeMinutes,
            Requirements: requirements,
            CanCraft: option.CanCraft(inventory),
            OutputType: outputType
        );
    }

    private static int GetMaterialCount(Inventory inv, string material)
    {
        if (Enum.TryParse<Resource>(material, out var resource))
            return inv.Count(resource);
        if (Enum.TryParse<ResourceCategory>(material, out var category))
            return inv.GetCount(category);
        return 0;
    }

    private static string FormatMaterialName(string material) => material switch
    {
        "Sticks" => "sticks",
        "Logs" => "logs",
        "Stone" => "stone",
        "Bone" => "bone",
        "Hide" => "hide",
        "PlantFiber" => "plant fiber",
        "Sinew" => "sinew",
        "BirchBark" => "birch bark",
        "Flint" => "flint",
        "Pyrite" => "pyrite",
        "Amadou" => "amadou",
        "Shale" => "shale",
        "Tinder" => "tinder",
        "CuredHide" => "cured hide",
        "ScrapedHide" => "scraped hide",
        "RawFiber" => "raw fiber",
        "RawFat" => "raw fat",
        "Rope" => "rope",
        "Tallow" => "tallow",
        "WillowBark" => "willow bark",
        "PineNeedles" => "pine needles",
        "RoseHip" => "rose hips",
        "Chaga" => "chaga",
        "BirchPolypore" => "birch polypore",
        "Usnea" => "usnea",
        "SphagnumMoss" => "sphagnum moss",
        "PineResin" => "pine resin",
        _ => material.ToLower()
    };
}

/// <summary>
/// Material requirement with availability status.
/// </summary>
public record MaterialRequirementDto(
    string MaterialName,
    int Required,
    int Available,
    bool IsMet
);

/// <summary>
/// Player's current material inventory (compact for context).
/// </summary>
public record MaterialInventoryDto(
    Dictionary<string, int> Materials
)
{
    public static MaterialInventoryDto FromInventory(Inventory inv)
    {
        var materials = new Dictionary<string, int>();

        // Add all resources with non-zero counts
        foreach (var resource in Enum.GetValues<Resource>())
        {
            int count = inv.Count(resource);
            if (count > 0)
                materials[resource.ToString()] = count;
        }

        return new MaterialInventoryDto(materials);
    }
}
