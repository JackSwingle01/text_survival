using System.Text.Json.Serialization;
using text_survival.Actions;
using text_survival.Items;

public enum ResourceCategory
{
    Fuel,
    Tinder,
    Food,
    Medicine,
    Material,
    Log  // Groups typed wood: Pine, Birch, Oak
}

public enum Resource
{
    // Fuel (wood types: Pine, Birch, Oak are also logs)
    Stick, Pine, Birch, Oak, Peat,

    // Tinder
    Tinder, BirchBark,

    // Food
    CookedMeat, RawMeat, Berries, Nuts, Roots, DriedMeat, DriedBerries, Honey,

    // Materials
    Stone, Bone, Hide, PlantFiber, Sinew, Shale, Flint, Pyrite,
    ScrapedHide, CuredHide, RawFiber, RawFat, Tallow, Charcoal, Rope, Ivory,
    MammothHide,  // Trophy material from mammoth hunts

    // Medicine
    BirchPolypore, Chaga, Amadou, RoseHip, JuniperBerry,
    WillowBark, PineNeedles, PineResin, Usnea, SphagnumMoss
}

public static class ResourceCategories
{
    public static readonly Dictionary<ResourceCategory, HashSet<Resource>> Items = new()
    {
        [ResourceCategory.Fuel] = new()
        {
            Resource.Stick, Resource.Tinder,
            Resource.Pine, Resource.Birch, Resource.Oak, Resource.BirchBark, Resource.Peat
        },

        [ResourceCategory.Log] = new()
        {
            Resource.Pine, Resource.Birch, Resource.Oak
        },

        [ResourceCategory.Tinder] = new()
        {
            Resource.Tinder, Resource.BirchBark
        },

        [ResourceCategory.Food] = new()
        {
            Resource.CookedMeat, Resource.RawMeat, Resource.Berries,
            Resource.Nuts, Resource.Roots, Resource.DriedMeat, Resource.DriedBerries, Resource.Honey
        },

        [ResourceCategory.Medicine] = new()
        {
            Resource.BirchPolypore, Resource.Chaga, Resource.Amadou,
            Resource.RoseHip, Resource.JuniperBerry, Resource.WillowBark,
            Resource.PineNeedles, Resource.PineResin, Resource.Usnea, Resource.SphagnumMoss
        },

        [ResourceCategory.Material] = new()
        {
            Resource.Stone, Resource.Bone, Resource.Hide, Resource.PlantFiber,
            Resource.Sinew, Resource.Shale, Resource.Flint, Resource.Pyrite,
            Resource.ScrapedHide, Resource.CuredHide, Resource.RawFiber,
            Resource.RawFat, Resource.Tallow, Resource.Charcoal, Resource.Rope, Resource.Ivory,
            Resource.MammothHide
        }
    };
}

public class Inventory
{
    // Capacity (-1 = unlimited)
    // Base capacity before accessory bonuses
    private double _baseMaxWeightKg = -1;

    // Effective capacity including accessory bonuses
    public double MaxWeightKg
    {
        get => _baseMaxWeightKg < 0 ? -1 : _baseMaxWeightKg + AccessoryCapacityBonus;
        set => _baseMaxWeightKg = value;
    }

    // For display/serialization when needed
    [System.Text.Json.Serialization.JsonIgnore]
    public double BaseMaxWeightKg => _baseMaxWeightKg;

    // Accessories (stackable capacity boosters)
    public List<Gear> Accessories { get; set; } = [];

    public double AccessoryCapacityBonus => Accessories.Sum(a => a.CapacityBonusKg);
    public double AccessoriesWeight => Accessories.Sum(a => a.Weight);

    // Stack-based resources
    private readonly Dictionary<Resource, Stack<double>> _stacks = new();

    // Public property for serialization (System.Text.Json needs this)
    // Note: StackConverterFactory handles order preservation, no manual reversal needed
    public Dictionary<Resource, Stack<double>> Stacks
    {
        get => _stacks;
        set
        {
            _stacks.Clear();
            foreach (var (resource, stack) in value)
            {
                _stacks[resource] = stack;
            }
        }
    }

