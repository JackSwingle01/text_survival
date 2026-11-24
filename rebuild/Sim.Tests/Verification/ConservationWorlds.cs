using Survival;

namespace Survival.Tests.Verification;

/// Worlds and ledgers shared by the conservation laws (documentation/verification.md).
static class ConservationWorlds
{
    public static readonly AgentId P = AgentId.Player;

    /// Seed 17 with nobody in it but the player, saved once and loaded on the seed's shared map (TestWorlds).
    static readonly Lazy<string> Template = new(() =>
    {
        var w = TestWorlds.New(17);
        foreach (var id in w.Agents.Keys.Where(id => id != P).ToList()) Agents.Remove(w, id);
        return Save.ToJson(w);
    });

    static World Load() => TestWorlds.Load(Template.Value);

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
        foreach (var a in w.Agents.Values) if (Items.PackOf(a) is { } pack) Add(pack.Kg);
        foreach (var ts in w.Tiles.Values)
        {
            if (ts.Pile is { } pile) Add(pile.Kg);
            foreach (var c in ts.Carcasses ?? []) Add(c.Pending);
        }
        return t;
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
