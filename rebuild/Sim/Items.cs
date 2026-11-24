namespace Survival;

/// Fuels are resources too (Fire.Props says how they burn): a log is one thing in a pack, on a pile or on the fire.
public enum Resource
{
    Tinder, BirchBark, Amadou, Usnea, Chaga, Kindling, Pine, Birch, Oak, Bone, Peat,
    RawMeat, CookedMeat, DriedMeat, RawFish, CookedFish, DriedFish, Berries, DriedBerries, Nuts, Roots, Honey,
    Water,
    Stone, Hide, PlantFiber, Sinew, Shale, Flint, Pyrite, ScrapedHide, CuredHide, RawFat, Tallow, Charcoal, Rope,
    Ivory, MammothHide, Feather,
    BirchPolypore, RoseHip, JuniperBerry, WillowBark, PineNeedles, PineResin, SphagnumMoss,
}

public enum ResourceCategory { Fuel, Food, Water, Medicine, Material }

public enum GearCategory { Tool, Clothing, Accessory }

public enum ToolType { Axe, Knife, FireStriker, HandDrill, BowDrill, WaterContainer, Spear, Club, Torch, Needle, Shovel, Tent, Hammerstone }

public enum EquipSlot { Head, Chest, Legs, Feet, Hands }

public enum WeaponClass { Blade, Blunt, Pierce, Claw, Unarmed }

/// Damage is in units of tissue condition (DamageInfo). Thrown is null for what is not made to be thrown.
public sealed record Weapon(double Damage, WeaponClass Class, double BlockChance, double ReachM, ThrownWeapon? Thrown = null);

public sealed record ThrownWeapon(double RangeM, double Accuracy, double Damage);

/// ScentLevel: what a kilo of it gives off to an animal's nose (Scent), stated for every food and nothing else.
public sealed record ResourceDef(ResourceCategory Category, double KcalPerKg = 0, double HydrationMlPerKg = 0, double? ScentLevel = null);

/// Instances carry only their kind and wear. Insulation is in tenths of a clo over the covered part of the body.
/// Capabilities is what carrying it lets someone do to a thing (Survival.Capabilities), stated on every row.
public sealed record GearDef(
    string Name, GearCategory Category, double BaseWeightKg, int MaxDurability, IReadOnlyList<Capability> Capabilities,
    ToolType? Tool = null, Weapon? Weapon = null, EquipSlot? Slot = null,
    double Insulation = 0, double Cushioning = 0, double Toughness = 0, double WaterproofLevel = 0,
    double CapacityBonusKg = 0);

/// One discrete item. Durability -1 never wears out; 0 is broken.
public sealed class Gear
{
    public required string Kind { get; init; }
    public int Durability { get; set; } = -1;
    public int MaxDurability { get; init; } = -1;
    /// Minutes of use not yet charged to Durability (Items.UseFor).
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double UseMinutes { get; set; }
    /// A packed tent keeps the levels it stood at, so it pitches again as it was.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ShelterCover? TentLevels { get; set; }
    /// Kilos by resource, merged from its inputs (Making), for decay, salvage and repair to read. Null for things found or brought.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<Resource, double>? MadeOf { get; set; }
}

/// A recipe worked on and not finished (GLOSSARY: Project); its materials went in when the work began.
public sealed class MakeProject
{
    public required string Recipe { get; init; }
    /// As a whole, warm, dry person would count them (Recipe.Minutes is the total).
    public double MinutesDone { get; set; }
    public Inventory Materials { get; init; } = new();
}

/// What one agent carries, or a pile on a tile.
public sealed class Inventory
{
    /// Sorted, not a Dictionary: a Dictionary's order after Remove-then-Add differs from its order
    /// freshly loaded, and anything that walks the stacks would then act differently after a load.
    public SortedDictionary<Resource, double> Kg { get; init; } = [];
    public List<Gear> Gear { get; init; } = [];
    /// One per recipe; null when there are none.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<MakeProject>? Projects { get; set; }
}

public static class Items
{
    public const double BaseCapacityKg = 15;
    const double CloPerInsulationPoint = 10.0;

    public static readonly IReadOnlyDictionary<Resource, ResourceDef> Resources = Content.Tables.Resources;

    public static readonly IReadOnlyDictionary<string, GearDef> GearKinds = Content.Tables.Gear;

