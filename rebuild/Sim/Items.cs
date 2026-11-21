namespace Survival;

/// Everything carried by weight. Fuels are resources too: Fire.Props says which ones burn and how,
/// so a log is one thing whether it is in a pack, on a stash or on the fire.
public enum Resource
{
    // tinders and fuel
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

/// One category per resource (fuel and tinder overlaps are Fire.Props).
public enum ResourceCategory { Fuel, Food, Water, Medicine, Material }

/// Clothing is worn on a body; accessories add carrying capacity.
public enum GearCategory { Tool, Clothing, Accessory }

public enum ToolType { Axe, Knife, FireStriker, HandDrill, BowDrill, WaterContainer, Spear, Club, Torch, Needle, Shovel, Tent, Hammerstone }

/// Body surface a garment covers.
public enum EquipSlot { Head, Chest, Legs, Feet, Hands }

public enum WeaponClass { Blade, Blunt, Pierce, Claw, Unarmed }

/// What a weapon does in a fight. Read by combat.
public sealed record Weapon(double Damage, WeaponClass Class, double BlockChance);

/// One row of the resource table (Content/items.json). Kcal and water per kg of food.
public sealed record ResourceDef(ResourceCategory Category, double KcalPerKg = 0, double HydrationMlPerKg = 0);

/// One row of the gear table (Content/items.json): the authored design. Instances carry only their kind and wear.
/// Insulation is in tenths of a clo over the covered part of the body.
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
    /// A tent packed after pitching: the levels it stood at, so it pitches again as it was.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ShelterCover? TentLevels { get; set; }
    /// What a made thing is made of, kilos by resource, merged from its inputs (Making): what decay,
    /// salvage and repair will read. Null for things found or brought, whose parts nobody recorded.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SortedDictionary<Resource, double>? MadeOf { get; set; }
}

/// A recipe worked on and not finished (GLOSSARY: Project), carried as an unfinished item in the pack:
/// its materials went in when the work began, and its progress waits here for the work to go on.
public sealed class MakeProject
{
    public required string Recipe { get; init; }
    /// Minutes of work done, as a whole, warm, dry person would count them (Recipe.Minutes is the total).
    public double MinutesDone { get; set; }
    /// The materials taken: kilos by resource and the items consumed.
    public Inventory Materials { get; init; } = new();
}

/// What one agent carries, or a stash on a tile. Plain data: resources by kg, gear by instance.
public sealed class Inventory
{
    /// Sorted, not a Dictionary: a Dictionary's order after Remove-then-Add differs from its order
    /// freshly loaded, and anything that walks the stacks would then act differently after a load.
    public SortedDictionary<Resource, double> Kg { get; init; } = [];
    public List<Gear> Gear { get; init; } = [];
    /// Unfinished things (Making), one per recipe; null when there are none.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<MakeProject>? Projects { get; set; }
}

/// Items, inventories and what they weigh.
public static class Items
{
    /// Base carrying capacity of a person before accessories.
    public const double BaseCapacityKg = 15;
    const double CloPerInsulationPoint = 10.0;

    /// Every resource's row (Content/items.json).
    public static readonly IReadOnlyDictionary<Resource, ResourceDef> Resources = Content.Tables.Resources;

    /// The gear designs, by kind (Content/items.json).
    public static readonly IReadOnlyDictionary<string, GearDef> GearKinds = Content.Tables.Gear;

    /// The fraction of body surface each slot covers. Sums to 1.
    public static readonly IReadOnlyDictionary<EquipSlot, double> SlotCoverage = Content.Tables.SlotCoverage;

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
    /// Worn things lose a quarter of their weight as they fall apart.
    public static double WeightKg(Gear g) => Def(g).BaseWeightKg * (0.75 + 0.25 * ConditionPct(g));
    /// One use. Returns whether the tool still works afterwards.
    public static bool Use(Gear g)
    {
        if (g.Durability == -1) return true;
        if (g.Durability == 0) return false;
        g.Durability--;
        return g.Durability > 0;
    }

    /// Wear by time in use: one Use every minutesPerUse, however the minutes come, so how a job is
    /// split into sessions cannot change what it costs the tool.
    public static void UseFor(Gear g, double minutes, double minutesPerUse)
    {
        g.UseMinutes += minutes;
        while (g.UseMinutes >= minutesPerUse - 1e-9)
        {
            g.UseMinutes -= minutesPerUse;
            Use(g);
        }
    }

    /// Local clo of one garment.
    public static double CloValue(Gear g) => Def(g).Insulation * CloPerInsulationPoint * ConditionPct(g);

    /// Whole-body clo, area-weighted: a bare slot costs its share.
    public static double ClothingClo(IEnumerable<Gear> worn) => worn.Sum(g => SlotCoverage[Def(g).Slot!.Value] * CloValue(g));
    /// The thermal mass that holds warmth.
    public static double ClothingWeightKg(IEnumerable<Gear> worn) => worn.Sum(WeightKg);

    // ---- stacks ----

    public static double Have(Inventory inv, Resource r) => inv.Kg.GetValueOrDefault(r);

    public static void Add(Inventory inv, Resource r, double kg)
    {
        if (kg < 0 || double.IsNaN(kg)) throw new ArgumentOutOfRangeException(nameof(kg), kg, $"Cannot add {kg} kg of {r}");
        if (kg == 0) return;
        inv.Kg[r] = Have(inv, r) + kg;
    }

