namespace Survival;

/// Everything carried by weight. Fuels are resources too: Fire.Props says which ones burn and how,
/// so a log is one thing whether it is in a pack, on a stash or on the fire.
public enum Resource
{
    // tinders and fuel (old FuelType and the matching old Resource values, one list)
    Tinder, BirchBark, Amadou, Usnea, Chaga, Kindling, Pine, Birch, Oak, Bone, Peat,
    // food
    RawMeat, CookedMeat, DriedMeat, RawFish, CookedFish, DriedFish, Berries, DriedBerries, Nuts, Roots, Honey,
    Water,
    // materials
    Stone, Hide, PlantFiber, Sinew, Shale, Flint, Pyrite, ScrapedHide, CuredHide, RawFat, Tallow, Charcoal, Rope,
    Ivory, MammothHide, Feather,
    // medicine
    BirchPolypore, RoseHip, JuniperBerry, WillowBark, PineNeedles, PineResin, SphagnumMoss,
}

/// Old ResourceCategories, one category per resource (old Fuel/Tinder/Log overlaps are Fire.Props now).
public enum ResourceCategory { Fuel, Food, Water, Medicine, Material }

/// Old Gear categories. Clothing is worn on a body; accessories add carrying capacity.
public enum GearCategory { Tool, Clothing, Accessory }

public enum ToolType { Axe, Knife, FireStriker, HandDrill, BowDrill, WaterContainer, Spear, Club, Torch, Needle, Shovel }

/// Body surface a garment covers. Old Inventory.SlotCoverage.
public enum EquipSlot { Head, Chest, Legs, Feet, Hands }

public enum WeaponClass { Blade, Blunt, Pierce, Claw, Unarmed }

/// What a weapon does in a fight. Read by combat; old Gear.Damage / WeaponClass / BlockChance.
public sealed record Weapon(double Damage, WeaponClass Class, double BlockChance);

/// One row of the resource table. Kcal and water per kg of food (old ConsumptionHandler).
public sealed record ResourceDef(ResourceCategory Category, double KcalPerKg = 0, double HydrationMlPerKg = 0);

/// One row of the gear table: the authored design. Instances carry only their kind and wear.
/// Insulation is the old BaseInsulation (tenths of a clo over the covered part of the body).
public sealed record GearDef(
    string Name, GearCategory Category, double BaseWeightKg, int MaxDurability = -1,
    ToolType? Tool = null, Weapon? Weapon = null, EquipSlot? Slot = null,
    double Insulation = 0, double Cushioning = 0, double Toughness = 0, double WaterproofLevel = 0,
    double CapacityBonusKg = 0);

/// One discrete item. Durability -1 never wears out; 0 is broken.
public sealed class Gear
{
    public required string Kind { get; init; }
    public int Durability { get; set; } = -1;
    public int MaxDurability { get; init; } = -1;
}

/// What one agent carries, or a stash on a tile. Plain data: resources by kg, gear by instance.
public sealed class Inventory
{
    /// Sorted, not a Dictionary: a Dictionary's order after Remove-then-Add differs from its order
    /// freshly loaded, and anything that walks the stacks would then act differently after a load.
    public SortedDictionary<Resource, double> Kg { get; init; } = [];
    public List<Gear> Gear { get; init; } = [];
}

/// Items, inventories and what they weigh. Port of old Items/Gear.cs and Items/Inventory.cs.
public static class Items
{
    /// Base carrying capacity of a person before accessories (old Player and NPCFactory: 15 kg).
    public const double BaseCapacityKg = 15;
    /// Old Gear.CloPerInsulationPoint.
    const double CloPerInsulationPoint = 10.0;

