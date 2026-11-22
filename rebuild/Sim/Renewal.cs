namespace Survival;

/// Herd behaviour rates are a model, so they live in code beside the behaviours, not in content.
/// Season days are Weather.DayOfYear, inclusive. Over capacity the birth rate runs backwards (Thin).
/// CapacityPerScore: members per unit of forage score, or per kg of living prey near the den for a meat-eater.
/// StarveDays: null for what never starves. Arrival: null for what never comes from outside.
public sealed record RenewalRow(int SeasonStartDay, int SeasonEndDay, double BirthsPerHeadPerDay, double CapacityPerScore, int? StarveDays,
    ArrivalRow? Arrival);

/// A herd coming into the valley: the valley carries ValleyPerScore members per unit of its score.
public sealed record ArrivalRow(double ValleyPerScore, int MinMembers, int MaxMembers, int MinTiles, int MaxTiles);

public static partial class Fauna
{
    /// Every number here is a judgment, read off the curves of a simulated year. Births are many for real herds
    /// because the valley's prey are a few dozen head; arrivals keep a species from dying out between seasons.
    static RenewalRow? RenewalOf(Species s) => s switch
    {
        Species.Caribou => new(135, 190, 0.030, 0.045, null, new(0.0035, 5, 15, 128, 192)),
        Species.Bison => new(130, 190, 0.020, 0.023, null, new(0.0066, 3, 8, 240, 400)),
        Species.Megaloceros => new(140, 190, 0.020, 0.033, null, new(0.0014, 1, 3, 48, 96)),
        Species.Mammoth => new(120, 200, 0.004, 0.032, null, null),
        Species.Wolf => new(110, 150, 0.020, 1.0 / 700, 14, new(1.0 / 3500, 3, 8, 48, 80)),
        // Hyenas and the carrion flock find something when no kill is near, so they never starve; they thin only over capacity.
        Species.Hyena => new(110, 170, 0.015, 1.0 / 850, null, new(1.0 / 3200, 3, 6, 64, 112)),
        Species.Raven => new(100, 180, 0.020, 0.05, null, new(0.0040, 3, 6, 150, 250)),
        // A bear's cub leaves as a herd of its own on its mother's land (Born).
        Species.Bear => new(100, 140, 0.004, 0.022, null, new(0.0012, 1, 1, 64, 128)),
        Species.CaveBear => new(100, 140, 0.004, 0.022, null, null), // its den is a cave
        // A dead megafauna hunter stays dead and none arrive; saber-tooths forage, so never starve. People and small game are not herds.
        Species.SaberTooth or Species.Human or Species.Hare or Species.Fox => null,
    };

    public const int PreyRangeTiles = ForayRadiusTiles;
    const double StarveDeathPerDay = 0.3;
    const double ArrivalsPerDay = 0.05; // at the valley's full deficit

    /// Once a day, at midnight. Each herd rolls on its own stream (renewal) in id order, arrivals on the valley's.
    /// A herd in a fight neither starves, thins nor gives birth that day.
    public static void Renew(World world, List<WorldEvent> events)
    {
        if (Weather.MinuteOfDay(world.Minute) != 0) return;
        int day = Weather.DayOfYear(world.Minute);
        foreach (var herd in world.Agents.Values.Where(a => a.Mind is HerdMind).ToList())
        {
            if (!world.Agents.ContainsKey(herd.Id) || Combat.EncounterOf(world, herd.Id) != null || RenewalOf(herd.Species) is not RenewalRow row) continue;
            var rng = world.Rng("renewal", herd.Id);
            if (Starve(world, herd, row, rng, events)) continue;
            double capacity = CapacityOf(world, herd), current = CurrentFor(world, herd);
            if (current > capacity && Thins(herd.Species))
            {
                Thin(world, herd, row.BirthsPerHeadPerDay * (current - capacity) / current, rng, events);
                continue;
            }
            if (day < row.SeasonStartDay || day > row.SeasonEndDay || capacity <= 0) continue;
            double p = row.BirthsPerHeadPerDay * Deficit(capacity, current);
            int births = 0, room = (int)Math.Floor(capacity - current);
            foreach (var _ in herd.Members.Where(Body.IsAlive).ToList())
                if (births < room && rng.NextDouble() < p) births++;
            for (int i = 0; i < births; i++) Born(world, herd, rng);
        }
        Arrive(world);
    }

