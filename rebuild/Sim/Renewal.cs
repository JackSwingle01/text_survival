namespace Survival;

/// Herd behaviour rates are a model, so they live in code beside the behaviours, not in content.
/// Season days are Weather.DayOfYear, inclusive. Over capacity the birth rate runs backwards (Thin).
/// CapacityPerScore: how many the map carries per unit of its score (ValleyScore). Arrival: null for what never comes in.
public sealed record RenewalRow(int SeasonStartDay, int SeasonEndDay, double BirthsPerHeadPerDay, double CapacityPerScore, ArrivalRow? Arrival);

/// A group coming into the valley while the valley holds fewer than ValleyPerScore members per unit of its score.
public sealed record ArrivalRow(double ValleyPerScore, int MinMembers, int MaxMembers);

public static partial class Fauna
{
    /// Judgments read off the curves of a simulated year; CapacityPerScore is what the herds' old lands carried, summed.
    static RenewalRow? RenewalOf(Species s) => s switch
    {
        Species.Caribou => new(135, 190, 0.030, 0.0035, new(0.0035, 5, 15)),
        Species.Bison => new(130, 190, 0.020, 0.011, new(0.0066, 3, 8)),
        Species.Megaloceros => new(140, 190, 0.020, 0.0026, new(0.0014, 1, 3)),
        Species.Mammoth => new(120, 200, 0.004, 0.0019, null),
        Species.Wolf => new(110, 150, 0.020, 1.0 / 2500, new(1.0 / 3500, 3, 8)),
        Species.Hyena => new(110, 170, 0.015, 1.0 / 1150, new(1.0 / 3200, 3, 6)),
        Species.Raven => new(100, 180, 0.020, 0.0057, new(0.0040, 3, 6)),
        Species.Fox => new(100, 150, 0.010, 0.0005, new(0.0008, 1, 2)),
        Species.Bear => new(100, 140, 0.004, 0.0009, new(0.0012, 1, 1)),
        Species.CaveBear => new(100, 140, 0.004, 0.0012, null), // its den is a cave
        // A dead megafauna hunter stays dead and none arrive. People and small game are not herds.
        Species.SaberTooth or Species.Human or Species.Hare => null,
    };

    const double ArrivalsPerDay = 0.05; // at the valley's full deficit

    /// At midnight each animal rolls on its own stream against its species' capacity and count at midnight; one in a fight does not.
    public static void Renew(World world, List<WorldEvent> events)
    {
        if (Weather.MinuteOfDay(world.Minute) != 0) return;
        int day = Weather.DayOfYear(world.Minute);
        var capacities = new Dictionary<Species, double>();
        var counts = new Dictionary<Species, int>();
        foreach (var a in world.Agents.Values)
            if (a.Mind is HerdMind && Sim.IsAlive(a)) counts[a.Species] = counts.GetValueOrDefault(a.Species) + 1;
        foreach (var herd in world.Agents.Values.Where(a => a.Mind is HerdMind).ToList())
        {
            if (!world.Agents.ContainsKey(herd.Id) || Combat.EncounterOf(world, herd.Id) != null || RenewalOf(herd.Species) is not RenewalRow row) continue;
            var rng = world.Rng("renewal", herd.Id);
            if (!capacities.TryGetValue(herd.Species, out double capacity)) capacities[herd.Species] = capacity = Capacity(world, herd.Species);
            double current = counts.GetValueOrDefault(herd.Species);
            if (current > capacity && Thins(herd.Species))
            {
                Thin(world, herd, row.BirthsPerHeadPerDay * (current - capacity) / current, rng, events);
                continue;
            }
            if (day < row.SeasonStartDay || day > row.SeasonEndDay || capacity <= 0) continue;
            double p = row.BirthsPerHeadPerDay * Deficit(capacity, current);
            if (capacity - current >= 1 && rng.NextDouble() < p) Born(world, herd);
        }
        Arrive(world);
    }

    static double Deficit(double capacity, double current) => capacity <= 0 ? 0 : Math.Max(0, (capacity - current) / capacity);

    /// What lives on kills follows them down; the rest only stop giving birth when full.
    static bool Thins(Species s) => BehaviorOf(s) is HerdBehavior.Pack or HerdBehavior.Scavenger or HerdBehavior.Opportunist;

    /// The birth rate run backwards, so a species falls toward capacity as fast as it would grow toward it.
    static void Thin(World world, Agent herd, double p, Rng rng, List<WorldEvent> events)
    {
        if (rng.NextDouble() < p) Lose(world, herd, events);
    }