    // ponytail: tables in code until phase 4 moves content to data files; plain rows so that move is mechanical.
    public static readonly IReadOnlyDictionary<Resource, ResourceDef> Resources = new Dictionary<Resource, ResourceDef>
    {
        [Resource.Tinder] = new(ResourceCategory.Fuel),
        [Resource.BirchBark] = new(ResourceCategory.Fuel),
        [Resource.Kindling] = new(ResourceCategory.Fuel),
        [Resource.Pine] = new(ResourceCategory.Fuel),
        [Resource.Birch] = new(ResourceCategory.Fuel),
        [Resource.Oak] = new(ResourceCategory.Fuel),
        [Resource.Peat] = new(ResourceCategory.Fuel),
        [Resource.CookedMeat] = new(ResourceCategory.Food, 2500),
        [Resource.RawMeat] = new(ResourceCategory.Food, 1500),
        [Resource.Berries] = new(ResourceCategory.Food, 500, 200),
        [Resource.Honey] = new(ResourceCategory.Food, 3000),
        [Resource.Nuts] = new(ResourceCategory.Food, 6000),
        [Resource.Roots] = new(ResourceCategory.Food, 400, 100),
        [Resource.DriedMeat] = new(ResourceCategory.Food, 3000, -50),
        [Resource.DriedBerries] = new(ResourceCategory.Food, 2500),
        [Resource.CookedFish] = new(ResourceCategory.Food, 1400),
        [Resource.RawFish] = new(ResourceCategory.Food, 1200),
        [Resource.DriedFish] = new(ResourceCategory.Food, 2800, -30),
        [Resource.Water] = new(ResourceCategory.Water, 0, 1000),
        [Resource.Stone] = new(ResourceCategory.Material),
        [Resource.Bone] = new(ResourceCategory.Material),
        [Resource.Hide] = new(ResourceCategory.Material),
        [Resource.PlantFiber] = new(ResourceCategory.Material),
        [Resource.Sinew] = new(ResourceCategory.Material),
        [Resource.Shale] = new(ResourceCategory.Material),
        [Resource.Flint] = new(ResourceCategory.Material),
        [Resource.Pyrite] = new(ResourceCategory.Material),
        [Resource.ScrapedHide] = new(ResourceCategory.Material),
        [Resource.CuredHide] = new(ResourceCategory.Material),
        [Resource.RawFat] = new(ResourceCategory.Material),
        [Resource.Tallow] = new(ResourceCategory.Material),
        [Resource.Charcoal] = new(ResourceCategory.Material),
        [Resource.Rope] = new(ResourceCategory.Material),
        [Resource.Ivory] = new(ResourceCategory.Material),
        [Resource.MammothHide] = new(ResourceCategory.Material),
        [Resource.Feather] = new(ResourceCategory.Material),
        [Resource.BirchPolypore] = new(ResourceCategory.Medicine),
        [Resource.Chaga] = new(ResourceCategory.Medicine),
        [Resource.Amadou] = new(ResourceCategory.Medicine),
        [Resource.RoseHip] = new(ResourceCategory.Medicine),
        [Resource.JuniperBerry] = new(ResourceCategory.Medicine),
        [Resource.WillowBark] = new(ResourceCategory.Medicine),
        [Resource.PineNeedles] = new(ResourceCategory.Medicine),
        [Resource.PineResin] = new(ResourceCategory.Medicine),
        [Resource.Usnea] = new(ResourceCategory.Medicine),
        [Resource.SphagnumMoss] = new(ResourceCategory.Medicine),
    };