    // Water (kept separate - measured in liters)
    public double WaterLiters { get; set; }

    // Discrete items
    public List<Gear> Tools { get; set; } = new();

    // Equipment - dictionary backing store
    private Dictionary<EquipSlot, Gear?> _equipment = new()
    {
        [EquipSlot.Head] = null,
        [EquipSlot.Chest] = null,
        [EquipSlot.Legs] = null,
        [EquipSlot.Feet] = null,
        [EquipSlot.Hands] = null
    };

    // Public property for JSON serialization
    public Dictionary<EquipSlot, Gear?> Equipment
    {
        get => _equipment;
        set
        {
            _equipment.Clear();
            foreach (var (slot, equipment) in value)
            {
                _equipment[slot] = equipment;
            }
        }
    }

    // Computed properties for backward compatibility (JsonIgnore to avoid duplication)
    [JsonIgnore] public Gear? Head { get => _equipment[EquipSlot.Head]; set => _equipment[EquipSlot.Head] = value; }
    [JsonIgnore] public Gear? Chest { get => _equipment[EquipSlot.Chest]; set => _equipment[EquipSlot.Chest] = value; }
    [JsonIgnore] public Gear? Legs { get => _equipment[EquipSlot.Legs]; set => _equipment[EquipSlot.Legs] = value; }
    [JsonIgnore] public Gear? Feet { get => _equipment[EquipSlot.Feet]; set => _equipment[EquipSlot.Feet] = value; }
    [JsonIgnore] public Gear? Hands { get => _equipment[EquipSlot.Hands]; set => _equipment[EquipSlot.Hands] = value; }

    public Gear? Weapon { get; set; }

    // Active torch
    public Gear? ActiveTorch { get; set; }
    public double TorchBurnTimeRemainingMinutes { get; set; }

    public Inventory()
    {
        foreach (Resource type in Enum.GetValues<Resource>())
            _stacks[type] = new Stack<double>();
    }

    // Access
    public Stack<double> this[Resource type] => _stacks[type];

    public void Add(Resource type, double weight) => _stacks[type].Push(weight);
    public int Count(Resource type) => _stacks[type].Count;
    public double Weight(Resource type) => _stacks[type].Sum();
    public double Pop(Resource type) => _stacks[type].TryPop(out var w) ? w : 0;
    public double Peek(Resource type) => _stacks[type].Count > 0 ? _stacks[type].Peek() : 0;

    public void Take(Resource type, int count)
    {
        for (int i = 0; i < count; i++) Pop(type);
    }

    // Category-based queries
    public bool Has(ResourceCategory category) =>
        ResourceCategories.Items[category].Any(type => _stacks[type].Count > 0);

    public double GetWeight(ResourceCategory category) =>
        ResourceCategories.Items[category].Sum(type => _stacks[type].Sum());

    public int GetCount(ResourceCategory category) =>
        ResourceCategories.Items[category].Sum(type => _stacks[type].Count);

    // Convenience properties
    public bool HasFood => Has(ResourceCategory.Food);
    public bool HasFuel => Has(ResourceCategory.Fuel);
    public bool HasWater => WaterLiters > 0;
    public bool HasMeat => _stacks[Resource.RawMeat].Count > 0 || _stacks[Resource.CookedMeat].Count > 0;

    // Log helpers (aggregates typed wood: Pine, Birch, Oak)
    public bool HasLogs => Has(ResourceCategory.Log);
    public int LogCount => GetCount(ResourceCategory.Log);
    public double LogWeight => GetWeight(ResourceCategory.Log);

    /// <summary>
    /// Pop a log, preferring the specified type if available.
    /// </summary>
    public double PopLog(Resource? preferred = null)
    {
        if (preferred.HasValue && _stacks[preferred.Value].Count > 0)
            return Pop(preferred.Value);

        foreach (var logType in ResourceCategories.Items[ResourceCategory.Log])
        {
            if (_stacks[logType].Count > 0)
                return Pop(logType);
        }
        return 0;
    }

