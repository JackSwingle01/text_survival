namespace Survival;

/// What kind of bodies an agent is made of. Ptarmigan, rat and fish are cut: birds have no body
/// plan any more (plan: Keep/Port/Cut), and the others are small-game pools, not fighters.
/// Raven is the folded scavenger kind (emergent-world-design §4): ravens, a fox and a wolverine as one
/// flock that lives on what others kill. Birds have no body plan, so its members are small quadrupeds.
public enum Species { Human, Wolf, Bear, CaveBear, Homotherium, Hyena, Caribou, Megaloceros, Bison, Mammoth, Rabbit, Fox, Raven }

/// How an animal meets a threat (old AnimalBehaviorType). Humans are their own row: they never bolt by instinct.
public enum Role { Human, Prey, DangerousPrey, Predator, Scavenger }

/// What makes a herd come at a target (old Temperament): a base, a bonus per head, hunger,
/// defending a kill or den, and a ceiling. Species that never start a fight have Cap 0.
public sealed record Temperament(double Base, double PerMember, double HungryAt, double StarvingAt, double HungerBonus, double DefendBonus, double Cap);

/// One row of the species table (Content/species.json): the body plan, how it fights, the morale
/// numbers combat runs on, and how its herd lives.
/// Old AnimalFactory and AnimalType; humans are the old Actor defaults and Player's unarmed attack.
public sealed record SpeciesStats(
    BodyKind Body, double WeightKg, double FatPct, double MusclePct,
    Role Role, bool IsSmall,
    double AttackDamage, DamageType AttackType,
    double Threat, double Boldness, double Aggression, double Cohesion,
    /// Metres covered in one combat action at full Moving (old CombatMovement.Allowance).
    int MoveAllowanceM,
    Temperament Temperament,
    /// How its herd lives, and what its herd grazes. Humans and small game never form herds of their own.
    HerdBehavior Behavior,
    Diet Diet,
    /// How it runs, fresh and unladen (SpeciesTable.Running gives a body's own, cut by wounds and load).
    Motion Motion = default,
    /// What a member eats a minute grazing a tile nothing has eaten yet; 0 for what never grazes.
    double GrazeKcalPerMinute = 0,
    /// People only: a woman's build and running, where they differ from a man's (the row's own).
    PersonBuild? Female = null)
{
    public bool IsAnimal => Role != Role.Human;
    /// Prey and small scavengers run once they know you are there; fighting is what predators do.
    public bool IsSkittish => Role == Role.Prey || (Role == Role.Scavenger && IsSmall);
}

/// How a species runs, fresh and unladen (documentation/species-stats-research.md §5): flat-out sprint,
/// the pace it holds for twenty minutes or more, the seconds at top speed that empty a full sprint
/// reserve, the net cost of running a metre, and the share of the reserve one strike, dodge or shove takes.
public readonly record struct Motion(double TopSpeedMps, double EnduranceSpeedMps, double SprintSeconds, double RunCostJPerKgM, double BurstPct);

/// A person's build and running where a woman's differ from a man's.
public sealed record PersonBuild(double WeightKg, double FatPct, double MusclePct, double TopSpeedMps, double EnduranceSpeedMps);

public enum Sex { Male, Female }

/// How one body runs now: its species' running, at the share of a fresh body's speed that its wounds,
/// fatigue and load leave it (SpeedPct, Body.Speed against a fresh one of its kind), carrying CarriedKg.
public readonly record struct Running(Motion Motion, double SpeedPct, double CarriedKg = 0);

public static class SpeciesTable
{
    static readonly IReadOnlyDictionary<Species, SpeciesStats> Rows = Content.Tables.Species.ToDictionary(kv => kv.Key, kv =>
    {
        var (motion, graze) = MotionRow(kv.Key);
        return kv.Value with { Motion = motion, GrazeKcalPerMinute = graze, Female = kv.Key == Species.Human ? WomanRow : null };
    });

