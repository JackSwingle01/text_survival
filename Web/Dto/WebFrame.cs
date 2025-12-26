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
    InventoryDto? Inventory = null
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
            t.Type.ToString()
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

        int cuttingCount = allTools.Count(t => t.Type == ToolType.Knife || t.Type == ToolType.Axe);
        int fireCount = allTools.Count(t => t.Type == ToolType.FireStriker || t.Type == ToolType.HandDrill || t.Type == ToolType.BowDrill);
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
