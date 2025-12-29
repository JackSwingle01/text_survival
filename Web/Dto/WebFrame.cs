using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Web.Dto;

/// <summary>
/// A frame sent from server to client via WebSocket.
/// Uses explicit Mode + Overlay model for predictable UI state.
/// </summary>
public record WebFrame(
    GameStateDto State,
    FrameMode Mode,
    List<Overlay> Overlays,
    InputRequestDto? Input,
    string? StatusText = null
);

/// <summary>
/// A choice option with unique ID for reliable button identity.
/// </summary>
public record ChoiceDto(string Id, string Label, bool IsDisabled = false);

/// <summary>
/// Request for player input.
/// </summary>
public record InputRequestDto(
    int InputId,           // Unique ID for this input request
    string Type,           // "select", "confirm", "anykey"
    string Prompt,
    List<ChoiceDto>? Choices  // null for confirm/anykey
);

// ProgressDto removed - replaced by ProgressMode

/// <summary>
/// A single resource entry for inventory display.
/// </summary>
public record ResourceEntryDto(
    string Key,           // Resource enum name for identification
    string DisplayName,   // "pine logs", "birch bark", etc.
    int Count,
    double WeightKg,
    string? CssClass      // Optional CSS class for styling
);

/// <summary>
/// Full inventory data for inventory screen.
/// Uses lists instead of 100+ individual properties for maintainability.
/// </summary>
public record InventoryDto(
    string Title,
    double CurrentWeightKg,
    double MaxWeightKg,

    // Gear (structured - keep as-is)
    string? Weapon,
    double? WeaponDamage,
    List<EquipmentSlotDto> Armor,
    List<ToolDto> Tools,
    List<ToolWarningDto> ToolWarnings,
    double TotalInsulation,

    // Resources by category (new structure)
    List<ResourceEntryDto> Fuel,
    List<ResourceEntryDto> Food,
    double WaterLiters,
    List<ResourceEntryDto> Materials,
    List<ResourceEntryDto> Medicinals,

    // Summaries
    double FuelBurnTimeHours,
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

            // Resources by category
            Fuel: ExtractFuel(inv),
            Food: ExtractFood(inv),
            WaterLiters: inv.WaterLiters,
            Materials: ExtractMaterials(inv),
            Medicinals: ExtractMedicinals(inv),

            // Summaries
            FuelBurnTimeHours: inv.TorchBurnTimeRemainingMinutes / 60.0,
            GearSummary: GearSummaryHelper.ComputeGearSummary(inv)
        );
    }

    private static List<ResourceEntryDto> ExtractFuel(Inventory inv)
    {
        var items = new List<ResourceEntryDto>();

        // Generic fuel first
        AddIfPresent(items, inv, Resource.Stick, "sticks", null);
        AddIfPresent(items, inv, Resource.Tinder, "tinder", null);

        // Wood types
        AddIfPresent(items, inv, Resource.Pine, "pine logs", "wood-pine");
        AddIfPresent(items, inv, Resource.Birch, "birch logs", "wood-birch");
        AddIfPresent(items, inv, Resource.Oak, "oak logs", "wood-oak");
        AddIfPresent(items, inv, Resource.BirchBark, "birch bark", "tinder");

        return items;
    }

    private static List<ResourceEntryDto> ExtractFood(Inventory inv)
    {
        var items = new List<ResourceEntryDto>();

        // Cooked (best)
        AddIfPresent(items, inv, Resource.CookedMeat, "cooked meat", "food-cooked");

        // Preserved
        AddIfPresent(items, inv, Resource.DriedMeat, "dried meat", "food-preserved");
        AddIfPresent(items, inv, Resource.DriedBerries, "dried berries", "food-preserved");

        // Raw
        AddIfPresent(items, inv, Resource.RawMeat, "raw meat", "food-raw");

        // Foraged
        AddIfPresent(items, inv, Resource.Berries, "berries", "food-foraged");
        AddIfPresent(items, inv, Resource.Nuts, "nuts", "food-foraged");
        AddIfPresent(items, inv, Resource.Roots, "roots", "food-raw");

        return items;
    }

    private static List<ResourceEntryDto> ExtractMaterials(Inventory inv)
    {
        var items = new List<ResourceEntryDto>();

        // Stone types (highlight rare)
        AddIfPresent(items, inv, Resource.Stone, "stone", null);
        AddIfPresent(items, inv, Resource.Shale, "shale", "material-stone");
        AddIfPresent(items, inv, Resource.Flint, "flint", "material-rare");
        AddIfPresent(items, inv, Resource.Pyrite, "pyrite", "material-precious");

        // Organics
        AddIfPresent(items, inv, Resource.Bone, "bone", null);
        AddIfPresent(items, inv, Resource.Hide, "hide", null);
        AddIfPresent(items, inv, Resource.PlantFiber, "plant fiber", null);
        AddIfPresent(items, inv, Resource.Sinew, "sinew", null);

        // Processed
        AddIfPresent(items, inv, Resource.ScrapedHide, "scraped hide", "material-processed");
        AddIfPresent(items, inv, Resource.CuredHide, "cured hide", "material-processed");
        AddIfPresent(items, inv, Resource.RawFiber, "raw fiber", null);
        AddIfPresent(items, inv, Resource.RawFat, "raw fat", null);
        AddIfPresent(items, inv, Resource.Tallow, "tallow", "material-processed");
        AddIfPresent(items, inv, Resource.Charcoal, "charcoal", null);
        AddIfPresent(items, inv, Resource.Rope, "rope", "material-processed");

        return items;
    }

    private static List<ResourceEntryDto> ExtractMedicinals(Inventory inv)
    {
        var items = new List<ResourceEntryDto>();

        // Fungi
        AddIfPresent(items, inv, Resource.BirchPolypore, "birch polypore", "medicinal-wound");
        AddIfPresent(items, inv, Resource.Chaga, "chaga", "medicinal-health");
        AddIfPresent(items, inv, Resource.Amadou, "amadou", "medicinal-versatile");

        // Plants
        AddIfPresent(items, inv, Resource.RoseHip, "rose hips", "medicinal-vitamin");
        AddIfPresent(items, inv, Resource.JuniperBerry, "juniper berries", "medicinal-antiseptic");
        AddIfPresent(items, inv, Resource.WillowBark, "willow bark", "medicinal-pain");
        AddIfPresent(items, inv, Resource.PineNeedles, "pine needles", "medicinal-vitamin");

        // Tree products
        AddIfPresent(items, inv, Resource.PineResin, "pine resin", "medicinal-antiseptic");
        AddIfPresent(items, inv, Resource.Usnea, "usnea", "medicinal-wound");
        AddIfPresent(items, inv, Resource.SphagnumMoss, "sphagnum moss", "medicinal-wound");

        return items;
    }

    private static void AddIfPresent(List<ResourceEntryDto> items, Inventory inv, Resource resource, string displayName, string? cssClass)
    {
        int count = inv.Count(resource);
        if (count > 0)
        {
            items.Add(new ResourceEntryDto(
                Key: resource.ToString(),
                DisplayName: displayName,
                Count: count,
                WeightKg: inv.Weight(resource),
                CssClass: cssClass
            ));
        }
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
            t.ToolType.ToString()!
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
/// A transferable item for the transfer UI. Aggregates stacks of the same resource.
/// </summary>
public record TransferableItemDto(
    string Id,              // Unique ID for transfer action: "player_resource_Pine" or "storage_tool_0"
    string Category,        // "Fuel", "Food", "Materials", "Medicinals", "Tools", "Accessories", "Water"
    string DisplayName,     // "5 pine logs", "Flint Knife"
    string Icon,            // Material icon name: "local_fire_department", "restaurant", etc.
    double WeightKg,
    int Count,              // 1 for discrete items, N for aggregated resources
    bool IsAggregated       // True if this represents multiple stacks
);

/// <summary>
/// Transfer screen data showing both inventories side-by-side.
/// </summary>
public record TransferDto(
    string PlayerTitle,
    double PlayerCurrentWeightKg,
    double PlayerMaxWeightKg,
    List<TransferableItemDto> PlayerItems,
    string StorageTitle,
    double StorageCurrentWeightKg,
    double StorageMaxWeightKg,
    List<TransferableItemDto> StorageItems
)
{
    public static TransferDto FromInventories(
        Inventory player,
        Inventory storage,
        string storageName)
    {
        return new TransferDto(
            PlayerTitle: "CARRYING",
            PlayerCurrentWeightKg: player.CurrentWeightKg,
            PlayerMaxWeightKg: player.MaxWeightKg,
            PlayerItems: ExtractTransferableItems(player, "player"),

            StorageTitle: storageName.ToUpper(),
            StorageCurrentWeightKg: storage.CurrentWeightKg,
            StorageMaxWeightKg: storage.MaxWeightKg,
            StorageItems: ExtractTransferableItems(storage, "storage")
        );
    }

    private static List<TransferableItemDto> ExtractTransferableItems(Inventory source, string prefix)
    {
        var items = new List<TransferableItemDto>();

        // Aggregate resources by type
        foreach (Resource type in Enum.GetValues<Resource>())
        {
            int count = source.Count(type);
            if (count == 0) continue;

            double totalWeight = source.Weight(type);
            string category = GetTransferCategory(type);
            string icon = GetCategoryIcon(category);
            string displayName = count == 1
                ? type.ToDisplayName().ToLower()
                : $"{count} {type.ToDisplayName().ToLower()}s";

            items.Add(new TransferableItemDto(
                Id: $"{prefix}_resource_{type}",
                Category: category,
                DisplayName: displayName,
                Icon: icon,
                WeightKg: totalWeight,
                Count: count,
                IsAggregated: count > 1
            ));
        }

        // Water (aggregate as single entry)
        if (source.WaterLiters >= 0.1)
        {
            items.Add(new TransferableItemDto(
                Id: $"{prefix}_water",
                Category: "Water",
                DisplayName: $"{source.WaterLiters:F1}L water",
                Icon: "water_drop",
                WeightKg: source.WaterLiters,
                Count: (int)(source.WaterLiters / 0.5),
                IsAggregated: source.WaterLiters > 0.5
            ));
        }

        // Tools (discrete)
        for (int i = 0; i < source.Tools.Count; i++)
        {
            var tool = source.Tools[i];
            items.Add(new TransferableItemDto(
                Id: $"{prefix}_tool_{i}",
                Category: "Tools",
                DisplayName: tool.Name,
                Icon: "construction",
                WeightKg: tool.Weight,
                Count: 1,
                IsAggregated: false
            ));
        }

        // Accessories (discrete)
        for (int i = 0; i < source.Accessories.Count; i++)
        {
            var acc = source.Accessories[i];
            items.Add(new TransferableItemDto(
                Id: $"{prefix}_accessory_{i}",
                Category: "Carrying",
                DisplayName: $"{acc.Name} (+{acc.CapacityBonusKg}kg)",
                Icon: "backpack",
                WeightKg: acc.Weight,
                Count: 1,
                IsAggregated: false
            ));
        }

        return items;
    }

    private static string GetTransferCategory(Resource type)
    {
        if (ResourceCategories.Items[ResourceCategory.Fuel].Contains(type)) return "Fuel";
        if (ResourceCategories.Items[ResourceCategory.Food].Contains(type)) return "Food";
        if (ResourceCategories.Items[ResourceCategory.Medicine].Contains(type)) return "Medicinals";
        return "Materials";
    }

    private static string GetCategoryIcon(string category) => category switch
    {
        "Fuel" => "local_fire_department",
        "Food" => "restaurant",
        "Water" => "water_drop",
        "Materials" => "category",
        "Medicinals" => "healing",
        "Tools" => "construction",
        "Carrying" => "backpack",
        _ => "inventory_2"
    };
}

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
    public static CraftingDto FromContext(GameContext ctx, NeedCraftingSystem crafting, NeedCategory? filterCategory = null)
    {
        var categories = new List<CategorySectionDto>();

        var categoriesToShow = filterCategory.HasValue
            ? new[] { filterCategory.Value }
            : Enum.GetValues<NeedCategory>();

        foreach (var needCategory in categoriesToShow)
        {
            var options = crafting.GetOptionsForNeed(needCategory, ctx.Inventory, showAll: true);

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

    private static string GetCategoryDisplayName(NeedCategory category) =>
        NeedCategoryDisplay.GetDisplayName(category);
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
    string CraftingTimeDisplay,  // Formatted time display
    List<MaterialRequirementDto> Requirements,
    List<ToolRequirementDto> ToolRequirements,
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

        var toolRequirements = option.RequiredTools.Select(toolType =>
        {
            var tool = inventory.GetTool(toolType);
            return new ToolRequirementDto(
                ToolName: toolType.ToString(),
                Durability: tool?.Durability ?? 0,
                IsAvailable: tool != null,
                IsBroken: tool != null && tool.Durability < 1
            );
        }).ToList();

        string outputType = option.ProducesGear ? "Gear"
            : option.ProducesFeature ? "Feature"
            : "Material";

        // For multi-session projects, show total build time instead of setup time
        int displayTime = option.CraftingTimeMinutes;
        if (option.ProducesFeature && option.Name.Contains("(Project)"))
        {
            var tempFeature = option.FeatureFactory!();
            if (tempFeature is Environments.Features.CraftingProjectFeature project)
            {
                displayTime = (int)project.TimeRequiredMinutes;
            }
        }

        // Format time display: show hours if >= 60 minutes
        string timeDisplay = FormatCraftingTime(displayTime);

        return new RecipeDto(
            Name: option.Name,
            Description: option.Description,
            CraftingTimeMinutes: displayTime,
            CraftingTimeDisplay: timeDisplay,
            Requirements: requirements,
            ToolRequirements: toolRequirements,
            CanCraft: option.CanCraft(inventory),
            OutputType: outputType
        );
    }

    private static string FormatCraftingTime(int minutes)
    {
        if (minutes < 60)
            return $"{minutes} min";

        int hours = minutes / 60;
        int remainingMinutes = minutes % 60;

        if (remainingMinutes == 0)
            return $"{hours}h";

        return $"{hours}h {remainingMinutes}m";
    }

    private static int GetMaterialCount(Inventory inv, Crafting.MaterialSpecifier material) => material switch
    {
        Crafting.MaterialSpecifier.Specific(var resource) => inv.Count(resource),
        Crafting.MaterialSpecifier.Category(var category) => inv.GetCount(category),
        _ => 0
    };

    private static string FormatMaterialName(Crafting.MaterialSpecifier material) => material switch
    {
        Crafting.MaterialSpecifier.Specific(var r) => r.ToDisplayName().ToLower(),
        Crafting.MaterialSpecifier.Category(var c) => c.ToString().ToLower(),
        _ => "unknown"
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
/// Tool requirement for crafting.
/// </summary>
public record ToolRequirementDto(
    string ToolName,
    int Durability,
    bool IsAvailable,
    bool IsBroken
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