    /// Sums to 1.
    public static readonly IReadOnlyDictionary<EquipSlot, double> SlotCoverage = Content.Tables.SlotCoverage;

    public static GearDef Def(Gear g) =>
        GearKinds.TryGetValue(g.Kind, out var d) ? d : throw new KeyNotFoundException($"No gear kind '{g.Kind}'");

    public static Gear Make(string kind, int? durability = null)
    {
        if (!GearKinds.TryGetValue(kind, out var d)) throw new KeyNotFoundException($"No gear kind '{kind}'");
        int max = durability ?? d.MaxDurability;
        return new Gear { Kind = kind, Durability = max, MaxDurability = max };
    }

    public static double ConditionPct(Gear g) => g.MaxDurability <= 0 ? 1.0 : g.Durability <= 0 ? 0.0 : (double)g.Durability / g.MaxDurability;
    public static bool Works(Gear g) => g.Durability != 0;
    /// Worn things lose a quarter of their weight as they fall apart.
    public static double WeightKg(Gear g) => Def(g).BaseWeightKg * (0.75 + 0.25 * ConditionPct(g));
    /// Returns whether the tool still works afterwards.
    public static bool Use(Gear g)
    {
        if (g.Durability == -1) return true;
        if (g.Durability == 0) return false;
        g.Durability--;
        return g.Durability > 0;
    }

    /// One Use every minutesPerUse, so splitting a job into sessions cannot change what it costs the tool.
    public static void UseFor(Gear g, double minutes, double minutesPerUse)
    {
        g.UseMinutes += minutes;
        while (g.UseMinutes >= minutesPerUse - 1e-9)
        {
            g.UseMinutes -= minutesPerUse;
            Use(g);
        }
    }

    public static double CloValue(Gear g) => Def(g).Insulation * CloPerInsulationPoint * ConditionPct(g);

    /// Area-weighted: a bare slot costs its share.
    public static double ClothingClo(IEnumerable<Gear> worn) => worn.Sum(g => SlotCoverage[Def(g).Slot!.Value] * CloValue(g));
    /// The thermal mass that holds warmth.
    public static double ClothingWeightKg(IEnumerable<Gear> worn) => worn.Sum(WeightKg);

    public static double Have(Inventory inv, Resource r) => inv.Kg.GetValueOrDefault(r);

    public static void Add(Inventory inv, Resource r, double kg)
    {
        if (kg < 0 || double.IsNaN(kg)) throw new ArgumentOutOfRangeException(nameof(kg), kg, $"Cannot add {kg} kg of {r}");
        if (kg == 0) return;
        inv.Kg[r] = Have(inv, r) + kg;
    }

    /// Below this, what is left of a stack or of a load is a rounding sliver that goes with the rest.
    public const double SliverKg = 1e-9;

