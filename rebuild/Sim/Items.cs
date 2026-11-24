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

public enum ResourceForm { Discrete, Continuous }

/// GLOSSARY: Resource, Piece. Form is null only in a row the loader refuses.
public sealed record ResourceDef(ResourceCategory Category, double KcalPerKg = 0, double HydrationMlPerKg = 0,
    ResourceForm? Form = null, PieceRange? PieceKg = null)
{
    public bool Discrete => Form == ResourceForm.Discrete;
    public double MinPieceKg => PieceKg?.Min ?? double.NaN;
    public double MaxPieceKg => PieceKg?.Max ?? double.NaN;
}

public readonly record struct PieceRange(double Min, double Max);

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
    /// The parts record: the actual pieces and amounts that went in (Making). Null for things found or brought.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Inventory? MadeOf { get; set; }
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
    /// Continuous, kilos by resource. Sorted: a Dictionary's order after Remove-then-Add differs from its order freshly loaded.
    public SortedDictionary<Resource, double> Amounts { get; init; } = [];
    /// Discrete: each piece's kilos, lightest first, never an empty list. Both are changed only through Items.
    public SortedDictionary<Resource, List<double>> Pieces { get; init; } = [];
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

    public static bool IsDiscrete(Resource r) => Resources[r].Discrete;

    public static double Have(Inventory inv, Resource r) =>
        inv.Pieces.TryGetValue(r, out var pieces) ? pieces.Sum() : inv.Amounts.GetValueOrDefault(r);

    /// Pieces of a discrete r held; 0 for a continuous one.
    public static int Count(Inventory inv, Resource r) => inv.Pieces.TryGetValue(r, out var pieces) ? pieces.Count : 0;

    public static double? LightestKg(Inventory inv, Resource r) => inv.Pieces.TryGetValue(r, out var pieces) ? pieces[0] : null;

    /// Every resource held with its kilos, in resource order, discrete and continuous alike.
    public static IEnumerable<(Resource Resource, double Kg)> Totals(Inventory inv) =>
        inv.Amounts.Select(kv => (kv.Key, kv.Value)).Concat(inv.Pieces.Select(kv => (kv.Key, kv.Value.Sum()))).OrderBy(x => x.Item1);

    public static SortedDictionary<Resource, double> KgByResource(Inventory inv) => new(Totals(inv).ToDictionary(x => x.Resource, x => x.Kg));

    public static bool HasResources(Inventory inv) => inv.Amounts.Count > 0 || inv.Pieces.Count > 0;

    /// No resources and no gear (projects aside).
    public static bool IsEmpty(Inventory inv) => !HasResources(inv) && inv.Gear.Count == 0;

    /// A continuous amount. A discrete resource comes in pieces (AddPiece).
    public static void Add(Inventory inv, Resource r, double kg)
    {
        if (IsDiscrete(r)) throw new InvalidOperationException($"{r} is discrete: add it piece by piece");
        if (kg < 0 || double.IsNaN(kg)) throw new ArgumentOutOfRangeException(nameof(kg), kg, $"Cannot add {kg} kg of {r}");
        if (kg == 0) return;
        inv.Amounts[r] = inv.Amounts.GetValueOrDefault(r) + kg;
    }

    const double RangeSlackKg = 1e-9;

    public static bool InRange(Resource r, double kg) =>
        Resources[r] is { Discrete: true } d && kg >= d.MinPieceKg - RangeSlackKg && kg <= d.MaxPieceKg + RangeSlackKg;

    public static void AddPiece(Inventory inv, Resource r, double kg)
    {
        if (!IsDiscrete(r)) throw new InvalidOperationException($"{r} is continuous: it has no pieces");
        if (!InRange(r, kg)) throw new ArgumentOutOfRangeException(nameof(kg), kg, $"A piece of {r} weighs {Resources[r].MinPieceKg}-{Resources[r].MaxPieceKg} kg");
        if (!inv.Pieces.TryGetValue(r, out var pieces)) inv.Pieces[r] = pieces = [];
        int at = pieces.BinarySearch(kg);
        pieces.Insert(at < 0 ? ~at : at, kg);
    }

    public static void AddPieces(Inventory inv, Resource r, IEnumerable<double> pieces)
    {
        foreach (var kg in pieces) AddPiece(inv, r, kg);
    }

    /// Below this, what is left of an amount or of a load is a rounding sliver that goes with the rest.
    public const double SliverKg = 1e-9;

    /// A continuous amount: returns what was taken; a sliver left behind is taken too, so it may exceed kg by up to SliverKg.
    public static double Take(Inventory inv, Resource r, double kg)
    {
        if (IsDiscrete(r)) throw new InvalidOperationException($"{r} is discrete: take it by the piece");
        if (kg < 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Cannot take a negative amount");
        double have = inv.Amounts.GetValueOrDefault(r);
        double taken = Math.Min(have, kg);
        if (have - taken <= SliverKg) { inv.Amounts.Remove(r); return have; }
        inv.Amounts[r] = have - taken;
        return taken;
    }

    public static List<double> TakePieces(Inventory inv, Resource r, int n)
    {
        if (!inv.Pieces.TryGetValue(r, out var pieces) || n <= 0) return [];
        var taken = pieces.GetRange(0, Math.Min(n, pieces.Count));
        pieces.RemoveRange(0, taken.Count);
        if (pieces.Count == 0) inv.Pieces.Remove(r);
        return taken;
    }

    /// The one rule for taking by weight. A continuous amount moves min(kg, limitKg, held). A discrete one moves
    /// whole pieces, lightest first: always the lightest if it fits limitKg (so asking for less than a piece
    /// gives one piece), then more while the load stays within both kg and limitKg. Returns the kilos moved.
    public static double Move(Inventory from, Inventory to, Resource r, double kg, double limitKg = double.PositiveInfinity)
    {
        if (kg < 0) throw new ArgumentOutOfRangeException(nameof(kg), kg, "Cannot move a negative amount");
        if (!IsDiscrete(r))
        {
            double amount = Math.Min(kg, Math.Max(0, limitKg));
            if (amount <= 0 || Have(from, r) <= 0) return 0;
            double taken = Take(from, r, amount);
            Add(to, r, taken);
            return taken;
        }
        if (kg <= 0 || !from.Pieces.TryGetValue(r, out var pieces)) return 0;
        int n = 0;
        double load = 0;
        while (n < pieces.Count && load + pieces[n] <= limitKg + RangeSlackKg && (n == 0 || load + pieces[n] <= kg + RangeSlackKg))
            load += pieces[n++];
        AddPieces(to, r, TakePieces(from, r, n));
        return load;
    }

    public static double MoveAll(Inventory from, Inventory to, Resource r) => Move(from, to, r, double.PositiveInfinity);

    /// What Move(kg, limitKg) would move, moving nothing.
    public static double WouldMoveKg(Inventory from, Resource r, double kg, double limitKg = double.PositiveInfinity)
    {
        var copy = new Inventory();
        if (from.Pieces.TryGetValue(r, out var pieces)) copy.Pieces[r] = [.. pieces];
        else if (from.Amounts.TryGetValue(r, out var amount)) copy.Amounts[r] = amount;
        return Move(copy, new Inventory(), r, kg, limitKg);
    }

    public static void AddAll(Inventory to, Inventory from)
    {
        foreach (var (r, kg) in from.Amounts.OrderBy(kv => kv.Key)) Add(to, r, kg);
        foreach (var (r, pieces) in from.Pieces.OrderBy(kv => kv.Key)) AddPieces(to, r, pieces);
        to.Gear.AddRange(from.Gear);
        // A dead person's half-made spear lies on their pile.
        if (from.Projects is { Count: > 0 } projects) (to.Projects ??= []).AddRange(projects);
    }

    public static Inventory CopyResources(Inventory inv) => new()
    {
        Amounts = new SortedDictionary<Resource, double>(inv.Amounts),
        Pieces = new SortedDictionary<Resource, List<double>>(inv.Pieces.ToDictionary(kv => kv.Key, kv => kv.Value.ToList())),
    };

    public static Gear? Tool(Inventory inv, ToolType type) =>
        inv.Gear.FirstOrDefault(g => Def(g).Tool == type && Works(g));

    public static bool HasCuttingTool(Inventory inv) => Tool(inv, ToolType.Knife) != null || Tool(inv, ToolType.Axe) != null;

    public static double WeightKg(Inventory inv) =>
        Totals(inv).Sum(x => x.Kg) + inv.Gear.Sum(WeightKg) + (inv.Projects?.Sum(p => WeightKg(p.Materials)) ?? 0);

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
        var rest = CopyResources(gained);
        foreach (var (r, kg) in Totals(gained).ToList())
        {
            double room = Math.Max(0, CapacityKg(pack) - CarriedKg(agent));
            // A sliver over capacity rides with the rest of an amount; a piece fits whole or not at all.
            Move(rest, pack, r, kg, IsDiscrete(r) ? room : kg - room <= SliverKg ? kg : room);
            MoveAll(rest, dropped, r);
        }
        foreach (var g in gained.Gear)
        {
            if (CarriedKg(agent) + WeightKg(g) <= CapacityKg(pack)) pack.Gear.Add(g);
            else dropped.Gear.Add(g);
        }
        if (dropExcess && !IsEmpty(dropped))
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
        if (IsEmpty(pile))
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

    /// Move's rule, from the tile's pile into an inventory; a tile left with nothing on it leaves World.Tiles.
    public static double MoveFromPile(World world, TileId tile, Inventory to, Resource r, double kg, double limitKg = double.PositiveInfinity)
    {
        var state = world.Tiles.GetValueOrDefault(tile);
        if (state?.Pile is not Inventory pile) return 0;
        double moved = Move(pile, to, r, kg, limitKg);
        TidyPile(world, tile);
        return moved;
    }

    public static void TidyPile(World world, TileId tile)
    {
        if (world.Tiles.GetValueOrDefault(tile) is not { Pile: Inventory pile } state || !IsEmpty(pile)) return;
        state.Pile = null;
        state.PileCause = null;
        state.PileId = null;
        Map.Tidy(world, tile);
    }

    public static string Describe(Inventory inv) =>
        string.Join(", ", Totals(inv).Select(x => Names.Amount(x.Resource, x.Kg, Count(inv, x.Resource)))
            .Concat(inv.Gear.Select(g => Def(g).Name)));

    /// New items each call, as is StartingPack.
    public static List<Gear> StartingClothes() => [.. Content.Tables.StartingClothes.Select(Make)];

    public static Inventory StartingPack()
    {
        var kit = Content.Tables.StartingPack;
        var pack = new Inventory { Gear = [.. kit.Gear.Select(Make)] };
        foreach (var (r, pieces) in kit.Kg)
            if (IsDiscrete(r)) AddPieces(pack, r, pieces);
            else Add(pack, r, pieces.Aggregate(0.0, (kg, piece) => kg + piece));
        return pack;
    }

    static Gear Make(KitGear k) => Make(k.Kind, k.Durability);
}