    /// Old Gear factory methods, by kind.
    public static readonly IReadOnlyDictionary<string, GearDef> GearKinds = new Dictionary<string, GearDef>
    {
        ["stone_axe"] = new("Stone Axe", GearCategory.Tool, 1.5, Tool: ToolType.Axe, Weapon: new(12, WeaponClass.Blade, 0.05)),
        ["flint_knife"] = new("Flint Knife", GearCategory.Tool, 0.3, Tool: ToolType.Knife, Weapon: new(6, WeaponClass.Blade, 0.02)),
        ["worn_knife"] = new("Worn Knife", GearCategory.Tool, 0.3, 3, ToolType.Knife, new(6, WeaponClass.Blade, 0.02)),
        ["bone_knife"] = new("Bone Knife", GearCategory.Tool, 0.3, 5, ToolType.Knife, new(6, WeaponClass.Blade, 0.02)),
        ["wooden_spear"] = new("Wooden Spear", GearCategory.Tool, 2.0, Tool: ToolType.Spear, Weapon: new(8, WeaponClass.Pierce, 0.12)),
        ["fire_striker"] = new("Fire Striker", GearCategory.Tool, 0.2, Tool: ToolType.FireStriker),
        ["hand_drill"] = new("Hand Drill", GearCategory.Tool, 0.3, Tool: ToolType.HandDrill),
        ["bow_drill"] = new("Bow Drill", GearCategory.Tool, 0.5, Tool: ToolType.BowDrill),
        ["waterskin"] = new("Waterskin", GearCategory.Tool, 0.3, Tool: ToolType.WaterContainer),
        ["torch"] = new("Torch", GearCategory.Tool, 0.5, 1, ToolType.Torch),
        ["bone_needle"] = new("Bone Needle", GearCategory.Tool, 0.05, 20, ToolType.Needle),
        ["worn_fur_chest_wrap"] = new("Worn Fur Chest Wrap", GearCategory.Clothing, 1.5, 50, Slot: EquipSlot.Chest, Insulation: 0.20, Cushioning: 0.12, Toughness: 0.04, WaterproofLevel: 0.1),
        ["fur_chest_wrap"] = new("Fur Chest Wrap", GearCategory.Clothing, 1.8, 100, Slot: EquipSlot.Chest, Insulation: 0.30, Cushioning: 0.20, Toughness: 0.08, WaterproofLevel: 0.1),
        ["fur_leg_wraps"] = new("Fur Leg Wraps", GearCategory.Clothing, 1.0, 100, Slot: EquipSlot.Legs, Insulation: 0.15, Cushioning: 0.15, Toughness: 0.06, WaterproofLevel: 0.1),
        ["worn_hide_boots"] = new("Worn Hide Boots", GearCategory.Clothing, 0.4, 30, Slot: EquipSlot.Feet, Insulation: 0.05, Cushioning: 0.06, Toughness: 0.08, WaterproofLevel: 0.1),
        ["hide_handwraps"] = new("Hide Handwraps", GearCategory.Clothing, 0.15, 50, Slot: EquipSlot.Hands, Insulation: 0.03, Cushioning: 0.05, Toughness: 0.10, WaterproofLevel: 0.1),
        ["bear_hide_chest"] = new("Bear Hide Chest", GearCategory.Clothing, 3.0, 150, Slot: EquipSlot.Chest, Insulation: 0.35, Cushioning: 0.18, Toughness: 0.30, WaterproofLevel: 0.2),
        ["mammoth_hide_chest"] = new("Mammoth Hide Chest", GearCategory.Clothing, 4.5, 200, Slot: EquipSlot.Chest, Insulation: 0.45, Cushioning: 0.25, Toughness: 0.38, WaterproofLevel: 0.25),
        ["mammoth_hide_legs"] = new("Mammoth Hide Legs", GearCategory.Clothing, 2.5, 200, Slot: EquipSlot.Legs, Insulation: 0.30, Cushioning: 0.22, Toughness: 0.35, WaterproofLevel: 0.25),
        ["worn_coat"] = new("Worn Coat", GearCategory.Clothing, 2.0, 50, Slot: EquipSlot.Chest, Insulation: 0.15),
        ["small_pouch"] = new("Small Pouch", GearCategory.Accessory, 0.1, 100, CapacityBonusKg: 0.5),
        ["rope_belt"] = new("Rope Belt", GearCategory.Accessory, 0.4, 100, CapacityBonusKg: 3.0),
        ["proper_belt"] = new("Proper Belt", GearCategory.Accessory, 0.3, 150, CapacityBonusKg: 4.0),
        ["large_bag"] = new("Large Bag", GearCategory.Accessory, 0.8, 100, CapacityBonusKg: 10.0),
    };

