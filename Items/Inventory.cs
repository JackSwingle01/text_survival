namespace text_survival.Items;

/// <summary>
/// Aggregate-based inventory using List of weights for resources.
/// Used for both player inventory (with weight limit) and camp storage (unlimited).
/// </summary>
public class Inventory
{
    // Capacity (-1 = unlimited, e.g., camp storage)
    public double MaxWeightKg { get; set; } = -1;

    // Fire supplies - each entry is item weight in kg
    public List<double> Logs { get; set; } = [];      // ~2kg each, ~60min burn
    public List<double> Sticks { get; set; } = [];    // ~0.3kg each, ~10min burn
    public List<double> Tinder { get; set; } = [];    // ~0.05kg each, for fire starting

    // Food - each entry is portion weight in kg
    public List<double> CookedMeat { get; set; } = [];
    public List<double> RawMeat { get; set; } = [];
    public List<double> Berries { get; set; } = [];

    // Water in liters
    public double WaterLiters { get; set; }

    // Discrete items - identity matters
    public List<Tool> Tools { get; set; } = [];
    public List<Item> Special { get; set; } = [];  // Quest items, trophies

    // Equipment slots (5 armor + 1 weapon)
    public Equipment? Head { get; set; }
    public Equipment? Chest { get; set; }
    public Equipment? Legs { get; set; }
    public Equipment? Feet { get; set; }
    public Equipment? Hands { get; set; }
    public Tool? Weapon { get; set; }

    // Weight calculations
    public double FuelWeightKg => Logs.Sum() + Sticks.Sum() + Tinder.Sum();
    public double FoodWeightKg => CookedMeat.Sum() + RawMeat.Sum() + Berries.Sum();
    public double WaterWeightKg => WaterLiters;  // 1L water = 1kg
    public double ToolsWeightKg => Tools.Sum(t => t.Weight);
    public double SpecialWeightKg => Special.Sum(i => i.Weight);

    public double EquipmentWeightKg =>
        (Head?.Weight ?? 0) + (Chest?.Weight ?? 0) + (Legs?.Weight ?? 0) +
        (Feet?.Weight ?? 0) + (Hands?.Weight ?? 0) + (Weapon?.Weight ?? 0);

    public double CurrentWeightKg =>
        FuelWeightKg + FoodWeightKg + WaterWeightKg + ToolsWeightKg + SpecialWeightKg + EquipmentWeightKg;

    /// <summary>
    /// Total insulation from all worn equipment (0-1 scale per slot, summed).
    /// </summary>
    public double TotalInsulation =>
        (Head?.Insulation ?? 0) + (Chest?.Insulation ?? 0) + (Legs?.Insulation ?? 0) +
        (Feet?.Insulation ?? 0) + (Hands?.Insulation ?? 0);

    public bool CanCarry(double additionalKg) =>
        MaxWeightKg < 0 || CurrentWeightKg + additionalKg <= MaxWeightKg;

    public double RemainingCapacityKg =>
        MaxWeightKg < 0 ? double.MaxValue : Math.Max(0, MaxWeightKg - CurrentWeightKg);

    // Fuel burn time estimates (rough: logs ~30min/kg, sticks ~30min/kg, tinder ~10min/kg)
    public double TotalFuelBurnTimeMinutes =>
        Logs.Sum() * 30 + Sticks.Sum() * 30 + Tinder.Sum() * 10;

    public double TotalFuelBurnTimeHours => TotalFuelBurnTimeMinutes / 60;

    // Counts for display
    public int LogCount => Logs.Count;
    public int StickCount => Sticks.Count;
    public int TinderCount => Tinder.Count;
    public int CookedMeatCount => CookedMeat.Count;
    public int RawMeatCount => RawMeat.Count;
    public int BerryCount => Berries.Count;

    /// <summary>
    /// Add resources from foraging, harvesting, or other collection.
    /// </summary>
    public void Add(FoundResources resources)
    {
        Logs.AddRange(resources.Logs);
        Sticks.AddRange(resources.Sticks);
        Tinder.AddRange(resources.Tinder);
        CookedMeat.AddRange(resources.CookedMeat);
        RawMeat.AddRange(resources.RawMeat);
        Berries.AddRange(resources.Berries);
        WaterLiters += resources.WaterLiters;
        Tools.AddRange(resources.Tools);
        Special.AddRange(resources.Special);
    }