    static void Lose(World world, Agent animal, List<WorldEvent> events)
    {
        var body = animal.Body;
        Combat.LeaveCarcass(world, animal.Tile, animal.Species, body, new Cause(null, "starvation"));
        events.Add(new Killed(world.Minute, animal.Id, animal.Species, body.WeightKg, animal.Tile, By: null));
        Agents.Remove(world, animal.Id);
    }

    static void Born(World world, Agent mother)
    {
        var young = world.Agent(Agents.Spawn(world, mother.Species, mother.Tile, MindOf(mother).Den));
        Inherit(world, mother, young);
    }

    /// Beyond the farthest anyone can see, so nobody sees a herd appear from nothing.
    public const int ArrivalClearanceTiles = Sight.MaxTiles;

    /// Species in order, each with chance ArrivalsPerDay x the valley's deficit; no more members than the valley has room for.
    static void Arrive(World world)
    {
        var rng = world.Rng("arrivals");
        foreach (var species in Enum.GetValues<Species>())
        {
            if (RenewalOf(species)?.Arrival is not ArrivalRow a) continue;
            double capacity = ValleyCapacity(world, species);
            double current = world.Agents.Values.Count(x => x.Species == species && x.Mind is HerdMind && Sim.IsAlive(x));
            if (rng.NextDouble() >= ArrivalsPerDay * Deficit(capacity, current)) continue;
            var land = ArrivalLand(world, species);
            if (land.Count == 0) continue;
            var start = land[rng.NextInt(land.Count)];
            // No more than either number allows: what draws them in, nor what the valley carries of them.
            double room = Math.Min(capacity, Capacity(world, species)) - current;
            int members = Math.Min(a.MinMembers + rng.NextInt(a.MaxMembers - a.MinMembers + 1), (int)Math.Floor(room));
            if (members < a.MinMembers || Reach(Map.Of(world), start, land, SpacingTiles).Count < 3) continue;
            Agents.SpawnGroup(world, species, start, members);
        }
    }

    /// Open land out of everyone's sight, and for a predator off the ground around the packs' dens (as Populate spaces them).
    static List<TileId> ArrivalLand(World world, Species species)
    {
        var people = world.Agents.Values.Where(a => a.Mind is PersonMind && Sim.IsAlive(a)).Select(a => a.Tile).ToList();
        var dens = IsPredator(species)
            ? world.Agents.Values.Where(a => a.Mind is HerdMind { Den: not null } && BehaviorOf(a.Species) == HerdBehavior.Pack && Sim.IsAlive(a))
                .Select(a => MindOf(a).Den!.Value).Distinct().ToList()
            : [];
        return [.. OpenLand(world).Where(t => dens.All(d => MapGen.Manhattan(d, t) > SpacingTiles) && people.All(p => MapGen.Manhattan(p, t) > ArrivalClearanceTiles))];
    }

    public static double ValleyCapacity(World world, Species species) =>
        RenewalOf(species)?.Arrival is ArrivalRow a ? ValleyScore(world, species) * a.ValleyPerScore : 0;

    /// Generation's forage, so cached and never saved; for a meat-eater the living prey now.
    static double ValleyScore(World world, Species species)
    {
        if (DietOf(species) == Diet.Carnivore)
            return world.Agents.Values.Where(a => a.Mind is HerdMind && IsQuarry(a.Species) && Sim.IsAlive(a)).Sum(a => a.Body.WeightKg);
        var cache = world.ValleyForageCache ??= [];
        if (!cache.TryGetValue(species, out double score)) cache[species] = score = OpenLand(world).Sum(t => ForageScore(world, t, species));
        return score;
    }

    /// Fixed at generation, so worked out once and never saved.
    static List<TileId> OpenLand(World world)
    {
        if (world.OpenLandCache is { } land) return land;
        var m = Map.Of(world);
        return world.OpenLandCache = [.. InteriorTiles(m).Where(t => MapGen.Manhattan(t, m.Camp) > SafeZoneTiles)];
    }

    public static bool IsMegafauna(Species s) => s is Species.Mammoth or Species.SaberTooth;

    /// How many of the species the map carries (RenewalOf's CapacityPerScore x ValleyScore): derived, never saved.
    public static double Capacity(World world, Species species) =>
        RenewalOf(species) is RenewalRow row ? ValleyScore(world, species) * row.CapacityPerScore : 0;

    /// Generation's, not today's: grazing regrows and herds move on, so renewal reads the land, not this week's grass.
    public static double ForageScore(World world, TileId tile, Species species)
    {
        if (ForageHere(world, tile) is not (_, ForagePool pool)) return 0;
        var eats = Grazes(species);
        return pool.Items.Where(i => eats.Contains(i.Resource)).Sum(i => pool.Density * i.Abundance);
    }

}