    /// Old Inventory.SlotCoverage: the fraction of body surface each slot covers. Sums to 1.
    public static readonly IReadOnlyDictionary<EquipSlot, double> SlotCoverage = new Dictionary<EquipSlot, double>
    {
        [EquipSlot.Head] = 0.1, [EquipSlot.Chest] = 0.4, [EquipSlot.Legs] = 0.3, [EquipSlot.Hands] = 0.1, [EquipSlot.Feet] = 0.1,
    };

    public static GearDef Def(Gear g) =>
        GearKinds.TryGetValue(g.Kind, out var d) ? d : throw new KeyNotFoundException($"No gear kind '{g.Kind}'");

    /// A new item at full condition. Durability is the design's unless an instance says otherwise.
    public static Gear Make(string kind, int? durability = null)
    {
        if (!GearKinds.TryGetValue(kind, out var d)) throw new KeyNotFoundException($"No gear kind '{kind}'");
        int max = durability ?? d.MaxDurability;
        return new Gear { Kind = kind, Durability = max, MaxDurability = max };
    }

    // ---- gear condition ----

    public static double ConditionPct(Gear g) => g.MaxDurability <= 0 ? 1.0 : g.Durability <= 0 ? 0.0 : (double)g.Durability / g.MaxDurability;
    public static bool Works(Gear g) => g.Durability != 0;
    /// Old Gear.Weight: worn things lose a quarter of their weight as they fall apart.
    public static double WeightKg(Gear g) => Def(g).BaseWeightKg * (0.75 + 0.25 * ConditionPct(g));
    /// One use. Returns whether the tool still works afterwards (old Gear.Use).
    public static bool Use(Gear g)
    {
        if (g.Durability == -1) return true;
        if (g.Durability == 0) return false;
        g.Durability--;
        return g.Durability > 0;
    }

    /// Local clo of one garment (old Gear.CloValue).
    public static double CloValue(Gear g) => Def(g).Insulation * CloPerInsulationPoint * ConditionPct(g);

    /// Whole-body clo, area-weighted: a bare slot costs its share (old Inventory.ClothingClo).
    public static double ClothingClo(IEnumerable<Gear> worn) => worn.Sum(g => SlotCoverage[Def(g).Slot!.Value] * CloValue(g));
    /// Old Inventory.TotalEquipmentWeightKg: the thermal mass that holds warmth.
    public static double ClothingWeightKg(IEnumerable<Gear> worn) => worn.Sum(WeightKg);

    // ---- stacks ----

    public static double Have(Inventory inv, Resource r) => inv.Kg.GetValueOrDefault(r);

    public static void Add(Inventory inv, Resource r, double kg)
    {
        if (kg < 0 || double.IsNaN(kg)) throw new ArgumentOutOfRangeException(nameof(kg), kg, $"Cannot add {kg} kg of {r}");
        if (kg == 0) return;
        inv.Kg[r] = Have(inv, r) + kg;
    }