    /// <summary>
    /// Remove and return the smallest log (most efficient for fire).
    /// Returns 0 if no logs available.
    /// </summary>
    public double TakeSmallestLog()
    {
        if (Logs.Count == 0) return 0;
        var smallest = Logs.Min();
        Logs.Remove(smallest);
        return smallest;
    }

    /// <summary>
    /// Remove and return the smallest stick.
    /// Returns 0 if no sticks available.
    /// </summary>
    public double TakeSmallestStick()
    {
        if (Sticks.Count == 0) return 0;
        var smallest = Sticks.Min();
        Sticks.Remove(smallest);
        return smallest;
    }

    /// <summary>
    /// Remove and return one tinder piece.
    /// Returns 0 if no tinder available.
    /// </summary>
    public double TakeTinder()
    {
        if (Tinder.Count == 0) return 0;
        var piece = Tinder[0];
        Tinder.RemoveAt(0);
        return piece;
    }

    /// <summary>
    /// Check if there's enough fuel and tinder to start a fire.
    /// </summary>
    public bool CanStartFire => Tinder.Count > 0 && (Sticks.Count > 0 || Logs.Count > 0);

    /// <summary>
    /// Check if there's any food available.
    /// </summary>
    public bool HasFood => CookedMeat.Count > 0 || RawMeat.Count > 0 || Berries.Count > 0;

    /// <summary>
    /// Check if there's any water available.
    /// </summary>
    public bool HasWater => WaterLiters > 0;

    /// <summary>
    /// Check if there's any fuel available.
    /// </summary>
    public bool HasFuel => Logs.Count > 0 || Sticks.Count > 0;

    /// <summary>
    /// Equip armor/clothing to the appropriate slot.
    /// Returns the previously equipped item if any.
    /// </summary>
    public Equipment? Equip(Equipment equipment)
    {
        Equipment? previous = equipment.Slot switch
        {
            EquipSlot.Head => Head,
            EquipSlot.Chest => Chest,
            EquipSlot.Legs => Legs,
            EquipSlot.Feet => Feet,
            EquipSlot.Hands => Hands,
            _ => null
        };

        switch (equipment.Slot)
        {
            case EquipSlot.Head: Head = equipment; break;
            case EquipSlot.Chest: Chest = equipment; break;
            case EquipSlot.Legs: Legs = equipment; break;
            case EquipSlot.Feet: Feet = equipment; break;
            case EquipSlot.Hands: Hands = equipment; break;
        }

        return previous;
    }

    /// <summary>
    /// Unequip armor/clothing from a slot.
    /// Returns the unequipped item if any.
    /// </summary>
    public Equipment? Unequip(EquipSlot slot)
    {
        Equipment? removed = slot switch
        {
            EquipSlot.Head => Head,
            EquipSlot.Chest => Chest,
            EquipSlot.Legs => Legs,
            EquipSlot.Feet => Feet,
            EquipSlot.Hands => Hands,
            _ => null
        };

        switch (slot)
        {
            case EquipSlot.Head: Head = null; break;
            case EquipSlot.Chest: Chest = null; break;
            case EquipSlot.Legs: Legs = null; break;
            case EquipSlot.Feet: Feet = null; break;
            case EquipSlot.Hands: Hands = null; break;
        }

        return removed;
    }

    /// <summary>
    /// Equip a tool as weapon (must have combat stats).
    /// Returns the previously equipped weapon if any.
    /// </summary>
    public Tool? EquipWeapon(Tool weapon)
    {
        if (!weapon.IsWeapon)
            throw new InvalidOperationException($"Tool '{weapon.Name}' cannot be equipped as weapon (no combat stats)");

        var previous = Weapon;
        Weapon = weapon;
        return previous;
    }

    /// <summary>
    /// Unequip the current weapon.
    /// Returns the unequipped weapon if any.
    /// </summary>
    public Tool? UnequipWeapon()
    {
        var removed = Weapon;
        Weapon = null;
        return removed;
    }

    /// <summary>
    /// Create a player inventory with default carry capacity.
    /// </summary>
    public static Inventory CreatePlayerInventory(double maxWeightKg = 15.0) =>
        new() { MaxWeightKg = maxWeightKg };

    /// <summary>
    /// Create a camp storage with unlimited capacity.
    /// </summary>
    public static Inventory CreateCampStorage() =>
        new() { MaxWeightKg = -1 };
}
