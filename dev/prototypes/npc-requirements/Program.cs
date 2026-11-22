// PROTOTYPE, throwaway: dotnet run -c Release [-- trace]
using NpcReq;
using Survival;

bool trace = args.Contains("trace");
string? only = args.FirstOrDefault(a => a.StartsWith("only="))?[5..];

static void PickedOver(World w, AgentId who, TileId camp)
{
    // The forest around camp was worked hard yesterday; the NPC remembers it as it was before.
    var beliefs = Knowledge.Of(w.Agent(who));
    for (int dx = -2; dx <= 2; dx++)
        for (int dy = -2; dy <= 2; dy++)
        {
            var t = new TileId(camp.X + dx, camp.Y + dy);
            var pools = Pools.At(w, t);
            int i = pools.ToList().FindIndex(p => p is ForagePool);
            if (i < 0) continue;
            for (int k = 0; k < 4; k++) Pools.Forage(w, new PoolId(t, i), 1.0, w.Rng("work", who));
            if (!beliefs.Places.TryGetValue(t, out var mem)) beliefs.Places[t] = mem = new TileMemory();
            mem.ForageDensity = ((ForagePool)pools[i]).Density;
            mem.VisitedMinute = w.Minute - 3 * 1440;
            mem.SeenMinute = w.Minute - 3 * 1440;
        }
}

static void NoMakings(World w, AgentId who, TileId camp)
{
    var pack = Items.Pack(w, who);
    pack.Kg.Remove(Resource.Tinder);
    pack.Kg.Remove(Resource.Kindling);
    Items.Add(Items.StashAt(w, camp, new Cause(who, "prototype")), Resource.Birch, 8);
}

var scenarios = new[]
{
    new Scenario("1 forest camp", "forest", Snowy: false),
    new Scenario("1b forest camp, no axe", "forest", Snowy: false, Axe: false),
    new Scenario("2 plain camp, trees 3 tiles off", "plain", Snowy: false),
    new Scenario("2b plain camp, no axe", "plain", Snowy: false, Axe: false),
    new Scenario("3 snowy night (plain, trees 3 off)", "plain", Snowy: true),
    new Scenario("5 picked-over forest, no axe", "forest", Snowy: false, PickedOver, Axe: false),
    new Scenario("6 no tinder or kindling, 8 kg birch stashed", "forest", Snowy: false, NoMakings),
};
var configs = new List<(Crude, bool)>
{
    (Crude.A, true), (Crude.B, true),
    (Crude.B with { Name = "B, fire 1 kg = 1 h", FireByFuelType = false }, true),
    (Crude.B with { Name = "B, yield without thinning", YieldDepletes = false }, true),
    (Crude.B with { Name = "B, source by travel only", CostIncludesGather = false }, true),
    (Crude.B with { Name = "B, no shared trips", ShareTrips = false }, true),
    (Crude.B with { Name = "B, fire stays at home" }, false),
};
string? cfg = args.FirstOrDefault(a => a.StartsWith("cfg="))?[4..];
int seed = int.Parse(args.FirstOrDefault(a => a.StartsWith("seed="))?[5..] ?? "5");
Harness.Seed = seed;
foreach (var (crude, site) in configs.Where(c => cfg == null || c.Item1.Name.StartsWith(cfg)))
    foreach (var sc in scenarios.Where(s => only == null || s.Name.StartsWith(only)))
    {
        foreach (var line in Harness.Run(sc, crude, trace, args.Contains("bench"), site)) Console.WriteLine(line);
        Console.WriteLine();
    }