    /// Returns what was taken; a sliver left behind is taken too, so it may exceed kg by up to SliverKg.
    public static double Take(Inventory inv, Resource r, double kg)
    {
        if (kg < 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Cannot take a negative amount");
        double have = Have(inv, r);
        double taken = Math.Min(have, kg);
        if (have - taken <= SliverKg) { inv.Kg.Remove(r); return have; }
        inv.Kg[r] = have - taken;
        return taken;
    }

    public static void AddAll(Inventory to, Inventory from)
    {
        foreach (var (r, kg) in from.Kg.OrderBy(kv => kv.Key)) Add(to, r, kg);
        to.Gear.AddRange(from.Gear);
        // A dead person's half-made spear lies on their pile.
        if (from.Projects is { Count: > 0 } projects) (to.Projects ??= []).AddRange(projects);
    }

    public static Gear? Tool(Inventory inv, ToolType type) =>
        inv.Gear.FirstOrDefault(g => Def(g).Tool == type && Works(g));

    public static bool HasCuttingTool(Inventory inv) => Tool(inv, ToolType.Knife) != null || Tool(inv, ToolType.Axe) != null;

    public static double WeightKg(Inventory inv) =>
        inv.Kg.Values.Sum() + inv.Gear.Sum(WeightKg) + (inv.Projects?.Sum(p => WeightKg(p.Materials)) ?? 0);

    /// Null for an agent that carries nothing (a herd).
    public static Inventory? PackOf(Agent agent) => (agent.Mind as PersonMind)?.Pack;

    public static Inventory Pack(World world, AgentId who) =>
        PackOf(world.Agent(who)) ?? throw new InvalidOperationException($"{who} carries nothing");

    public static double CapacityKg(Inventory pack) =>
        BaseCapacityKg + pack.Gear.Where(g => Def(g).Category == GearCategory.Accessory).Sum(g => Def(g).CapacityBonusKg);

    /// Worn clothing included.
    public static double CarriedKg(Agent agent) =>
        (PackOf(agent) is { } pack ? WeightKg(pack) : 0) + agent.Members.Sum(b => ClothingWeightKg(b.Worn));

    public static double EncumbrancePct(Agent agent) =>
        PackOf(agent) is { } pack ? CarriedKg(agent) / CapacityKg(pack) : 0;

    /// What does not fit drops on the tile's pile, or with dropExcess false is only handed back. Null events reports nothing.
    /// Returns what did not fit.
    public static Inventory Receive(World world, Agent agent, Inventory gained, List<WorldEvent>? events, bool dropExcess = true)
    {
        var pack = PackOf(agent) ?? throw new InvalidOperationException($"{agent.Id} carries nothing");
        var dropped = new Inventory();
        foreach (var (r, kg) in gained.Kg.OrderBy(kv => kv.Key))
        {
            double room = Math.Max(0, CapacityKg(pack) - CarriedKg(agent));
            double kept = Math.Min(kg, room);
            if (kg - kept <= SliverKg) kept = kg; // a sliver over capacity rides with the rest
            Add(pack, r, kept);
            if (kg - kept > 0) Add(dropped, r, kg - kept);
        }
        foreach (var g in gained.Gear)
        {
            if (CarriedKg(agent) + WeightKg(g) <= CapacityKg(pack)) pack.Gear.Add(g);
            else dropped.Gear.Add(g);
        }
        if (dropExcess && (dropped.Kg.Count > 0 || dropped.Gear.Count > 0))
        {
            AddAll(PileAt(world, agent.Tile, new Cause(agent.Id, $"dropped by {Cause.Describe(world, agent.Id)}, pack full")), dropped);
            events?.Add(new Dropped(world.Minute, agent.Id, agent.Tile, Describe(dropped)));
        }
        return dropped;
    }

    /// Camp storage and dropped things are the same pile: unlimited. The first thing put on an empty pile begins a new pile with this cause and its own id.
    public static Inventory PileAt(World world, TileId tile, Cause made)
    {
        var pile = PileOrNew(world, tile, out var state);
        if (pile.Kg.Count == 0 && pile.Gear.Count == 0)
        {
            state.PileCause = made;
            state.PileId = world.NextPileId++;
        }
        return pile;
    }

    /// For setting a scene (tests): the game puts things down through PileAt with a cause.
    public static Inventory PileAt(World world, TileId tile)
    {
        var pile = PileOrNew(world, tile, out var state);
        state.PileId ??= world.NextPileId++;
        return pile;
    }

    static Inventory PileOrNew(World world, TileId tile, out TileState state)
    {
        if (!world.Tiles.TryGetValue(tile, out state!)) world.Tiles[tile] = state = new TileState();
        return state.Pile ??= new Inventory();
    }

    /// A tile left with nothing on it leaves World.Tiles.
    public static double TakeFromPile(World world, TileId tile, Resource r, double kg)
    {
        var state = world.Tiles.GetValueOrDefault(tile);
        if (state?.Pile is not Inventory pile) return 0;
        double taken = Take(pile, r, kg);
        if (pile.Kg.Count == 0 && pile.Gear.Count == 0)
        {
            state.Pile = null;
            state.PileCause = null;
            state.PileId = null;
            Map.Tidy(world, tile);
        }
        return taken;
    }

    public static string Describe(Inventory inv) =>
        string.Join(", ", inv.Kg.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}kg")
            .Concat(inv.Gear.Select(g => Def(g).Name)));

    /// New items each call, as is StartingPack.
    public static List<Gear> StartingClothes() => [.. Content.Tables.StartingClothes.Select(Make)];

    public static Inventory StartingPack()
    {
        var kit = Content.Tables.StartingPack;
        var pack = new Inventory { Gear = [.. kit.Gear.Select(Make)] };
        foreach (var (r, pieces) in kit.Kg) pack.Kg[r] = pieces.Aggregate(0.0, (kg, piece) => kg + piece);
        return pack;
    }

    static Gear Make(KitGear k) => Make(k.Kind, k.Durability);
}
