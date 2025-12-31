using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.Items;

namespace text_survival.Web.Dto;

public record WebFrame(
    GameStateDto State,
    FrameMode Mode,
    List<Overlay> Overlays,
    InputRequestDto? Input,
    string? StatusText = null
);

public record ChoiceDto(string Id, string Label, bool IsDisabled = false);

public record InputRequestDto(
    int InputId,           // Unique ID for this input request
    string Type,           // "select", "confirm", "anykey"
    string Prompt,
    List<ChoiceDto>? Choices  // null for confirm/anykey
);

public record ResourceEntryDto(
    string Key,           // Resource enum name for identification
    string DisplayName,   // "pine logs", "birch bark", etc.
    int Count,
    double WeightKg,
    string? CssClass      // Optional CSS class for styling
);

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

public record TransferableItemDto(
    string Id,              // Unique ID for transfer action: "player_resource_Pine" or "storage_tool_0"
    string Category,        // "Fuel", "Food", "Materials", "Medicinals", "Tools", "Accessories", "Water"
    string DisplayName,     // "5 pine logs", "Flint Knife"
    string Icon,            // Material icon name: "local_fire_department", "restaurant", etc.
    double WeightKg,
    int Count,              // 1 for discrete items, N for aggregated resources
    bool IsAggregated       // True if this represents multiple stacks
);

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
                Count: (int)(source.WaterLiters / 1.0),
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

public record FuelItemDto(
    string Id,              // "fuel_Pine" or "fuel_Stick"
    string DisplayName,     // "5 pine logs"
    string Icon,            // Material icon name
    double WeightKg,
    int Count,
    bool CanAdd,            // false if fire too cold for this fuel
    string? DisabledReason, // "Fire too small for oak"
    int BurnTimeMinutes     // How long one unit burns
);

public record FireToolDto(
    string Id,              // "tool_0" (index in tools list)
    string DisplayName,     // "Hand Drill"
    string Icon,
    int SuccessPercent,     // Calculated base success chance
    bool IsSelected
);

public record TinderDto(
    string Id,              // "tinder_BirchBark"
    string DisplayName,     // "Birch Bark"
    string Icon,
    int BonusPercent,       // +25 for birch bark
    int Count,
    bool IsSelected
);

public record FirePanelDto(
    string Mode,            // "starting" or "tending"
    string Phase,           // "Cold", "Roaring", etc.
    string PhaseIcon,
    double TemperatureF,
    double HeatOutputF,
    double BurningKg,
    double UnburnedKg,
    double TotalKg,
    double MaxCapacityKg,
    int MinutesRemaining,
    double BurnRateKgPerHour,
    string Urgency,         // "safe", "caution", "warning", "critical"
    string PitType,
    double WindProtection,
    double FuelEfficiency,
    bool HasEmbers,
    double CharcoalKg,
    // Starting mode fields
    bool HasKindling,
    int FinalSuccessPercent
);

