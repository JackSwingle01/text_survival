namespace Survival;

/// How a species renews (emergent-world-design §6.3). Herd behaviour rates are a model, so they live in
/// code beside the behaviours (as Fauna's do), not in content.
/// Births: in its season (SeasonStartDay to SeasonEndDay, days of the year, Weather.DayOfYear,
/// inclusive) each living member has a daily chance BirthsPerHeadPerDay x the herd's deficit. Over
/// capacity, the same rate runs backwards for what lives on kills (Thin).
/// CapacityPerScore: members its land carries per unit of score: forage score for what eats what grows
/// (Fauna.ForageScore over its territory), or kg of living prey near its den for a meat-eater.
/// StarveDays: a meat-eater this long unfed starts to lose members; null for what never starves.
/// Arrival: herds that come into the valley from outside, at any time of year, when the valley holds
/// fewer than it carries; null for what never comes (megafauna, cave dwellers).
public sealed record RenewalRow(int SeasonStartDay, int SeasonEndDay, double BirthsPerHeadPerDay, double CapacityPerScore, int? StarveDays,
    ArrivalRow? Arrival);

/// A herd coming into the valley: the valley carries ValleyPerScore members per unit of the valley's
/// score (its whole forage for what grows, or its living prey kg for a meat-eater), and a herd that
/// comes is MinMembers-MaxMembers strong on MinTiles-MaxTiles of land, as Populate makes them.
public sealed record ArrivalRow(double ValleyPerScore, int MinMembers, int MaxMembers, int MinTiles, int MaxTiles);

public static partial class Fauna
{
    /// The old game had no calving, recruitment, arrivals or starving herds at all: every number here
    /// is a judgment, set by running the year on five seeds and reading the curves. Calving runs from
    /// mid-May to midsummer, pups and cubs come in spring. Births are many for real herds because the
    /// valley's prey are a few dozen head under one or two packs; arrivals keep a species from dying out
    /// between seasons, as migrating herds and dispersing wolves would.
    static RenewalRow? RenewalOf(Species s) => s switch
    {
        Species.Caribou => new(135, 190, 0.030, 0.045, null, new(0.0035, 5, 15, 128, 192)),
        Species.Bison => new(130, 190, 0.020, 0.023, null, new(0.0066, 3, 8, 240, 400)),
        Species.Megaloceros => new(140, 190, 0.020, 0.033, null, new(0.0014, 1, 3, 48, 96)),
        Species.Mammoth => new(120, 200, 0.004, 0.032, null, null),
        Species.Wolf => new(110, 150, 0.020, 1.0 / 700, 14, new(1.0 / 3500, 3, 8, 48, 80)),
        // Hyenas and the carrion flock find something (bones, small game, berries) when no kill is
        // near, so no stretch unfed starves them (judgment: the old hyena only scavenged and never went
        // hungry); they thin only when their land holds more of them than it carries (Thin).
        Species.Hyena => new(110, 170, 0.015, 1.0 / 850, null, new(1.0 / 3200, 3, 6, 64, 112)),
        Species.Raven => new(100, 180, 0.020, 0.05, null, new(0.0040, 3, 6, 150, 250)),
        // A bear's cub leaves as a herd of its own on its mother's land (Born).
        Species.Bear => new(100, 140, 0.004, 0.022, null, new(0.0012, 1, 1, 64, 128)),
        Species.CaveBear => new(100, 140, 0.004, 0.022, null, null), // its den is a cave
        // A dead megafauna hunter stays dead and none arrive (overview: megafauna herds); saber-tooths
        // forage, so they never starve either. People and small game are not herds.
        Species.SaberTooth or Species.Human or Species.Rabbit or Species.Fox => null,
    };

    /// Within this many tiles of its den a meat-eater counts the living prey it lives on.
    public const int PreyRangeTiles = ForayRadiusTiles;
    /// The day's chance that a meat-eater past its StarveDays loses one of its members to hunger.
    const double StarveDeathPerDay = 0.3;
    /// The day's chance of an arrival at the valley's full deficit.
    const double ArrivalsPerDay = 0.05;

    /// Once a day, at midnight: meat-eaters too long unfed lose a member; a herd that lives on kills
    /// (Thins) whose land holds more of its kind than it carries loses members to hunger, all year,
    /// p = base x (current - capacity) / current for each living member; in their season herds give
    /// birth toward what their land carries, p = base x max(0, (capacity - current) / capacity) for
    /// each living member; and herds arrive where the valley holds fewer than it carries, on the same
    /// rule. Each herd rolls on its own
    /// stream (renewal), in id order, and arrivals on the valley's. A herd in a fight neither starves,
    /// thins nor gives birth that day.
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

