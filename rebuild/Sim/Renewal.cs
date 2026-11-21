namespace Survival;

/// How a species renews (emergent-world-design §6.3). Herd behaviour rates are a model, so they live in
/// code beside the behaviours (as Fauna's do), not in content.
/// Births: in its season (SeasonStartDay to SeasonEndDay, days of the year, Weather.DayOfYear,
/// inclusive) each living member has a daily chance BirthsPerHeadPerDay x the herd's deficit.
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
        // near, so they never starve (judgment: the old hyena only scavenged and never went hungry).
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

    /// Once a day, at midnight: meat-eaters too long unfed lose a member; in their season herds give
    /// birth toward what their land carries, p = base x max(0, (capacity - current) / capacity) for
    /// each living member; and herds arrive where the valley holds fewer than it carries, on the same
    /// rule. Each herd rolls on its own stream (renewal), in id order, and arrivals on the valley's. A
    /// herd in a fight neither starves nor gives birth that day.
    public static void Renew(World world, List<WorldEvent> events)
    {
        if (Weather.MinuteOfDay(world.Minute) != 0) return;
        int day = Weather.DayOfYear(world.Minute);
        foreach (var herd in world.Agents.Values.Where(a => a.Mind is HerdMind).ToList())
        {
            if (!world.Agents.ContainsKey(herd.Id) || Combat.EncounterOf(world, herd.Id) != null || RenewalOf(herd.Species) is not RenewalRow row) continue;
            var rng = world.Rng("renewal", herd.Id);
            if (Starve(world, herd, row, rng, events)) continue;
            if (day < row.SeasonStartDay || day > row.SeasonEndDay) continue;
            double capacity = CapacityOf(world, herd), current = CurrentFor(world, herd);
            if (capacity <= 0) continue;
            double p = row.BirthsPerHeadPerDay * Deficit(capacity, current);
            int births = 0, room = (int)Math.Floor(capacity - current);
            foreach (var _ in herd.Members.Where(Body.IsAlive).ToList())
                if (births < room && rng.NextDouble() < p) births++;
            for (int i = 0; i < births; i++) Born(world, herd, rng);
        }
        Arrive(world);
    }

    static double Deficit(double capacity, double current) => capacity <= 0 ? 0 : Math.Max(0, (capacity - current) / capacity);

    /// A meat-eater past its StarveDays loses its leanest member to hunger on the day's roll; the last
    /// of a herd dying of it leaves the world. Returns whether the herd is gone.
    static bool Starve(World world, Agent herd, RenewalRow row, Rng rng, List<WorldEvent> events)
    {
        var mind = MindOf(herd);
        if (row.StarveDays is not int days || world.Minute - mind.LastFedMinute <= days * 1440L) return false;
        if (rng.NextDouble() >= StarveDeathPerDay) return false;
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

    /// For each species that comes from outside, in species order: with the day's chance ArrivalsPerDay
    /// x the valley's deficit, a herd comes in somewhere outside camp's safe zone, sized and landed as
    /// Populate makes them.
    static void Arrive(World world)
    {
        var rng = world.Rng("arrivals");
        foreach (var species in Enum.GetValues<Species>())
        {
            if (RenewalOf(species)?.Arrival is not ArrivalRow a) continue;
            double capacity = ValleyCapacity(world, species);
            double current = world.Agents.Values.Where(x => x.Species == species && x.Mind is HerdMind).Sum(x => x.Members.Count(Body.IsAlive));
            if (rng.NextDouble() >= ArrivalsPerDay * Deficit(capacity, current)) continue;
            var land = OpenLand(world);
            if (land.Count == 0) continue;
            var start = land[rng.NextInt(land.Count)];
            var territory = Territory(Map.Of(world), start, land, a.MinTiles + rng.NextInt(a.MaxTiles - a.MinTiles + 1), rng);
            if (territory.Count < 3) continue;
            Agents.Spawn(world, species, start, a.MinMembers + rng.NextInt(a.MaxMembers - a.MinMembers + 1), territory);
        }
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

    /// Open land a herd can live on (Populate's interior) outside camp's safe zone, x-major.
    static List<TileId> OpenLand(World world)
    {
        var m = Map.Of(world);
        return [.. InteriorTiles(m).Where(t => MapGen.Manhattan(t, m.Camp) > SafeZoneTiles)];
    }

    // ---- capacity ----

    /// The megafauna: a dead herd of them stays dead and none arrive (overview: megafauna herds).
    public static bool IsMegafauna(Species s) => s is Species.Mammoth or Species.SaberTooth;

    /// Prey a meat-eater lives on: not a mammoth, which no pack in the valley brings down (judgment).
    static bool IsQuarry(Species s) => IsPrey(s) && !IsMegafauna(s);

    /// Members the herd's land carries now, derived from its pools and never saved: what grows there
    /// for what it eats (ForageScore over its territory), or for a meat-eater the living prey within
    /// PreyRangeTiles of its den (PreyKgNear).
    public static double CapacityOf(World world, Agent herd) =>
        RenewalOf(herd.Species) is RenewalRow row ? CapacityScore(world, herd) * row.CapacityPerScore : 0;

    public static double CapacityScore(World world, Agent herd)
    {
        var mind = MindOf(herd);
        var diet = DietOf(herd.Species);
        return diet == Diet.Carnivore ? PreyKgNear(world, mind.Territory[0]) : mind.Territory.Sum(t => ForageScore(world, t, diet));
    }

    /// Who counts against the capacity: the herd's own living members, or for a solitary animal every
    /// one of its kind standing on its land.
    static double CurrentFor(World world, Agent herd)
    {
        if (BehaviorOf(herd.Species) != HerdBehavior.Solitary) return herd.Members.Count(Body.IsAlive);
        var land = MindOf(herd).Territory.ToHashSet();
        return world.Agents.Values.Count(a => a.Species == herd.Species && a.Mind is HerdMind && Sim.IsAlive(a) && land.Contains(a.Tile));
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
