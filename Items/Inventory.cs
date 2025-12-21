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

    // Crafting materials - each entry is item weight in kg
    public List<double> Stone { get; set; } = [];       // ~0.3kg each, from foraging
    public List<double> Bone { get; set; } = [];        // From butchering
    public List<double> Hide { get; set; } = [];        // From butchering
    public List<double> PlantFiber { get; set; } = [];  // ~0.1kg bundles, from foraging
    public List<double> Sinew { get; set; } = [];       // From butchering

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
    public double CraftingMaterialsWeightKg => Stone.Sum() + Bone.Sum() + Hide.Sum() + PlantFiber.Sum() + Sinew.Sum();
    public double ToolsWeightKg => Tools.Sum(t => t.Weight);
    public double SpecialWeightKg => Special.Sum(i => i.Weight);

    public double EquipmentWeightKg =>
        (Head?.Weight ?? 0) + (Chest?.Weight ?? 0) + (Legs?.Weight ?? 0) +
        (Feet?.Weight ?? 0) + (Hands?.Weight ?? 0) + (Weapon?.Weight ?? 0);

    public double CurrentWeightKg =>
        FuelWeightKg + FoodWeightKg + WaterWeightKg + CraftingMaterialsWeightKg + ToolsWeightKg + SpecialWeightKg + EquipmentWeightKg;

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
    public int StoneCount => Stone.Count;
    public int BoneCount => Bone.Count;
    public int HideCount => Hide.Count;
    public int PlantFiberCount => PlantFiber.Count;
    public int SinewCount => Sinew.Count;

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
        Stone.AddRange(resources.Stone);
        Bone.AddRange(resources.Bone);
        Hide.AddRange(resources.Hide);
        PlantFiber.AddRange(resources.PlantFiber);
        Sinew.AddRange(resources.Sinew);
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
    /// Remove and return one stone.
    /// Returns 0 if no stones available.
    /// </summary>
    public double TakeStone()
    {
        if (Stone.Count == 0) return 0;
        var piece = Stone[0];
        Stone.RemoveAt(0);
        return piece;
    }

    /// <summary>
    /// Remove and return one bone.
    /// Returns 0 if no bones available.
    /// </summary>
    public double TakeBone()
    {
        if (Bone.Count == 0) return 0;
        var piece = Bone[0];
        Bone.RemoveAt(0);
        return piece;
    }

    /// <summary>
    /// Remove and return one hide.
    /// Returns 0 if no hides available.
    /// </summary>
    public double TakeHide()
    {
        if (Hide.Count == 0) return 0;
        var piece = Hide[0];
        Hide.RemoveAt(0);
        return piece;
    }

    /// <summary>
    /// Remove and return one bundle of plant fiber.
    /// Returns 0 if no plant fiber available.
    /// </summary>
    public double TakePlantFiber()
    {
        if (PlantFiber.Count == 0) return 0;
        var piece = PlantFiber[0];
        PlantFiber.RemoveAt(0);
        return piece;
    }

    /// <summary>
    /// Remove and return one piece of sinew.
    /// Returns 0 if no sinew available.
    /// </summary>
    public double TakeSinew()
    {
        if (Sinew.Count == 0) return 0;
        var piece = Sinew[0];
        Sinew.RemoveAt(0);
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
    /// Check if carrying any meat (raw or cooked).
    /// </summary>
    public bool HasMeat => RawMeat.Count > 0 || CookedMeat.Count > 0;

    /// <summary>
    /// Drops all meat (raw and cooked). Returns total weight dropped.
    /// Used for predator encounters where player sacrifices meat to escape.
    /// </summary>
    public double DropAllMeat()
    {
        double total = RawMeat.Sum() + CookedMeat.Sum();
        RawMeat.Clear();
        CookedMeat.Clear();
        return total;
    }

    /// <summary>
    /// Check if there's any water available.
    /// </summary>
    public bool HasWater => WaterLiters > 0;

    /// <summary>
    /// Check if there's any fuel available.
    /// Tinder burns fast (inefficient) but counts as fuel.
    /// </summary>
    public bool HasFuel => Logs.Count > 0 || Sticks.Count > 0 || Tinder.Count > 0;

    /// <summary>
    /// Check if inventory has a cutting tool (knife or axe) for butchering.
    /// Checks both equipped weapon and unequipped tools.
    /// </summary>
    public bool HasCuttingTool =>
        (Weapon != null && (Weapon.Type == ToolType.Knife || Weapon.Type == ToolType.Axe)) ||
        Tools.Any(t => t.Type == ToolType.Knife || t.Type == ToolType.Axe);

    /// <summary>
    /// Check if there are any crafting materials available.
    /// Includes sticks (can be used for crafting) and dedicated materials.
    /// </summary>
    public bool HasCraftingMaterials =>
        Stone.Count > 0 || Bone.Count > 0 || Hide.Count > 0 ||
        PlantFiber.Count > 0 || Sinew.Count > 0 || Sticks.Count > 1 || Logs.Count > 0;

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
    /// Gets or equips a weapon of the specified type.
    /// If no matching weapon is equipped, checks Tools and auto-equips.
    /// Prompts player if multiple matching weapons are available.
    /// </summary>
    public Tool? GetOrEquipWeapon(ToolType? type = null)
    {
        // Already have matching weapon equipped?
        if (Weapon != null && (type == null || Weapon.Type == type))
            return Weapon;

        // Find matching weapons in Tools
        var available = Tools.Where(t => t.IsWeapon && (type == null || t.Type == type)).ToList();

        if (available.Count == 0)
            return null;

        Tool toEquip;
        if (available.Count == 1)
        {
            toEquip = available[0];
        }
        else
        {
            // Prompt player to choose
            var choice = new Actions.Choice<Tool>("Which weapon?");
            foreach (var w in available)
                choice.AddOption($"{w.Name} ({w.Damage:F0} dmg)", w);
            toEquip = choice.GetPlayerChoice();
        }

        Tools.Remove(toEquip);
        var previous = EquipWeapon(toEquip);
        if (previous != null)
            Tools.Add(previous);

        return toEquip;
    }

    /// <summary>
    /// Get a tool by type. Checks equipped weapon first, then tools list.
    /// </summary>
    public Tool? GetTool(ToolType type)
    {
        if (Weapon != null && Weapon.Type == type)
            return Weapon;
        return Tools.FirstOrDefault(t => t.Type == type);
    }

    /// <summary>
    /// Get equipped equipment by slot.
    /// </summary>
    public Equipment? GetEquipment(EquipSlot slot)
    {
        return slot switch
        {
            EquipSlot.Head => Head,
            EquipSlot.Chest => Chest,
            EquipSlot.Legs => Legs,
            EquipSlot.Feet => Feet,
            EquipSlot.Hands => Hands,
            _ => null
        };
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
        new() { MaxWeightKg = 500.0 };

    /// <summary>
    /// Get a list of all transferable items with descriptions for UI.
    /// Returns tuples of (category, description, weight, transferAction).
    /// </summary>
    public List<(string Category, string Description, double Weight, Action TransferTo)> GetTransferableItems(Inventory target)
    {
        var items = new List<(string, string, double, Action)>();

        // Fuel
        for (int i = 0; i < Logs.Count; i++)
        {
            int idx = i;
            double w = Logs[idx];
            items.Add(("Fuel", $"Log ({w:F1}kg)", w, () => { target.Logs.Add(w); Logs.RemoveAt(idx); }));
        }
        for (int i = 0; i < Sticks.Count; i++)
        {
            int idx = i;
            double w = Sticks[idx];
            items.Add(("Fuel", $"Stick ({w:F2}kg)", w, () => { target.Sticks.Add(w); Sticks.RemoveAt(idx); }));
        }
        for (int i = 0; i < Tinder.Count; i++)
        {
            int idx = i;
            double w = Tinder[idx];
            items.Add(("Fuel", $"Tinder ({w:F2}kg)", w, () => { target.Tinder.Add(w); Tinder.RemoveAt(idx); }));
        }

        // Food
        for (int i = 0; i < CookedMeat.Count; i++)
        {
            int idx = i;
            double w = CookedMeat[idx];
            items.Add(("Food", $"Cooked meat ({w:F1}kg)", w, () => { target.CookedMeat.Add(w); CookedMeat.RemoveAt(idx); }));
        }
        for (int i = 0; i < RawMeat.Count; i++)
        {
            int idx = i;
            double w = RawMeat[idx];
            items.Add(("Food", $"Raw meat ({w:F1}kg)", w, () => { target.RawMeat.Add(w); RawMeat.RemoveAt(idx); }));
        }
        for (int i = 0; i < Berries.Count; i++)
        {
            int idx = i;
            double w = Berries[idx];
            items.Add(("Food", $"Berries ({w:F2}kg)", w, () => { target.Berries.Add(w); Berries.RemoveAt(idx); }));
        }

        // Water (transfer in 0.5L increments)
        if (WaterLiters >= 0.5)
        {
            items.Add(("Water", $"Water (0.5L)", 0.5, () => { target.WaterLiters += 0.5; WaterLiters -= 0.5; }));
        }

        // Materials
        for (int i = 0; i < Stone.Count; i++)
        {
            int idx = i;
            double w = Stone[idx];
            items.Add(("Materials", $"Stone ({w:F1}kg)", w, () => { target.Stone.Add(w); Stone.RemoveAt(idx); }));
        }
        for (int i = 0; i < Bone.Count; i++)
        {
            int idx = i;
            double w = Bone[idx];
            items.Add(("Materials", $"Bone ({w:F1}kg)", w, () => { target.Bone.Add(w); Bone.RemoveAt(idx); }));
        }
        for (int i = 0; i < Hide.Count; i++)
        {
            int idx = i;
            double w = Hide[idx];
            items.Add(("Materials", $"Hide ({w:F1}kg)", w, () => { target.Hide.Add(w); Hide.RemoveAt(idx); }));
        }
        for (int i = 0; i < PlantFiber.Count; i++)
        {
            int idx = i;
            double w = PlantFiber[idx];
            items.Add(("Materials", $"Plant fiber ({w:F2}kg)", w, () => { target.PlantFiber.Add(w); PlantFiber.RemoveAt(idx); }));
        }
        for (int i = 0; i < Sinew.Count; i++)
        {
            int idx = i;
            double w = Sinew[idx];
            items.Add(("Materials", $"Sinew ({w:F2}kg)", w, () => { target.Sinew.Add(w); Sinew.RemoveAt(idx); }));
        }

        // Tools
        for (int i = 0; i < Tools.Count; i++)
        {
            int idx = i;
            var tool = Tools[idx];
            items.Add(("Tools", $"{tool.Name} ({tool.Weight:F1}kg)", tool.Weight, () => { target.Tools.Add(tool); Tools.RemoveAt(idx); }));
        }

        return items;
    }
}