    // Weight calculations
    public double ResourceWeight => _stacks.Values.Sum(s => s.Sum());
    public double ToolsWeight => Tools.Sum(t => t.Weight);
    public double EquipmentWeight =>
        _equipment.Values.Sum(e => e?.Weight ?? 0) + (Weapon?.Weight ?? 0);

    public double CurrentWeightKg =>
        ResourceWeight + WaterLiters + ToolsWeight + EquipmentWeight +
        AccessoriesWeight + (ActiveTorch?.Weight ?? 0);

    public double TotalInsulation =>
        _equipment.Values.Sum(e => e?.Insulation ?? 0);

    // Armor: Cushioning absorbs Blunt, Toughness resists Sharp/Pierce
    // Values stack from all equipped gear
    public double TotalCushioning =>
        _equipment.Values.Sum(e => e?.Cushioning ?? 0);

    public double TotalToughness =>
        _equipment.Values.Sum(e => e?.Toughness ?? 0);

    public bool CanCarry(double additionalKg) =>
        MaxWeightKg < 0 || CurrentWeightKg + additionalKg <= MaxWeightKg;

    public double RemainingCapacityKg =>
        MaxWeightKg < 0 ? double.MaxValue : Math.Max(0, MaxWeightKg - CurrentWeightKg);

    // Torch methods
    public bool HasLitTorch => ActiveTorch != null && TorchBurnTimeRemainingMinutes > 0;
    public bool HasUnlitTorch => Tools.Any(t => t.ToolType == ToolType.Torch && t.Works);

    public double GetTorchHeatBonusF()
    {
        if (!HasLitTorch) return 0;
        double burnPct = TorchBurnTimeRemainingMinutes / 60.0;
        return 3.0 + (2.0 * burnPct);
    }

    /// <summary>
    /// Get heat bonus from lit ember carriers.
    /// Smaller than torch (2-3°F vs 3-5°F) but longer lasting.
    /// </summary>
    public double GetEmberCarrierHeatBonusF()
    {
        var litCarriers = Tools
            .Where(t => t.IsEmberCarrier && t.IsEmberLit)
            .ToList();

        if (litCarriers.Count == 0) return 0;

        double totalBonus = 0;
        foreach (var carrier in litCarriers)
        {
            double burnPct = carrier.EmberBurnHoursRemaining / carrier.EmberBurnHoursMax;
            totalBonus += 2.0 + (1.0 * burnPct);  // 2-3°F per carrier
        }
        return totalBonus;
    }

    public bool LightTorch()
    {
        var torch = Tools.FirstOrDefault(t => t.ToolType == ToolType.Torch && t.Works);
        if (torch == null) return false;
        Tools.Remove(torch);
        ActiveTorch = torch;
        TorchBurnTimeRemainingMinutes = 60;
        return true;
    }

    // Combine inventories
    public void Combine(Inventory other)
    {
        foreach (var type in _stacks.Keys)
            foreach (var item in other._stacks[type])
                _stacks[type].Push(item);

        WaterLiters += other.WaterLiters;
        Tools.AddRange(other.Tools);
        Accessories.AddRange(other.Accessories);
    }