    static double Deficit(double capacity, double current) => capacity <= 0 ? 0 : Math.Max(0, (capacity - current) / capacity);

    /// Returns whether the herd is gone.
    static bool Starve(World world, Agent herd, RenewalRow row, Rng rng, List<WorldEvent> events)
    {
        var mind = MindOf(herd);
        if (row.StarveDays is not int days || world.Minute - mind.LastFedMinute <= days * 1440L) return false;
        if (rng.NextDouble() >= StarveDeathPerDay) return false;
        return Lose(world, herd, events);
    }

    /// What lives on kills follows them down. Grazers, browsers and bears only stop giving birth when full.
    /// A herd Populate put over capacity should not starve, and a cave bear's land (cave floor) carries nothing measurable.
    static bool Thins(Species s) => BehaviorOf(s) is HerdBehavior.Pack or HerdBehavior.Scavenger or HerdBehavior.Carrion;

    /// The birth rate run backwards, so a herd falls toward capacity as fast as it would grow toward it.
    static void Thin(World world, Agent herd, double p, Rng rng, List<WorldEvent> events)
    {
        int losses = herd.Members.Where(Body.IsAlive).Count(_ => rng.NextDouble() < p);
        for (int i = 0; i < losses && world.Agents.ContainsKey(herd.Id); i++) Lose(world, herd, events);
    }

    /// Returns whether the herd is gone (the last member leaves the world).
    static bool Lose(World world, Agent herd, List<WorldEvent> events)
    {
        var body = herd.Members.Where(Body.IsAlive).OrderBy(b => b.FatKg).First();
        Combat.LeaveCarcass(world, herd.Tile, herd.Species, body, new Cause(null, "starvation"));
        events.Add(new Killed(world.Minute, herd.Id, herd.Species, body.WeightKg, herd.Tile, By: null));
        herd.Members.Remove(body);
        if (herd.Members.Count > 0) return false;
        Agents.Remove(world, herd.Id);
        return true;
    }

    /// Newborns are full-grown bodies: there is no growth model.
    static void Born(World world, Agent herd, Rng rng)
    {
        if (BehaviorOf(herd.Species) == HerdBehavior.Solitary) Agents.Spawn(world, herd.Species, herd.Tile, 1, [.. MindOf(herd).Territory]);
        else herd.Members.Add(SpeciesTable.NewBody(herd.Species, rng));
    }

    /// Beyond the farthest anyone can see, so nobody sees a herd appear from nothing.
    public const int ArrivalClearanceTiles = Sight.MaxTiles;

    /// Species in order, each with chance ArrivalsPerDay x the valley's deficit; no more members than the land has room for.
    static void Arrive(World world)
    {
        var rng = world.Rng("arrivals");
        foreach (var species in Enum.GetValues<Species>())
        {
            if (RenewalOf(species)?.Arrival is not ArrivalRow a) continue;
            double capacity = ValleyCapacity(world, species);
            double current = world.Agents.Values.Where(x => x.Species == species && x.Mind is HerdMind).Sum(x => x.Members.Count(Body.IsAlive));
            if (rng.NextDouble() >= ArrivalsPerDay * Deficit(capacity, current)) continue;
            var land = ArrivalLand(world, species);
            if (land.Count == 0) continue;
            var start = land[rng.NextInt(land.Count)];
            var territory = Territory(Map.Of(world), start, land, a.MinTiles + rng.NextInt(a.MaxTiles - a.MinTiles + 1), rng);
            int members = a.MinMembers + rng.NextInt(a.MaxMembers - a.MinMembers + 1);
            if (territory.Count < 3) continue;
            double room = CapacityOn(world, species, territory) - CurrentOn(world, species, territory);
            members = Math.Min(members, (int)Math.Floor(room));
            if (members < a.MinMembers) continue;
            Agents.Spawn(world, species, start, members, territory);
        }
    }

