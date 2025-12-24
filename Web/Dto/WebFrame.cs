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

            // Fuel - generic
            LogCount: inv.Logs.Count,
            LogsKg: inv.Logs.Sum(),
            StickCount: inv.Sticks.Count,
            SticksKg: inv.Sticks.Sum(),
            TinderCount: inv.Tinder.Count,
            TinderKg: inv.Tinder.Sum(),
            FuelBurnTimeHours: inv.TotalFuelBurnTimeHours,

            // Fuel - wood types
            PineCount: inv.Pine.Count,
            PineKg: inv.Pine.Sum(),
            BirchCount: inv.Birch.Count,
            BirchKg: inv.Birch.Sum(),
            OakCount: inv.Oak.Count,
            OakKg: inv.Oak.Sum(),
            BirchBarkCount: inv.BirchBark.Count,
            BirchBarkKg: inv.BirchBark.Sum(),

            // Food - cooked/preserved
            CookedMeatCount: inv.CookedMeat.Count,
            CookedMeatKg: inv.CookedMeat.Sum(),
            DriedMeatCount: inv.DriedMeat.Count,
            DriedMeatKg: inv.DriedMeat.Sum(),
            DriedBerriesCount: inv.DriedBerries.Count,
            DriedBerriesKg: inv.DriedBerries.Sum(),

            // Food - raw/foraged
            RawMeatCount: inv.RawMeat.Count,
            RawMeatKg: inv.RawMeat.Sum(),
            BerryCount: inv.Berries.Count,
            BerriesKg: inv.Berries.Sum(),
            NutsCount: inv.Nuts.Count,
            NutsKg: inv.Nuts.Sum(),
            RootsCount: inv.Roots.Count,
            RootsKg: inv.Roots.Sum(),

            // Water
            WaterLiters: inv.WaterLiters,

            // Materials - stones
            StoneCount: inv.Stone.Count,
            StoneKg: inv.Stone.Sum(),
            ShaleCount: inv.Shale.Count,
            ShaleKg: inv.Shale.Sum(),
            FlintCount: inv.Flint.Count,
            FlintKg: inv.Flint.Sum(),
            PyriteKg: inv.Pyrite,

            // Materials - organics
            BoneCount: inv.Bone.Count,
            BoneKg: inv.Bone.Sum(),
            HideCount: inv.Hide.Count,
            HideKg: inv.Hide.Sum(),
            PlantFiberCount: inv.PlantFiber.Count,
            PlantFiberKg: inv.PlantFiber.Sum(),
            SinewCount: inv.Sinew.Count,
            SinewKg: inv.Sinew.Sum(),

            // Materials - processed
            ScrapedHideCount: inv.ScrapedHide.Count,
            CuredHideCount: inv.CuredHide.Count,
            RawFiberCount: inv.RawFiber.Count,
            RawFatCount: inv.RawFat.Count,
            TallowCount: inv.Tallow.Count,
            CharcoalKg: inv.Charcoal,

            // Medicinals - fungi
            BirchPolyporeCount: inv.BirchPolypore.Count,
            ChagaCount: inv.Chaga.Count,
            AmadouCount: inv.Amadou.Count,

            // Medicinals - plants
            RoseHipsCount: inv.RoseHips.Count,
            JuniperBerriesCount: inv.JuniperBerries.Count,
            WillowBarkCount: inv.WillowBark.Count,
            PineNeedlesCount: inv.PineNeedles.Count,

            // Medicinals - tree products
            PineResinCount: inv.PineResin.Count,
            UsneaCount: inv.Usnea.Count,
            SphagnumCount: inv.Sphagnum.Count,

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
        int foodPortions = inv.CookedMeat.Count + inv.RawMeat.Count + inv.Berries.Count +
                          inv.Nuts.Count + inv.Roots.Count + inv.DriedMeat.Count + inv.DriedBerries.Count;

        // Water portions (~0.25L each)
        int waterPortions = (int)(inv.WaterLiters / 0.25);

        // Has preserved food
        bool hasPreserved = inv.DriedMeat.Count > 0 || inv.DriedBerries.Count > 0;

        // Total crafting materials
        int craftingCount = inv.Stone.Count + inv.Bone.Count + inv.Hide.Count +
                           inv.PlantFiber.Count + inv.Sinew.Count +
                           inv.Shale.Count + inv.Flint.Count + (inv.Pyrite > 0 ? 1 : 0) +
                           inv.ScrapedHide.Count + inv.CuredHide.Count;

        // Total medicinals
        int medicinalCount = inv.BirchPolypore.Count + inv.Chaga.Count + inv.Amadou.Count +
                            inv.RoseHips.Count + inv.JuniperBerries.Count + inv.WillowBark.Count +
                            inv.PineNeedles.Count + inv.PineResin.Count + inv.Usnea.Count + inv.Sphagnum.Count;

        // Has rare materials (flint or pyrite)
        bool hasRare = inv.Flint.Count > 0 || inv.Pyrite > 0;

        return new GearSummaryDto(
            WeaponName: inv.Weapon?.Name,
            WeaponDamage: inv.Weapon?.Damage,
            CuttingToolCount: cuttingCount,
            FireStarterCount: fireCount,
            OtherToolCount: otherCount,
            FoodPortions: foodPortions,
            WaterPortions: waterPortions,
            HasPreservedFood: hasPreserved,
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
    bool HasPreservedFood,
    int CraftingMaterialCount,
    int MedicinalCount,
    bool HasRareMaterials
);