    /// Below this, what is left of a stack or of a load is a rounding sliver, never a thing of its own:
    /// it goes with the rest, so no sliver is ever lost.
    public const double SliverKg = 1e-9;

    /// Takes up to kg; returns what was taken. A sliver left behind is taken too, so the returned mass
    /// may exceed kg by up to SliverKg.
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
        // Unfinished things move with the rest: a dead person's half-made spear lies on their stash.
        if (from.Projects is { Count: > 0 } projects) (to.Projects ??= []).AddRange(projects);
    }

    /// First working tool of this type, or null.
    public static Gear? Tool(Inventory inv, ToolType type) =>
        inv.Gear.FirstOrDefault(g => Def(g).Tool == type && Works(g));

    public static bool HasCuttingTool(Inventory inv) => Tool(inv, ToolType.Knife) != null || Tool(inv, ToolType.Axe) != null;

    /// Stacks, gear and the materials in unfinished things.
    public static double WeightKg(Inventory inv) =>
        inv.Kg.Values.Sum() + inv.Gear.Sum(WeightKg) + (inv.Projects?.Sum(p => WeightKg(p.Materials)) ?? 0);

    // ---- carrying ----

    /// What this agent carries, or null for an agent that carries nothing (a herd).
    public static Inventory? PackOf(Agent agent) => (agent.Mind as PersonMind)?.Pack;

    public static Inventory Pack(World world, AgentId who) =>
        PackOf(world.Agent(who)) ?? throw new InvalidOperationException($"{who} carries nothing");

    /// Capacity with accessories in the pack.
    public static double CapacityKg(Inventory pack) =>
        BaseCapacityKg + pack.Gear.Where(g => Def(g).Category == GearCategory.Accessory).Sum(g => Def(g).CapacityBonusKg);

    /// Everything on the agent, worn clothing included.
    public static double CarriedKg(Agent agent) =>
        (PackOf(agent) is { } pack ? WeightKg(pack) : 0) + agent.Members.Sum(b => ClothingWeightKg(b.Worn));

    /// Carried weight over capacity. Zero for an agent that carries nothing.
    public static double EncumbrancePct(Agent agent) =>
        PackOf(agent) is { } pack ? CarriedKg(agent) / CapacityKg(pack) : 0;

    /// Puts what was gained in the pack, up to capacity; the rest drops on the tile's ground stash,
    /// or with dropExcess false is only handed back. A null events list reports nothing. Returns what did not fit.
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
            AddAll(StashAt(world, agent.Tile, new Cause(agent.Id, $"dropped by {Cause.Describe(world, agent.Id)}, pack full")), dropped);
            events?.Add(new Dropped(world.Minute, agent.Id, agent.Tile, Describe(dropped)));
        }
        return dropped;
    }

    /// The tile's stash, created on first use. Camp storage and dropped things are the same pile: unlimited.
    /// made is who is putting something down: putting the first thing on an empty pile begins a new
    /// stash, with this cause and an id of its own.
    public static Inventory StashAt(World world, TileId tile, Cause made)
    {
        var stash = Pile(world, tile, out var state);
        if (stash.Kg.Count == 0 && stash.Gear.Count == 0)
        {
            state.StashCause = made;
            state.StashId = world.NextStashId++;
        }
        return stash;
    }

    /// The tile's stash, created with no cause if there is none: for setting a scene (tests). The game
    /// puts things down through StashAt with a cause.
    public static Inventory StashAt(World world, TileId tile)
    {
        var stash = Pile(world, tile, out var state);
        state.StashId ??= world.NextStashId++;
        return stash;
    }

    static Inventory Pile(World world, TileId tile, out TileState state)
    {
        if (!world.Tiles.TryGetValue(tile, out state!)) world.Tiles[tile] = state = new TileState();
        return state.Stash ??= new Inventory();
    }

    /// Takes up to kg from the tile's stash; a tile left with nothing on it leaves World.Tiles.
    public static double TakeFromStash(World world, TileId tile, Resource r, double kg)
    {
        var state = world.Tiles.GetValueOrDefault(tile);
        if (state?.Stash is not Inventory stash) return 0;
        double taken = Take(stash, r, kg);
        if (stash.Kg.Count == 0 && stash.Gear.Count == 0)
        {
            state.Stash = null;
            state.StashCause = null;
            state.StashId = null;
            Map.Tidy(world, tile);
        }
        return taken;
    }

    public static string Describe(Inventory inv) =>
        string.Join(", ", inv.Kg.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}kg")
            .Concat(inv.Gear.Select(g => Def(g).Name)));

    /// The starting kit: worn furs and boots. New items each call.
    public static List<Gear> StartingClothes() => [.. Content.Tables.StartingClothes.Select(Make)];

    /// The starting pack: a hand drill, 0.9 kg of sticks, four pinches of tinder. New each call.
    public static Inventory StartingPack()
    {
        var kit = Content.Tables.StartingPack;
        var pack = new Inventory { Gear = [.. kit.Gear.Select(Make)] };
        foreach (var (r, pieces) in kit.Kg) pack.Kg[r] = pieces.Aggregate(0.0, (kg, piece) => kg + piece);
        return pack;
    }

    static Gear Make(KitGear k) => Make(k.Kind, k.Durability);
}
