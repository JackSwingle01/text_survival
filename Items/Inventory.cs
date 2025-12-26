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
    Stick, Pine, Birch, Oak,

    // Tinder
    Tinder, BirchBark,

    // Food
    CookedMeat, RawMeat, Berries, Nuts, Roots, DriedMeat, DriedBerries,

    // Materials
    Stone, Bone, Hide, PlantFiber, Sinew, Shale, Flint, Pyrite,
    ScrapedHide, CuredHide, RawFiber, RawFat, Tallow, Charcoal,

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
            Resource.Pine, Resource.Birch, Resource.Oak, Resource.BirchBark
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
            Resource.Nuts, Resource.Roots, Resource.DriedMeat, Resource.DriedBerries
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
            Resource.RawFat, Resource.Tallow, Resource.Charcoal
        }
    };
}

public class Inventory
{
    // Capacity (-1 = unlimited)
    public double MaxWeightKg { get; set; } = -1;

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
    public List<Tool> Tools { get; set; } = new();
    public List<Item> Special { get; set; } = new();

    // Equipment - dictionary backing store
    private Dictionary<EquipSlot, Equipment?> _equipment = new()
    {
        [EquipSlot.Head] = null,
        [EquipSlot.Chest] = null,
        [EquipSlot.Legs] = null,
        [EquipSlot.Feet] = null,
        [EquipSlot.Hands] = null
    };

    // Public property for JSON serialization
    public Dictionary<EquipSlot, Equipment?> Equipment
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
    [JsonIgnore] public Equipment? Head { get => _equipment[EquipSlot.Head]; set => _equipment[EquipSlot.Head] = value; }
    [JsonIgnore] public Equipment? Chest { get => _equipment[EquipSlot.Chest]; set => _equipment[EquipSlot.Chest] = value; }
    [JsonIgnore] public Equipment? Legs { get => _equipment[EquipSlot.Legs]; set => _equipment[EquipSlot.Legs] = value; }
    [JsonIgnore] public Equipment? Feet { get => _equipment[EquipSlot.Feet]; set => _equipment[EquipSlot.Feet] = value; }
    [JsonIgnore] public Equipment? Hands { get => _equipment[EquipSlot.Hands]; set => _equipment[EquipSlot.Hands] = value; }

    public Tool? Weapon { get; set; }

    // Active torch
    public Tool? ActiveTorch { get; set; }
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
    public double SpecialWeight => Special.Sum(i => i.Weight);
    public double EquipmentWeight =>
        _equipment.Values.Sum(e => e?.Weight ?? 0) + (Weapon?.Weight ?? 0);

    public double CurrentWeightKg =>
        ResourceWeight + WaterLiters + ToolsWeight + SpecialWeight + EquipmentWeight +
        (ActiveTorch?.Weight ?? 0);

    public double TotalInsulation =>
        _equipment.Values.Sum(e => e?.Insulation ?? 0);

    public bool CanCarry(double additionalKg) =>
        MaxWeightKg < 0 || CurrentWeightKg + additionalKg <= MaxWeightKg;

    public double RemainingCapacityKg =>
        MaxWeightKg < 0 ? double.MaxValue : Math.Max(0, MaxWeightKg - CurrentWeightKg);

    // Torch methods
    public bool HasLitTorch => ActiveTorch != null && TorchBurnTimeRemainingMinutes > 0;
    public bool HasUnlitTorch => Tools.Any(t => t.Type == ToolType.Torch && t.Works);

    public double GetTorchHeatBonusF()
    {
        if (!HasLitTorch) return 0;
        double burnPct = TorchBurnTimeRemainingMinutes / 60.0;
        return 3.0 + (2.0 * burnPct);
    }

    public bool LightTorch()
    {
        var torch = Tools.FirstOrDefault(t => t.Type == ToolType.Torch && t.Works);
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
        Special.AddRange(other.Special);
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
        Special.Count == 0;

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

    public bool HasCuttingTool =>
        (Weapon != null && (Weapon.Type == ToolType.Knife || Weapon.Type == ToolType.Axe)) ||
        Tools.Any(t => t.Type == ToolType.Knife || t.Type == ToolType.Axe);

    public bool HasCraftingMaterials => Has(ResourceCategory.Material);

    // Equipment management
    public text_survival.Items.Equipment? Equip(text_survival.Items.Equipment equipment)
    {
        var previous = _equipment[equipment.Slot];
        _equipment[equipment.Slot] = equipment;
        return previous;
    }

    public text_survival.Items.Equipment? Unequip(EquipSlot slot)
    {
        var removed = _equipment[slot];
        _equipment[slot] = null;
        return removed;
    }

    public Tool? EquipWeapon(Tool weapon)
    {
        if (!weapon.IsWeapon)
            throw new InvalidOperationException($"Tool '{weapon.Name}' cannot be equipped as weapon");

        var previous = Weapon;
        Weapon = weapon;
        return previous;
    }

    public Tool? UnequipWeapon()
    {
        var removed = Weapon;
        Weapon = null;
        return removed;
    }

    public Tool? GetOrEquipWeapon(GameContext ctx, ToolType? type = null)
    {
        if (Weapon != null && (type == null || Weapon.Type == type))
            return Weapon;

        var available = Tools.Where(t => t.IsWeapon && (type == null || t.Type == type)).ToList();
        if (available.Count == 0) return null;

        Tool toEquip;
        if (available.Count == 1)
        {
            toEquip = available[0];
        }
        else
        {
            var choice = new Choice<Tool>("Which weapon?");
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

    public Tool? GetTool(ToolType type)
    {
        if (Weapon != null && Weapon.Type == type)
            return Weapon;
        return Tools.FirstOrDefault(t => t.Type == type);
    }

    public text_survival.Items.Equipment? GetEquipment(EquipSlot slot) =>
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
        foreach (var item in inv.Special) parts.Add(item.Name);

        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }
}