    /// Takes up to kg; returns what was taken. Old Inventory.ConsumeByWeight.
    public static double Take(Inventory inv, Resource r, double kg)
    {
        if (kg < 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Cannot take a negative amount");
        double have = Have(inv, r);
        double taken = Math.Min(have, kg);
        if (have - taken <= 1e-9) inv.Kg.Remove(r); else inv.Kg[r] = have - taken;
        return taken;
    }

    public static void AddAll(Inventory to, Inventory from)
    {
        foreach (var (r, kg) in from.Kg.OrderBy(kv => kv.Key)) Add(to, r, kg);
        to.Gear.AddRange(from.Gear);
    }

    /// First working tool of this type, or null (old Inventory.GetTool).
    public static Gear? Tool(Inventory inv, ToolType type) =>
        inv.Gear.FirstOrDefault(g => Def(g).Tool == type && Works(g));

    /// Old Inventory.HasCuttingTool.
    public static bool HasCuttingTool(Inventory inv) => Tool(inv, ToolType.Knife) != null || Tool(inv, ToolType.Axe) != null;

    public static double WeightKg(Inventory inv) => inv.Kg.Values.Sum() + inv.Gear.Sum(WeightKg);

    // ---- carrying ----

    /// What this agent carries, or null for an agent that carries nothing (a herd).
    public static Inventory? PackOf(Agent agent) => (agent.Mind as PersonMind)?.Pack;

    public static Inventory Pack(World world, AgentId who) =>
        PackOf(world.Agent(who)) ?? throw new InvalidOperationException($"{who} carries nothing");

    /// Capacity with accessories in the pack (old Inventory.MaxWeightKg).
    public static double CapacityKg(Inventory pack) =>
        BaseCapacityKg + pack.Gear.Where(g => Def(g).Category == GearCategory.Accessory).Sum(g => Def(g).CapacityBonusKg);

    /// Everything on the agent, worn clothing included (old Inventory.CurrentWeightKg).
    public static double CarriedKg(Agent agent) =>
        (PackOf(agent) is { } pack ? WeightKg(pack) : 0) + agent.Members.Sum(b => ClothingWeightKg(b.Worn));

    /// Old AbilityContext.FromFullContext: carried weight over capacity. Zero for an agent that carries nothing.
    public static double EncumbrancePct(Agent agent) =>
        PackOf(agent) is { } pack ? CarriedKg(agent) / CapacityKg(pack) : 0;

    /// Puts what was gained in the pack, up to capacity; the rest drops on the tile's ground stash,
    /// or with dropExcess false is only handed back. A null events list reports nothing. Returns what did not fit (old
    /// InventoryCapacityHelper.CombineAndReport).
    public static Inventory Receive(World world, Agent agent, Inventory gained, List<WorldEvent>? events, bool dropExcess = true)
    {
        var pack = PackOf(agent) ?? throw new InvalidOperationException($"{agent.Id} carries nothing");
        var dropped = new Inventory();
        foreach (var (r, kg) in gained.Kg.OrderBy(kv => kv.Key))
        {
            double room = Math.Max(0, CapacityKg(pack) - CarriedKg(agent));
            double kept = Math.Min(kg, room);
            Add(pack, r, kept);
            if (kg - kept > 1e-9) Add(dropped, r, kg - kept);
        }
        foreach (var g in gained.Gear)
        {
            if (CarriedKg(agent) + WeightKg(g) <= CapacityKg(pack)) pack.Gear.Add(g);
            else dropped.Gear.Add(g);
        }
        if (dropExcess && (dropped.Kg.Count > 0 || dropped.Gear.Count > 0))
        {
            AddAll(StashAt(world, agent.Tile), dropped);
            events?.Add(new Dropped(world.Minute, agent.Id, agent.Tile, Describe(dropped)));
        }
        return dropped;
    }

    /// The tile's stash, created on first use. Camp storage and dropped things are the same pile: unlimited.
    public static Inventory StashAt(World world, TileId tile)
    {
        if (!world.Tiles.TryGetValue(tile, out var state)) world.Tiles[tile] = state = new TileState();
        return state.Stash ??= new Inventory();
    }

    public static string Describe(Inventory inv) =>
        string.Join(", ", inv.Kg.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value:0.##}kg")
            .Concat(inv.Gear.Select(g => Def(g).Name)));

    /// The old starting kit (GameContext.CreateNewGame): worn furs and boots.
    public static List<Gear> StartingClothes() =>
        [Make("worn_fur_chest_wrap"), Make("fur_leg_wraps", 60), Make("worn_hide_boots"), Make("hide_handwraps")];

    /// The old starting pack: a hand drill, 0.9 kg of sticks, four pinches of tinder.
    public static Inventory StartingPack() => new()
    {
        Kg = { [Resource.Kindling] = 0.3 + 0.25 + 0.35, [Resource.Tinder] = 0.05 + 0.04 + 0.05 + 0.04 },
        Gear = { Make("hand_drill") },
    };
}
