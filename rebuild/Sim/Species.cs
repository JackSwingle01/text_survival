namespace Survival;

public enum Species { Human, Wolf, Bear, CaveBear, SaberTooth, Hyena, Caribou, Megaloceros, Bison, Mammoth, Hare, Fox, Raven }

/// Who an animal takes for kin: any of its species (Open), or only those it is familiar with (Closed).
public enum Group { Open, Closed }

/// The hours a species is about: by day, at dawn and dusk (Weather.IsTwilight), or by night.
public enum ActivityPattern { Diurnal, Crepuscular, Nocturnal }

[Flags]
public enum Ability { None = 0, Flies = 1 << 0, Climbs = 1 << 1, Digs = 1 << 2, Strong = 1 << 3, Hands = 1 << 4 }

public enum Role { Human, Prey, DangerousPrey, Predator, Scavenger }

public sealed record Temperament(double Base, double PerMember, double HungryAt, double StarvingAt, double HungerBonus, double DefendBonus, double Cap);

public sealed record SpeciesStats(
    BodyKind Body, double WeightKg, double FatPct, double MusclePct,
    Role Role, bool IsSmall,
    double AttackDamage, DamageType AttackType,
    double Threat, double Boldness, double Aggression, double Cohesion,
    Group Group,
    int MoveAllowanceM,
    double ReachM,
    Temperament Temperament,
    HerdBehavior Behavior,
    Diet Diet,
    IReadOnlyList<Resource> Grazes,
    Motion Motion,
    double GrazeKcalPerMinute,
    /// Giving ground with the enemy in reach, it strikes instead of backing off at this resolve or above.
    double StrikeBackResolve,
    double ChaseLimitSeconds,
    double SleepHoursPerDay,
    ActivityPattern Activity,
    FatRange? AutumnFatPct,
    PersonBuild? Female,
    double SmellFactor,
    double ScentLevel,
    int? FlightMinutesPerTile = null,
    double PerceptionFactor = 1,
    double StealthFactor = 1,
    Ability Abilities = Ability.None)
{
    public bool IsAnimal => Role != Role.Human;
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
    // By the species' number: read for every animal many times a minute.
    static readonly SpeciesStats?[] Rows = ByNumber(Content.Tables.Species);

    static SpeciesStats?[] ByNumber(IReadOnlyDictionary<Species, SpeciesStats> table)
    {
        var rows = new SpeciesStats?[table.Keys.Max(s => (int)s) + 1];
        foreach (var (species, row) in table) rows[(int)species] = row;
        return rows;
    }

    public static SpeciesStats Of(Species s) =>
        (uint)s < (uint)Rows.Length && Rows[(int)s] is { } row ? row : throw new KeyNotFoundException($"No species row for {s}");

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
        Refreshed(body);
        return body;
    }

    static void Refreshed(BodyState body)
    {
        body.HydrationMl = Body.MaxHydrationMlOf(body);
        body.EnergyMinutes = Body.MaxEnergyMinutes;
    }

    public static BodyState Average(Species species, Sex sex = Sex.Male)
    {
        if (species == Species.Human) return BodyState.BaselineHuman(sex);
        var row = Of(species);
        var body = BodyState.Create(row.Body, row.WeightKg, row.FatPct, row.MusclePct);
        body.CaloriesKcal = Body.CaloriesCapacityKcal(body);
        Refreshed(body);
        return body;
    }

    /// A share of a fresh member's speed, not Body.Speed itself: Speed's kilogram terms are a person's,
    /// and against its own kind they cancel.
    public static Running Running(Species species, BodyState body, AbilityContext context = default, double carriedKg = 0) =>
        RunningIfAble(species, body, context, carriedKg)
        ?? throw new InvalidOperationException($"A {species} at speed zero cannot run: ask RunningIfAble");

    /// Null for a body at speed zero: it cannot run, and nothing about running is computed for it. Above zero, held at MinSpeedPct.
    public static Running? RunningIfAble(Species species, BodyState body, AbilityContext context = default, double carriedKg = 0)
    {
        double speed = Body.Speed(body, context);
        if (speed <= 0) return null;
        var row = Of(species);
        var motion = species == Species.Human && body.Sex == Sex.Female && row.Female is PersonBuild f
            ? row.Motion with { TopSpeedMps = f.TopSpeedMps, EnduranceSpeedMps = f.EnduranceSpeedMps }
            : row.Motion;
        return new Running(motion, Math.Max(MinSpeedPct, speed / FreshSpeed(species, body.Sex)), carriedKg);
    }

    public const double MinSpeedPct = 0.1;

    /// Body.Speed under the load, held at MinSpeedPct of a fresh body's unloaded speed, the floor running has too; zero stays zero.
    public static double WalkingSpeed(Species species, BodyState body, double encumbrancePct = 0)
    {
        double speed = Body.Speed(body, new AbilityContext(EncumbrancePct: encumbrancePct));
        return speed > 0 ? Math.Max(speed, MinSpeedPct * FreshSpeed(species, body.Sex)) : 0;
    }

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

    /// By its structure, which starving never spends: a member is born at its species' build at any size (NewBody).
    public static double FreshMuscleKg(Species species, BodyState body)
    {
        var row = Of(species);
        var (fatPct, musclePct) = species == Species.Human && body.Sex == Sex.Female && row.Female is PersonBuild f
            ? (f.FatPct, f.MusclePct) : (row.FatPct, row.MusclePct);
        return body.StructuralKg * musclePct / (1 - fatPct - musclePct);
    }

    public static double SpeedPct(Species species, BodyState body) => Math.Min(1, Body.Speed(body) / FreshSpeed(species, body.Sex));

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