    /// <summary>
    /// Combine inventories while respecting capacity limits.
    /// Returns a new Inventory containing items that didn't fit.
    /// </summary>
    public Inventory CombineWithCapacity(Inventory other)
    {
        // If unlimited capacity, just combine normally
        if (MaxWeightKg < 0)
        {
            Combine(other);
            return new Inventory();
        }

        var leftovers = new Inventory();

        // Add resources one by one, checking capacity
        foreach (var type in _stacks.Keys)
        {
            foreach (var weight in other._stacks[type].ToArray())
            {
                if (CanCarry(weight))
                    _stacks[type].Push(weight);
                else
                    leftovers._stacks[type].Push(weight);
            }
        }

        // Add water (partial liters OK)
        if (other.WaterLiters > 0)
        {
            double canFit = RemainingCapacityKg;
            double toAdd = Math.Min(other.WaterLiters, canFit);
            WaterLiters += toAdd;
            double leftover = other.WaterLiters - toAdd;
            if (leftover > 0)
                leftovers.WaterLiters = leftover;
        }

        // Add tools if they fit
        foreach (var tool in other.Tools)
        {
            if (CanCarry(tool.Weight))
                Tools.Add(tool);
            else
                leftovers.Tools.Add(tool);
        }

        // Add accessories if they fit
        foreach (var accessory in other.Accessories)
        {
            if (CanCarry(accessory.Weight))
                Accessories.Add(accessory);
            else
                leftovers.Accessories.Add(accessory);
        }

        return leftovers;
    }

    // Apply multiplier to all resources
    public void ApplyMultiplier(double multiplier)
    {
        foreach (var stack in _stacks.Values)
            MultiplyStack(stack, multiplier);

        WaterLiters *= multiplier;
    }

    private static void MultiplyStack(Stack<double> stack, double multiplier)
    {
        var items = stack.ToArray();
        stack.Clear();
        foreach (var item in items)
            stack.Push(item * multiplier);
    }

    // Convenience checks
    public bool IsEmpty =>
        _stacks.Values.All(s => s.Count == 0) &&
        WaterLiters == 0 &&
        Tools.Count == 0 &&
        Accessories.Count == 0;

    public bool CanStartFire =>
        _stacks[Resource.Tinder].Count > 0 &&
        (_stacks[Resource.Stick].Count > 0 || HasLogs);

    public double DropAllMeat()
    {
        double total = _stacks[Resource.RawMeat].Sum() + _stacks[Resource.CookedMeat].Sum();
        _stacks[Resource.RawMeat].Clear();
        _stacks[Resource.CookedMeat].Clear();
        return total;
    }

    public bool HasWeapon => Weapon != null;

    public bool HasFirestarter =>
        Tools.Any(t => t.ToolType == ToolType.FireStriker ||
                       t.ToolType == ToolType.HandDrill ||
                       t.ToolType == ToolType.BowDrill);

    public bool HasCuttingTool =>
        (Weapon != null && (Weapon.ToolType == ToolType.Knife || Weapon.ToolType == ToolType.Axe)) ||
        Tools.Any(t => t.ToolType == ToolType.Knife || t.ToolType == ToolType.Axe);

    public bool HasCraftingMaterials => Has(ResourceCategory.Material);

    public bool HasBuildingMaterials =>
        Count(Resource.Stone) > 0 ||
        GetCount(ResourceCategory.Log) > 0 ||
        Count(Resource.Stick) > 0 ||
        Count(Resource.PlantFiber) > 0 ||
        Count(Resource.Hide) > 0 ||
        Count(Resource.CuredHide) > 0 ||
        Count(Resource.Rope) > 0 ||
        Count(Resource.Sinew) > 0;

    // Equipment management
    public Gear? Equip(Gear equipment)
    {
        if (equipment.Slot == null)
            throw new InvalidOperationException($"Gear '{equipment.Name}' has no slot");
        var previous = _equipment[equipment.Slot.Value];
        _equipment[equipment.Slot.Value] = equipment;
        return previous;
    }

    public Gear? Unequip(EquipSlot slot)
    {
        var removed = _equipment[slot];
        _equipment[slot] = null;
        return removed;
    }

    public Gear? EquipWeapon(Gear weapon)
    {
        if (!weapon.IsWeapon)
            throw new InvalidOperationException($"Gear '{weapon.Name}' cannot be equipped as weapon");

        var previous = Weapon;
        Weapon = weapon;
        return previous;
    }

    public Gear? UnequipWeapon()
    {
        var removed = Weapon;
        Weapon = null;
        return removed;
    }