    /// Open land off the packs' land for a predator (as Populate keeps them) and out of everyone's sight.
    static List<TileId> ArrivalLand(World world, Species species)
    {
        var people = world.Agents.Values.Where(a => a.Mind is PersonMind && Sim.IsAlive(a)).Select(a => a.Tile).ToList();
        var packs = IsPredator(species)
            ? world.Agents.Values.Where(a => a.Mind is HerdMind && BehaviorOf(a.Species) == HerdBehavior.Pack && Sim.IsAlive(a))
                .SelectMany(a => MindOf(a).Territory).ToHashSet()
            : [];
        return [.. OpenLand(world).Where(t => !packs.Contains(t) && people.All(p => MapGen.Manhattan(p, t) > ArrivalClearanceTiles))];
    }

    public static double ValleyCapacity(World world, Species species) =>
        RenewalOf(species)?.Arrival is ArrivalRow a ? ValleyScore(world, species) * a.ValleyPerScore : 0;

    /// Generation's forage, so cached and never saved; for a meat-eater the living prey now.
    static double ValleyScore(World world, Species species)
    {
        var diet = DietOf(species);
        if (diet == Diet.Carnivore)
            return world.Agents.Values.Where(a => a.Mind is HerdMind && IsQuarry(a.Species)).Sum(a => a.Members.Where(Body.IsAlive).Sum(b => b.WeightKg));
        var cache = world.ValleyForageCache ??= [];
        if (!cache.TryGetValue(diet, out double score)) cache[diet] = score = OpenLand(world).Sum(t => ForageScore(world, t, diet));
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

    /// Derived from its pools and never saved.
    public static double CapacityOf(World world, Agent herd) => CapacityOn(world, herd.Species, MindOf(herd).Territory);

    /// The den is the land's first tile.
    public static double CapacityOn(World world, Species species, IReadOnlyList<TileId> land) =>
        RenewalOf(species) is RenewalRow row ? CapacityScore(world, species, land) * row.CapacityPerScore : 0;

    public static double CapacityScore(World world, Agent herd) => CapacityScore(world, herd.Species, MindOf(herd).Territory);

    static double CapacityScore(World world, Species species, IReadOnlyList<TileId> land)
    {
        var diet = DietOf(species);
        return diet == Diet.Carnivore ? PreyKgNear(world, land[0]) : land.Sum(t => ForageScore(world, t, diet));
    }

    static double CurrentFor(World world, Agent herd) => CurrentOn(world, herd.Species, MindOf(herd).Territory);

    /// Herds whose land overlaps share what it carries rather than each filling it; a bear's cub on a copy of its
    /// mother's land counts as a whole bear there; two packs count each other by how far their dens' ranges overlap.
    static double CurrentOn(World world, Species species, IReadOnlyList<TileId> land)
    {
        bool meat = DietOf(species) == Diet.Carnivore;
        var tiles = meat ? null : land.ToHashSet();
        double current = 0;
        foreach (var other in world.Agents.Values)
        {
            if (other.Species != species || other.Mind is not HerdMind mind) continue;
            int alive = other.Members.Count(Body.IsAlive);
            if (alive == 0) continue;
            double share = meat ? RangeOverlap(mind.Territory[0], land[0]) : mind.Territory.Count(tiles!.Contains) / (double)mind.Territory.Count;
            current += alive * share;
        }
        return current;
    }

    /// Overlap of two equal diamonds (open ground assumed, edges and walls ignored): 1 for the same den, 0 beyond twice the range.
    static double RangeOverlap(TileId a, TileId b)
    {
        double side = 2 * PreyRangeTiles + 1;
        int du = Math.Abs(a.X + a.Y - b.X - b.Y), dv = Math.Abs(a.X - a.Y - b.X + b.Y);
        return Math.Max(0, 1 - du / side) * Math.Max(0, 1 - dv / side);
    }

    /// Generation's, not today's: grazing regrows and herds move on, so renewal reads the land, not this week's grass.
    public static double ForageScore(World world, TileId tile, Diet diet)
    {
        if (ForageHere(world, tile) is not (_, ForagePool pool)) return 0;
        var eats = Eats(diet);
        return pool.Items.Where(i => eats.Contains(i.Resource)).Sum(i => pool.Density * i.Abundance);
    }

    public static double PreyKgNear(World world, TileId tile) =>
        Nearby.Within(world, tile, PreyRangeTiles).Where(a => a.Mind is HerdMind && IsQuarry(a.Species))
            .Sum(a => a.Members.Where(Body.IsAlive).Sum(b => b.WeightKg));
}