    /// A meat-eater past its StarveDays loses its leanest member to hunger on the day's roll. Returns
    /// whether the herd is gone.
    static bool Starve(World world, Agent herd, RenewalRow row, Rng rng, List<WorldEvent> events)
    {
        var mind = MindOf(herd);
        if (row.StarveDays is not int days || world.Minute - mind.LastFedMinute <= days * 1440L) return false;
        if (rng.NextDouble() >= StarveDeathPerDay) return false;
        return Lose(world, herd, events);
    }

    /// What lives on kills follows them down: packs, scavengers and the carrion flock, whose capacity
    /// is the prey near them or, for the flock, the land it ranges. Grazers, browsers and bears only
    /// stop giving birth when their land is full; predators thin them. Judgment: a fixed land that a
    /// herd was put on over its capacity (Populate sizes herds without it) should not starve it, and a
    /// cave bear's land, all cave floor, carries nothing it could be measured by.
    static bool Thins(Species s) => BehaviorOf(s) is HerdBehavior.Pack or HerdBehavior.Scavenger or HerdBehavior.Carrion;

    /// Too many for the land: each living member is lost to hunger with chance p (judgment: the birth
    /// rate run backwards, so a herd falls toward what its land carries as fast as it would grow toward
    /// it). What the land cannot feed is what goes when prey crash, or the scavengers' kills dry up.
    static void Thin(World world, Agent herd, double p, Rng rng, List<WorldEvent> events)
    {
        int losses = herd.Members.Where(Body.IsAlive).Count(_ => rng.NextDouble() < p);
        for (int i = 0; i < losses && world.Agents.ContainsKey(herd.Id); i++) Lose(world, herd, events);
    }

    /// The herd's leanest member dies of hunger where the herd stands; the last of a herd dying of it
    /// leaves the world. Returns whether the herd is gone.
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

    /// A newborn: a member of the herd, or for a solitary animal a herd of its own on a copy of its
    /// mother's land. Newborns are full-grown bodies (judgment: there is no growth model).
    static void Born(World world, Agent herd, Rng rng)
    {
        if (BehaviorOf(herd.Species) == HerdBehavior.Solitary) Agents.Spawn(world, herd.Species, herd.Tile, 1, [.. MindOf(herd).Territory]);
        else herd.Members.Add(SpeciesTable.NewBody(herd.Species, rng));
    }

    // ---- arrivals ----

    /// An arrival comes in at least this far from every living person: out of sight, so nobody sees a
    /// herd appear from nothing (judgment: twice the sight radius).
    public const int ArrivalClearanceTiles = 2 * Sight.RadiusTiles;

    /// For each species that comes from outside, in species order: with the day's chance ArrivalsPerDay
    /// x the valley's deficit, a herd comes in somewhere on the open land, out of everyone's sight, and
    /// a predator off the packs' land (Populate's rule). It is MinMembers-MaxMembers strong, as
    /// Populate makes them, but no more than the land it comes to has room for (CapacityOn less
    /// CurrentOn); where that is fewer than MinMembers, none comes.
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

    /// Where a herd of the species may come in today: the open land, off the packs' land for a predator
    /// (as Populate keeps them), and out of everyone's sight (ArrivalClearanceTiles), x-major.
    static List<TileId> ArrivalLand(World world, Species species)
    {
        var people = world.Agents.Values.Where(a => a.Mind is PersonMind && Sim.IsAlive(a)).Select(a => a.Tile).ToList();
        var packs = IsPredator(species)
            ? world.Agents.Values.Where(a => a.Mind is HerdMind && BehaviorOf(a.Species) == HerdBehavior.Pack && Sim.IsAlive(a))
                .SelectMany(a => MindOf(a).Territory).ToHashSet()
            : [];
        return [.. OpenLand(world).Where(t => !packs.Contains(t) && people.All(p => MapGen.Manhattan(p, t) > ArrivalClearanceTiles))];
    }

    /// Members of the species the whole valley carries now (zero for what never arrives).
    public static double ValleyCapacity(World world, Species species) =>
        RenewalOf(species)?.Arrival is ArrivalRow a ? ValleyScore(world, species) * a.ValleyPerScore : 0;

