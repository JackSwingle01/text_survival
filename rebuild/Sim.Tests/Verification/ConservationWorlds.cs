using Survival;

namespace Survival.Tests.Verification;

/// Worlds and ledgers shared by the conservation laws (documentation/verification.md).
static class ConservationWorlds
{
    public static readonly AgentId P = AgentId.Player;

    static World Load() => TestWorlds.Bare(17);

    /// The bare world under a pinned sky at this air temperature (°C), still at minute 0.
    public static World Bare(double tempC)
    {
        var w = Load();
        w.FixedWeather = new(0, WeatherCondition.Cloudy, tempC, 0.1, 0, 0.8, WindDirection.North);
        return w;
    }

    /// The bare world under the seed's own weather: the sky every system reads in the game.
    public static World Seeded() => Load();

    public static TileId Camp(World w) => Map.Of(w).Camp;

    /// Kilograms of each resource held as items: every pack, every pile, and every cut piece
    /// waiting on a carcass. Carcass ledgers (uncut Remaining) and fires are not items.
    public static SortedDictionary<Resource, double> ItemKg(World w)
    {
        var t = new SortedDictionary<Resource, double>();
        void Add(IEnumerable<KeyValuePair<Resource, double>> kgs)
        {
            foreach (var (r, kg) in kgs) t[r] = t.GetValueOrDefault(r) + kg;
        }
        foreach (var a in w.Agents.Values) if (Items.PackOf(a) is { } pack) Add(Items.KgByResource(pack));
        foreach (var ts in w.Tiles.Values)
        {
            if (ts.Pile is { } pile) Add(Items.KgByResource(pile));
            foreach (var c in ts.Carcasses ?? []) Add(c.Pending);
        }
        return t;
    }

    /// Every inventory a piece can be in: packs, piles, projects in either and on tiles, and the parts records of all
    /// gear, wherever it is.
    public static IEnumerable<Inventory> AllInventories(World w)
    {
        var all = new List<Inventory>();
        void With(Inventory inv)
        {
            all.Add(inv);
            foreach (var p in inv.Projects ?? []) With(p.Materials);
            foreach (var g in inv.Gear) if (g.MadeOf is { } made) With(made);
        }
        foreach (var a in w.Agents.Values)
        {
            if (Items.PackOf(a) is { } pack) With(pack);
            foreach (var b in a.Members) foreach (var g in b.Worn) if (g.MadeOf is { } made) With(made);
        }
        foreach (var ts in w.Tiles.Values) if (ts.Pile is { } pile) With(pile);
        foreach (var p in w.Projects.Values) With(p.Materials);
        return all;
    }

    /// A pack never holds part of a piece: every discrete resource is held as whole pieces, each inside its
    /// kind's range, lightest first, and no continuous one is held as pieces. Returns what is wrong, or null.
    public static string? BrokenPiece(World w)
    {
        foreach (var inv in AllInventories(w))
        {
            foreach (var r in inv.Amounts.Keys) if (Items.IsDiscrete(r)) return $"{r} is discrete but held as an amount";
            foreach (var (r, pieces) in inv.Pieces)
            {
                if (!Items.IsDiscrete(r)) return $"{r} is continuous but held as pieces";
                if (pieces.Count == 0) return $"an empty list of {r}";
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (!Items.InRange(r, pieces[i])) return $"a piece of {r} weighs {pieces[i]} kg, outside {Items.Resources[r].MinPieceKg}-{Items.Resources[r].MaxPieceKg}";
                    if (i > 0 && pieces[i] < pieces[i - 1]) return $"the pieces of {r} are out of order";
                }
            }
        }
        return null;
    }

    /// Every piece of gear, by kind: in packs, worn by any member of any agent, and on piles.
    public static SortedDictionary<string, int> GearCount(World w)
    {
        var t = new SortedDictionary<string, int>();
        void Add(IEnumerable<Gear> gs) { foreach (var g in gs) t[g.Kind] = t.GetValueOrDefault(g.Kind) + 1; }
        foreach (var a in w.Agents.Values)
        {
            if (Items.PackOf(a) is { } pack) Add(pack.Gear);
            foreach (var b in a.Members) Add(b.Worn);
        }
        foreach (var ts in w.Tiles.Values) if (ts.Pile is { } pile) Add(pile.Gear);
        return t;
    }

    public static void AddTo(SortedDictionary<Resource, double> d, Resource r, double kg) => d[r] = d.GetValueOrDefault(r) + kg;

    public static string Show(IReadOnlyDictionary<Resource, double> d) =>
        string.Join(", ", d.Where(kv => Math.Abs(kv.Value) > 1e-12).Select(kv => $"{kv.Key} {kv.Value:R}"));

    /// Keeps the body far from every threshold, so a long run of steps never stops for hunger,
    /// thirst, exhaustion or death. Touches no item.
    public static void TopUp(BodyState b)
    {
        b.CaloriesKcal = 1800;
        b.HydrationMl = 3500;
        b.EnergyMinutes = Body.MaxEnergyMinutes;
        b.CoreTempF = Body.BaseCoreTempF;
    }
}