public record FireManagementDto(
    string Mode,                    // "starting" or "tending"
    List<FuelItemDto>? Fuels,       // Tending mode
    List<FireToolDto>? Tools,       // Starting mode
    List<TinderDto>? Tinders,       // Starting mode
    FirePanelDto Fire
)
{
    public static FireManagementDto FromContext(
        GameContext ctx,
        HeatSourceFeature fire,
        string? selectedToolId,
        string? selectedTinderId)
    {
        bool isStartingMode = !fire.IsActive && !fire.HasEmbers;
        string mode = isStartingMode ? "starting" : "tending";

        List<FuelItemDto>? fuels = null;
        List<FireToolDto>? tools = null;
        List<TinderDto>? tinders = null;

        if (isStartingMode)
        {
            tools = BuildToolList(ctx, selectedToolId);
            tinders = BuildTinderList(ctx, selectedTinderId);
        }
        else
        {
            fuels = BuildFuelList(ctx, fire);
        }

        var panel = BuildFirePanel(ctx, fire, mode, selectedToolId, selectedTinderId);

        return new FireManagementDto(mode, fuels, tools, tinders, panel);
    }

    private static List<FuelItemDto> BuildFuelList(GameContext ctx, HeatSourceFeature fire)
    {
        var fuels = new List<FuelItemDto>();
        var inv = ctx.Inventory;

        // Map resource types to fuel types
        var fuelMappings = new (Resource resource, FuelType fuel, string icon, string name)[]
        {
            (Resource.Stick, FuelType.Kindling, "horizontal_rule", "Sticks"),
            (Resource.Pine, FuelType.PineWood, "park", "Pine Logs"),
            (Resource.Birch, FuelType.BirchWood, "nature", "Birch Logs"),
            (Resource.Oak, FuelType.OakWood, "forest", "Oak Logs"),
            (Resource.Tinder, FuelType.Tinder, "grass", "Tinder"),
            (Resource.BirchBark, FuelType.BirchBark, "note", "Birch Bark"),
            (Resource.Usnea, FuelType.Usnea, "eco", "Usnea"),
            (Resource.Chaga, FuelType.Chaga, "spa", "Chaga"),
            (Resource.Charcoal, FuelType.Kindling, "whatshot", "Charcoal"),
            (Resource.Bone, FuelType.Bone, "pets", "Bone"),
        };

        foreach (var (resource, fuelType, icon, name) in fuelMappings)
        {
            int count = inv.Count(resource);
            if (count <= 0) continue;

            double weight = inv.Weight(resource);
            var props = FuelDatabase.Get(fuelType);
            bool canAdd = fire.CanAddFuel(fuelType);
            string? reason = null;

            if (!canAdd)
            {
                if (props.MinFireTemperature > 0)
                    reason = $"Fire too small (needs {props.MinFireTemperature:0}°F)";
                else if (fire.TotalMassKg >= fire.MaxFuelCapacityKg)
                    reason = "Fire at capacity";
            }

            // Calculate burn time for one unit (average weight / burn rate * 60)
            double avgWeight = weight / count;
            int burnTimeMinutes = (int)(avgWeight / props.BurnRateKgPerHour * 60);

            fuels.Add(new FuelItemDto(
                Id: $"fuel_{resource}",
                DisplayName: count > 1 ? $"{name} ({count})" : name,
                Icon: icon,
                WeightKg: weight,
                Count: count,
                CanAdd: canAdd,
                DisabledReason: reason,
                BurnTimeMinutes: burnTimeMinutes
            ));
        }

        return fuels;
    }

    private static List<FireToolDto> BuildToolList(GameContext ctx, string? selectedId)
    {
        var tools = new List<FireToolDto>();
        var fireTools = ctx.Inventory.Tools
            .Where(t => t.ToolType == ToolType.HandDrill ||
                       t.ToolType == ToolType.BowDrill ||
                       t.ToolType == ToolType.FireStriker)
            .ToList();

        for (int i = 0; i < fireTools.Count; i++)
        {
            var tool = fireTools[i];
            string id = $"tool_{i}";
            int baseChance = tool.ToolType switch
            {
                ToolType.HandDrill => 35,
                ToolType.BowDrill => 55,
                ToolType.FireStriker => 75,
                _ => 30
            };

            tools.Add(new FireToolDto(
                Id: id,
                DisplayName: tool.Name,
                Icon: "hardware",
                SuccessPercent: baseChance,
                IsSelected: id == selectedId || (selectedId == null && i == 0)
            ));
        }

        return tools;
    }

    private static List<TinderDto> BuildTinderList(GameContext ctx, string? selectedId)
    {
        var tinders = new List<TinderDto>();
        var inv = ctx.Inventory;

        // Generic tinder first, then specialty tinders by bonus (descending)
        var tinderTypes = new (Resource resource, FuelType fuel, string icon, string name)[]
        {
            (Resource.Tinder, FuelType.Tinder, "grass", "Tinder"),
            (Resource.BirchBark, FuelType.BirchBark, "note", "Birch Bark"),
            (Resource.Amadou, FuelType.Amadou, "spa", "Amadou"),
            (Resource.Usnea, FuelType.Usnea, "eco", "Usnea"),
            (Resource.Chaga, FuelType.Chaga, "spa", "Chaga"),
        };

        bool hasSelection = false;
        foreach (var (resource, fuelType, icon, name) in tinderTypes)
        {
            int count = inv.Count(resource);
            if (count <= 0) continue;

            var props = FuelDatabase.Get(fuelType);
            int bonus = (int)(props.IgnitionBonus * 100);
            string id = $"tinder_{resource}";
            bool isSelected = id == selectedId || (!hasSelection && selectedId == null);
            if (isSelected) hasSelection = true;

            tinders.Add(new TinderDto(
                Id: id,
                DisplayName: name,
                Icon: icon,
                BonusPercent: bonus,
                Count: count,
                IsSelected: isSelected
            ));
        }

        return tinders;
    }

    private static FirePanelDto BuildFirePanel(
        GameContext ctx,
        HeatSourceFeature fire,
        string mode,
        string? selectedToolId,
        string? selectedTinderId)
    {
        double temp = fire.GetCurrentFireTemperature();
        double heatOutput = fire.GetEffectiveHeatOutput(ctx.Weather.TemperatureInFahrenheit);
        string phase = fire.GetFirePhase();
        int minutesRemaining = (int)(fire.TotalHoursRemaining * 60);

        string urgency = minutesRemaining switch
        {
            >= 60 => "safe",
            >= 30 => "caution",
            >= 10 => "warning",
            _ => "critical"
        };

        string phaseIcon = phase switch
        {
            "Cold" => "ac_unit",
            "Embers" => "fireplace",
            "Igniting" => "local_fire_department",
            "Building" => "local_fire_department",
            "Steady" => "local_fire_department",
            "Roaring" => "whatshot",
            "Dying" => "whatshot",
            _ => "local_fire_department"
        };

        // Calculate success chance for starting mode
        int finalSuccess = 0;
        bool hasKindling = ctx.Inventory.Count(Resource.Stick) > 0;

        if (mode == "starting")
        {
            var tools = ctx.Inventory.Tools
                .Where(t => t.ToolType == ToolType.HandDrill ||
                           t.ToolType == ToolType.BowDrill ||
                           t.ToolType == ToolType.FireStriker)
                .ToList();
            int toolIndex = 0;
            if (selectedToolId != null && selectedToolId.StartsWith("tool_"))
                int.TryParse(selectedToolId[5..], out toolIndex);

            int baseChance = 30;
            if (toolIndex < tools.Count)
            {
                baseChance = tools[toolIndex].ToolType switch
                {
                    ToolType.HandDrill => 35,
                    ToolType.BowDrill => 55,
                    ToolType.FireStriker => 75,
                    _ => 30
                };
            }

            int tinderBonus = 0;
            if (selectedTinderId != null && selectedTinderId.StartsWith("tinder_"))
            {
                var resourceName = selectedTinderId[7..];
                if (Enum.TryParse<Resource>(resourceName, out var res))
                {
                    var fuelType = res switch
                    {
                        Resource.BirchBark => FuelType.BirchBark,
                        Resource.Usnea => FuelType.Usnea,
                        Resource.Chaga => FuelType.Chaga,
                        _ => FuelType.Tinder
                    };
                    tinderBonus = (int)(FuelDatabase.Get(fuelType).IgnitionBonus * 100);
                }
            }

            finalSuccess = Math.Min(95, baseChance + tinderBonus);
        }

        return new FirePanelDto(
            Mode: mode,
            Phase: phase,
            PhaseIcon: phaseIcon,
            TemperatureF: temp,
            HeatOutputF: heatOutput,
            BurningKg: fire.BurningMassKg,
            UnburnedKg: fire.UnburnedMassKg,
            TotalKg: fire.TotalMassKg,
            MaxCapacityKg: fire.MaxFuelCapacityKg,
            MinutesRemaining: minutesRemaining,
            BurnRateKgPerHour: fire.EffectiveBurnRateKgPerHour,
            Urgency: urgency,
            PitType: fire.PitType.ToString(),
            WindProtection: fire.WindProtectionFactor,
            FuelEfficiency: fire.FuelEfficiencyFactor,
            HasEmbers: fire.HasEmbers,
            CharcoalKg: fire.CharcoalAvailableKg,
            HasKindling: hasKindling,
            FinalSuccessPercent: finalSuccess
        );
    }
}

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

public record CategorySectionDto(
    string CategoryName,
    string CategoryKey,
    List<RecipeDto> CraftableRecipes,
    List<RecipeDto> UncraftableRecipes
);

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

public record MaterialRequirementDto(
    string MaterialName,
    int Required,
    int Available,
    bool IsMet
);

public record ToolRequirementDto(
    string ToolName,
    int Durability,
    bool IsAvailable,
    bool IsBroken
);

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