    // Interim, until the content loader takes these columns in species.json (they are data, not code):
    // research §5.3's running and §4.1's winter intake, each tagged V (read in a primary source), R
    // (recalled: check before pinning a test to it) or D (derived or judged).
    static (Motion Motion, double GrazeKcalPerMinute) MotionRow(Species species) => species switch
    {
        // Top speed R, endurance V (Liebenberg 2006), sprint seconds D, running cost R (Margaria 1963), burst D.
        Species.Human => (new(8.5, 3.3, 15, 4.0, 0.12), 0),
        // Top speed chosen above the clocked 12 m/s (V, Mech 1966), the chase pace D, sprint D, cost V (Taylor 1982), burst D.
        Species.Wolf => (new(14, 8, 40, 3.3, 0.06), 0),
        // Brown bear: top speed V (secondary), endurance R, the rest D. Intake D: the old forage-to-hunger ratio at Kleiber's burn.
        Species.Bear => (new(13, 4, 20, 1.8, 0.08), 18),
        // All D; intake the brown bear's, scaled by Kleiber.
        Species.CaveBear => (new(11, 3.5, 20, 1.5, 0.08), 25),
        // Speeds R (Barnett 2020: cursorial), a felid's short sprint D. Its "grazing" is the small game a lone cat
        // lives on between kills (D: the old forage-to-hunger ratio at Kleiber's burn), as the old solitary forage was.
        Species.Homotherium => (new(14, 4, 15, 2.0, 0.10), 15),
        // Speeds R (Kruuk 1972), the rest D.
        Species.Hyena => (new(15, 7, 40, 2.7, 0.06), 0),
        // Top speed R, endurance and sprint D, cost V. Intake D from winter lichen.
        Species.Caribou => (new(16, 7, 20, 2.4, 0.10), 13),
        // All D, by red deer and elk.
        Species.Megaloceros => (new(15, 6, 20, 1.4, 0.08), 20),
        // Top speed R, the rest D. Intake D from V grams a minute (Hudson & Frank 1987; Raynor 2015).
        Species.Bison => (new(15, 6, 25, 1.3, 0.08), 35),
        // Top speed V (Hutchinson 2003, elephants), endurance R, sprint seconds a guess (D). Intake D, by elephants.
        Species.Mammoth => (new(6.5, 3, 90, 0.7, 0.05), 90),
        // The mountain hare: top speed R (unverified), the rest D.
        Species.Rabbit => (new(17, 4, 10, 7.5, 0.15), 0.4),
        // Top speed R, the rest D.
        Species.Fox => (new(13.5, 5, 15, 6.0, 0.10), 0),
        // The flock runs as its fox does (D); its foraging the old forage-to-hunger ratio at Kleiber's burn (D).
        Species.Raven => (new(13.5, 5, 15, 6.0, 0.10), 0.53),
        _ => throw new KeyNotFoundException($"No running for {species}"),
    };

    // Build R (Holt & Formicola 2008, Pontzer 2012, Janssen 2000), top speed R, endurance D.
    static PersonBuild WomanRow => new(55, 0.22, 0.31, 7.3, 2.9);

    public static SpeciesStats Of(Species s) =>
        Rows.TryGetValue(s, out var row) ? row : throw new KeyNotFoundException($"No species row for {s}");

    /// A new member: the species' plan at a random size, most near average (old Animal.GenerateTraits).
    /// A human is the dressed baseline adult.
    /// An animal starts its life a little hungry: its store 70-100% full (old Fauna.NewMind's 0-0.3 hunger).
    public static BodyState NewBody(Species species, Rng rng)
    {
        if (species == Species.Human) return BodyState.BaselineHuman();
        var row = Of(species);
        // Average of two uniforms: roughly normal around 1, spread 0.15, kept to 0.7-1.3.
        double size = Math.Clamp(1.0 + ((rng.NextDouble() + rng.NextDouble()) / 2 - 0.5) * 2 * 0.15, 0.7, 1.3);
        var body = BodyState.Create(row.Body, row.WeightKg * size, row.FatPct, row.MusclePct);
        body.CaloriesKcal = Body.CaloriesCapacityKcal(body) * (1 - rng.NextDouble() * 0.3);
        return body;
    }

    /// A member of average build, whole and fed: the body a species' running is measured on.
    public static BodyState Average(Species species, Sex sex = Sex.Male)
    {
        if (species == Species.Human) return BodyState.BaselineHuman(sex);
        var row = Of(species);
        var body = BodyState.Create(row.Body, row.WeightKg, row.FatPct, row.MusclePct);
        body.CaloriesKcal = Body.CaloriesCapacityKcal(body);
        return body;
    }

    /// How this body runs now: its species' (a woman's own, for a woman), at the share of an average
    /// fresh member's speed (Body.Speed, load included) that it has. A share, not Body.Speed itself:
    /// Speed's kilogram terms are a person's (body review B11), and against its own kind they cancel.
    public static Running Running(Species species, BodyState body, AbilityContext context = default, double carriedKg = 0)
    {
        var row = Of(species);
        var motion = species == Species.Human && body.Sex == Sex.Female && row.Female is PersonBuild f
            ? row.Motion with { TopSpeedMps = f.TopSpeedMps, EnduranceSpeedMps = f.EnduranceSpeedMps }
            : row.Motion;
        return new Running(motion, Math.Max(MinSpeedPct, Body.Speed(body, context) / FreshSpeed(species, body.Sex)), carriedKg);
    }

    /// A body so hurt or laden that it barely moves still runs a little, and pays a finite price for a burst.
    public const double MinSpeedPct = 0.1;

    static readonly Dictionary<(Species, Sex), double> FreshSpeeds = [];

    /// Body.Speed of an average fresh member, unladen: worked out once per kind.
    static double FreshSpeed(Species species, Sex sex)
    {
        if (species != Species.Human) sex = Sex.Male;
        lock (FreshSpeeds)
        {
            if (!FreshSpeeds.TryGetValue((species, sex), out double speed))
                FreshSpeeds[(species, sex)] = speed = Body.Speed(Average(species, sex));
            return speed;
        }
    }
}
