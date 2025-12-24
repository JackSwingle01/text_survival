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
    double TotalInsulation,
    // Fuel
    int LogCount,
    double LogsKg,
    int StickCount,
    double SticksKg,
    int TinderCount,
    double TinderKg,
    double FuelBurnTimeHours,
    // Food/Water
    int CookedMeatCount,
    double CookedMeatKg,
    int RawMeatCount,
    double RawMeatKg,
    int BerryCount,
    double BerriesKg,
    double WaterLiters,
    // Materials
    int StoneCount,
    double StoneKg,
    int BoneCount,
    double BoneKg,
    int HideCount,
    double HideKg,
    int PlantFiberCount,
    double PlantFiberKg,
    int SinewCount,
    double SinewKg
)
{
    public static InventoryDto FromInventory(Inventory inv, string title = "INVENTORY")
    {
        return new InventoryDto(
            Title: title,
            CurrentWeightKg: inv.CurrentWeightKg,
            MaxWeightKg: inv.MaxWeightKg,
            Weapon: inv.Weapon?.Name,
            WeaponDamage: inv.Weapon?.Damage,
            Armor: ExtractArmor(inv),
            Tools: inv.Tools.Select(t => new ToolDto(t.Name, t.IsWeapon ? t.Damage : null)).ToList(),
            TotalInsulation: inv.TotalInsulation,
            LogCount: inv.Logs.Count,
            LogsKg: inv.Logs.Sum(),
            StickCount: inv.Sticks.Count,
            SticksKg: inv.Sticks.Sum(),
            TinderCount: inv.Tinder.Count,
            TinderKg: inv.Tinder.Sum(),
            FuelBurnTimeHours: inv.TotalFuelBurnTimeHours,
            CookedMeatCount: inv.CookedMeat.Count,
            CookedMeatKg: inv.CookedMeat.Sum(),
            RawMeatCount: inv.RawMeat.Count,
            RawMeatKg: inv.RawMeat.Sum(),
            BerryCount: inv.Berries.Count,
            BerriesKg: inv.Berries.Sum(),
            WaterLiters: inv.WaterLiters,
            StoneCount: inv.Stone.Count,
            StoneKg: inv.Stone.Sum(),
            BoneCount: inv.Bone.Count,
            BoneKg: inv.Bone.Sum(),
            HideCount: inv.Hide.Count,
            HideKg: inv.Hide.Sum(),
            PlantFiberCount: inv.PlantFiber.Count,
            PlantFiberKg: inv.PlantFiber.Sum(),
            SinewCount: inv.Sinew.Count,
            SinewKg: inv.Sinew.Sum()
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
}

public record EquipmentSlotDto(string Slot, string Name, double Insulation);
public record ToolDto(string Name, double? Damage);