    public Gear? GetOrEquipWeapon(GameContext ctx, ToolType? type = null)
    {
        if (Weapon != null && (type == null || Weapon.ToolType == type))
            return Weapon;

        var available = Tools.Where(t => t.IsWeapon && (type == null || t.ToolType == type)).ToList();
        if (available.Count == 0) return null;

        Gear toEquip;
        if (available.Count == 1)
        {
            toEquip = available[0];
        }
        else
        {
            var choice = new Choice<Gear>("Which weapon?");
            foreach (var w in available)
                choice.AddOption($"{w.Name} ({w.Damage:F0} dmg)", w);
            toEquip = choice.GetPlayerChoice(ctx);
        }

        Tools.Remove(toEquip);
        var previous = EquipWeapon(toEquip);
        if (previous != null)
            Tools.Add(previous);

        return toEquip;
    }

    public Gear? GetTool(ToolType type)
    {
        if (Weapon != null && Weapon.ToolType == type)
            return Weapon;
        return Tools.FirstOrDefault(t => t.ToolType == type);
    }

    public Gear? GetEquipment(EquipSlot slot) =>
        _equipment.GetValueOrDefault(slot);

    // Factory methods
    public static Inventory CreatePlayerInventory(double maxWeightKg = 15.0) =>
        new() { MaxWeightKg = maxWeightKg };

    public static Inventory CreateCampStorage() =>
        new() { MaxWeightKg = 500.0 };

    // Transfer items for camp storage UI
    public List<(string Category, string Description, double Weight, Action TransferTo)> GetTransferableItems(Inventory target)
    {
        var items = new List<(string, string, double, Action)>();

        foreach (Resource type in Enum.GetValues<Resource>())
        {
            foreach (var w in _stacks[type])
            {
                string name = type.ToDisplayName();
                string category = GetCategoryName(type);
                items.Add((category, $"{name} ({w:F2}kg)", w,
                    () => target._stacks[type].Push(_stacks[type].Pop())));
            }
        }

        if (WaterLiters >= 0.5)
            items.Add(("Water", "Water (0.5L)", 0.5,
                () => { target.WaterLiters += 0.5; WaterLiters -= 0.5; }
            ));

        foreach (var tool in Tools.ToList())
        {
            items.Add(("Tools", $"{tool.Name} ({tool.Weight:F1}kg)", tool.Weight, () =>
            {
                target.Tools.Add(tool);
                Tools.Remove(tool);
            }
            ));
        }

        foreach (var accessory in Accessories.ToList())
        {
            items.Add(("Carrying", $"{accessory.Name} (+{accessory.CapacityBonusKg}kg) ({accessory.Weight:F1}kg)", accessory.Weight, () =>
            {
                target.Accessories.Add(accessory);
                Accessories.Remove(accessory);
            }
            ));
        }

        return items;
    }

    private static string GetCategoryName(Resource type)
    {
        foreach (var (category, items) in ResourceCategories.Items)
            if (items.Contains(type))
                return category.ToString();
        return "Other";
    }
}

public static class ResourceTypeExtensions
{
    public static string ToDisplayName(this Resource type)
    {
        string name = type.ToString();
        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? $" {char.ToLower(c)}" : c.ToString()
        ));
    }
}

public static class InventoryExtensions
{
    public static string GetDescription(this Inventory inv)
    {
        var parts = new List<string>();

        foreach (Resource type in Enum.GetValues<Resource>())
        {
            int count = inv.Count(type);
            if (count > 0)
            {
                string name = type.ToDisplayName();
                parts.Add(count > 1 ? $"{count} {name}s" : $"{count} {name}");
            }
        }

        if (inv.WaterLiters > 0) parts.Add($"{inv.WaterLiters:F1}L water");

        foreach (var tool in inv.Tools) parts.Add(tool.Name);
        foreach (var accessory in inv.Accessories) parts.Add(accessory.Name);

        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }
}