namespace Survival;

/// Raven is the folded scavenger kind (emergent-world-design §4): ravens, a fox and a wolverine as one
/// flock. Birds have no body plan, so its members are small quadrupeds.
public enum Species { Human, Wolf, Bear, CaveBear, Homotherium, Hyena, Caribou, Megaloceros, Bison, Mammoth, Rabbit, Fox, Raven }

/// Humans are their own row: they never bolt by instinct.
public enum Role { Human, Prey, DangerousPrey, Predator, Scavenger }

/// Species that never start a fight have Cap 0.
public sealed record Temperament(double Base, double PerMember, double HungryAt, double StarvingAt, double HungerBonus, double DefendBonus, double Cap);

/// One row of the species table (Content/species.json).
public sealed record SpeciesStats(
    BodyKind Body, double WeightKg, double FatPct, double MusclePct,
    Role Role, bool IsSmall,
    double AttackDamage, DamageType AttackType,
    double Threat, double Boldness, double Aggression, double Cohesion,
    int MoveAllowanceM, // per combat action at full Moving
    double ReachM,
    Temperament Temperament,
    /// Humans and small game never form herds of their own.
    HerdBehavior Behavior,
    Diet Diet,
    Motion Motion,
    double GrazeKcalPerMinute,
    /// Giving ground with the enemy in reach, it strikes instead of backing off at this margin or above.
    double StrikeBackMargin,
    double ChaseLimitSeconds,
    /// People only: a person's starting fat is drawn from this range, well fed for the autumn.
    FatRange? AutumnFatPct,
    /// People only: where a woman's build differs from a man's (the row's own).
    PersonBuild? Female)
{
    public bool IsAnimal => Role != Role.Human;
    /// Prey and small scavengers run once they know you are there; fighting is what predators do.
    public bool IsSkittish => Role == Role.Prey || (Role == Role.Scavenger && IsSmall);
}

/// See documentation/species-stats-research.md §5. EnduranceSpeedMps is the pace held for twenty minutes or more;
/// BurstPct is the share of the sprint reserve one strike, dodge or shove takes.
public readonly record struct Motion(double TopSpeedMps, double EnduranceSpeedMps, double SprintSeconds, double RunCostJPerKgM, double BurstPct);

public sealed record PersonBuild(double WeightKg, double FatPct, double MusclePct, double TopSpeedMps, double EnduranceSpeedMps, FatRange AutumnFatPct);

public sealed record FatRange(double Min, double Max);

public enum Sex { Male, Female }

/// SpeedPct is Body.Speed against a fresh body of its kind: what wounds, fatigue and load leave.
public readonly record struct Running(Motion Motion, double SpeedPct, double CarriedKg = 0);

public static class SpeciesTable
{
    static readonly IReadOnlyDictionary<Species, SpeciesStats> Rows = Content.Tables.Species;

    public static SpeciesStats Of(Species s) =>
        Rows.TryGetValue(s, out var row) ? row : throw new KeyNotFoundException($"No species row for {s}");

    /// An animal starts a little hungry: its store 70-100% full. A person is well fed for the autumn,
    /// fat drawn evenly from the row's range (a lean 12% left NPCs too little to winter on).
    /// The rng is the person's own (World.Birth), so the draw is the seed's.
    public static BodyState NewBody(Species species, Rng rng, Sex sex = Sex.Male)
    {
        if (species == Species.Human)
        {
            var person = BodyState.BaselineHuman(sex);
            var range = sex == Sex.Female ? Of(Species.Human).Female!.AutumnFatPct : Of(Species.Human).AutumnFatPct!;
            double fatPct = range.Min + rng.NextDouble() * (range.Max - range.Min);
            double leanKg = person.StructuralKg + person.MuscleKg;
            person.FatKg = leanKg * fatPct / (1 - fatPct);
            return person;
        }
        var row = Of(species);
        // Average of two uniforms: roughly normal around 1, so most are near average.
        double size = Math.Clamp(1.0 + ((rng.NextDouble() + rng.NextDouble()) / 2 - 0.5) * 2 * 0.15, 0.7, 1.3);
        var body = BodyState.Create(row.Body, row.WeightKg * size, row.FatPct, row.MusclePct);
        body.CaloriesKcal = Body.CaloriesCapacityKcal(body) * (1 - rng.NextDouble() * 0.3);
        return body;
    }

    public static BodyState Average(Species species, Sex sex = Sex.Male)
    {
        if (species == Species.Human) return BodyState.BaselineHuman(sex);
        var row = Of(species);
        var body = BodyState.Create(row.Body, row.WeightKg, row.FatPct, row.MusclePct);
        body.CaloriesKcal = Body.CaloriesCapacityKcal(body);
        return body;
    }

    /// A share of a fresh member's speed, not Body.Speed itself: Speed's kilogram terms are a person's,
    /// and against its own kind they cancel.
    public static Running Running(Species species, BodyState body, AbilityContext context = default, double carriedKg = 0)
    {
        var row = Of(species);
        var motion = species == Species.Human && body.Sex == Sex.Female && row.Female is PersonBuild f
            ? row.Motion with { TopSpeedMps = f.TopSpeedMps, EnduranceSpeedMps = f.EnduranceSpeedMps }
            : row.Motion;
        return new Running(motion, Math.Max(MinSpeedPct, Body.Speed(body, context) / FreshSpeed(species, body.Sex)), carriedKg);
    }

    /// Floor: a body so hurt or laden it barely moves still pays a finite price for a burst.
    public const double MinSpeedPct = 0.1;

    /// A share of a fresh member's strength, so a species' or weapon's damage is what an average one does:
    /// Strength's kilogram terms are a person's.
    public static double StrengthPct(Species species, BodyState body)
    {
        var sex = species == Species.Human ? body.Sex : Sex.Male;
        lock (FreshStrengths)
        {
            if (!FreshStrengths.TryGetValue((species, sex), out double strength))
                FreshStrengths[(species, sex)] = strength = Body.Strength(Average(species, sex));
            return Body.Strength(body) / strength;
        }
    }

    static readonly Dictionary<(Species, Sex), double> FreshStrengths = [];
    static readonly Dictionary<(Species, Sex), double> FreshSpeeds = [];

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