    /// The valley's score for a species: everything its diet eats on the open land (generation's, so
    /// cached and never saved), or for a meat-eater the living prey in the valley now.
    static double ValleyScore(World world, Species species)
    {
        var diet = DietOf(species);
        if (diet == Diet.Carnivore)
            return world.Agents.Values.Where(a => a.Mind is HerdMind && IsQuarry(a.Species)).Sum(a => a.Members.Where(Body.IsAlive).Sum(b => b.WeightKg));
        var cache = world.ValleyForageCache ??= [];
        if (!cache.TryGetValue(diet, out double score)) cache[diet] = score = OpenLand(world).Sum(t => ForageScore(world, t, diet));
        return score;
    }

    /// Open land a herd can live on (Populate's interior) outside camp's safe zone, x-major. Fixed at
    /// generation, so worked out once and never saved.
    static List<TileId> OpenLand(World world)
    {
        if (world.OpenLandCache is { } land) return land;
        var m = Map.Of(world);
        return world.OpenLandCache = [.. InteriorTiles(m).Where(t => MapGen.Manhattan(t, m.Camp) > SafeZoneTiles)];
    }

    // ---- capacity ----

    /// The megafauna: a dead herd of them stays dead and none arrive (overview: megafauna herds).
    public static bool IsMegafauna(Species s) => s is Species.Mammoth or Species.SaberTooth;

    /// Members the herd's land carries now, derived from its pools and never saved (CapacityOn).
    public static double CapacityOf(World world, Agent herd) => CapacityOn(world, herd.Species, MindOf(herd).Territory);

    /// Members of the species a land carries now: what grows on it for what the species eats
    /// (ForageScore over the land), or for a meat-eater the living prey within PreyRangeTiles of its
    /// den (PreyKgNear). The den is the land's first tile.
    public static double CapacityOn(World world, Species species, IReadOnlyList<TileId> land) =>
        RenewalOf(species) is RenewalRow row ? CapacityScore(world, species, land) * row.CapacityPerScore : 0;

    public static double CapacityScore(World world, Agent herd) => CapacityScore(world, herd.Species, MindOf(herd).Territory);

    static double CapacityScore(World world, Species species, IReadOnlyList<TileId> land)
    {
        var diet = DietOf(species);
        return diet == Diet.Carnivore ? PreyKgNear(world, land[0]) : land.Sum(t => ForageScore(world, t, diet));
    }

    /// Who counts against the herd's capacity (CurrentOn its land).
    static double CurrentFor(World world, Agent herd) => CurrentOn(world, herd.Species, MindOf(herd).Territory);

    /// The species on this land, as its capacity counts it: every herd of the species, each with its
    /// living members times the share of its own land that lies in this one. Herds whose land overlaps
    /// share what it carries; a herd alone on its land counts only its own; a bear's cub, on a copy of
    /// its mother's land, counts as a whole bear there. A meat-eater's land is the ground within
    /// PreyRangeTiles of its den, so two packs count each other by how far their dens' ranges overlap.
    /// Old (this rebuild): each herd counted only its own members, and a solitary animal whoever stood
    /// on its land, so overlapping herds each filled the same land to its capacity (bug, fixed).
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

    /// The share of one den's hunting range (the diamond within PreyRangeTiles) that lies within
    /// another's: the overlap of two equal squares, turned 45 degrees (judgment: open ground, edges
    /// and walls ignored). 1 for the same den, 0 once they are more than twice the range apart.
    static double RangeOverlap(TileId a, TileId b)
    {
        double side = 2 * PreyRangeTiles + 1;
        int du = Math.Abs(a.X + a.Y - b.X - b.Y), dv = Math.Abs(a.X - a.Y - b.X + b.Y);
        return Math.Max(0, 1 - du / side) * Math.Max(0, 1 - dv / side);
    }

    /// What grows on a tile for this diet as generated: the forage's density times the abundance of
    /// each thing the diet eats there. Generation's, not today's: grazing regrows, and a herd moves on
    /// from what it has eaten out (judgment: renewal reads the land, not this week's grass).
    public static double ForageScore(World world, TileId tile, Diet diet)
    {
        if (ForageHere(world, tile) is not (_, ForagePool pool)) return 0;
        var eats = Eats(diet);
        return pool.Items.Where(i => eats.Contains(i.Resource)).Sum(i => pool.Density * i.Abundance);
    }

    /// Kg of living prey whose herds stand within PreyRangeTiles of the tile.
    public static double PreyKgNear(World world, TileId tile) =>
        Nearby.Within(world, tile, PreyRangeTiles).Where(a => a.Mind is HerdMind && IsQuarry(a.Species))
            .Sum(a => a.Members.Where(Body.IsAlive).Sum(b => b.WeightKg));
}
