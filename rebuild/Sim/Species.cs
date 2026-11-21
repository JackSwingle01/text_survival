namespace Survival;

/// What kind of bodies an agent is made of. Ptarmigan, rat and fish are cut: birds have no body
/// plan any more (plan: Keep/Port/Cut), and the others are small-game pools, not fighters.
public enum Species { Human, Wolf, Bear, CaveBear, SaberTooth, Hyena, Caribou, Megaloceros, Bison, Mammoth, Rabbit, Fox }

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
    Diet Diet)
{
    public bool IsAnimal => Role != Role.Human;
    /// Prey and small scavengers run once they know you are there; fighting is what predators do.
    public bool IsSkittish => Role == Role.Prey || (Role == Role.Scavenger && IsSmall);
}

public static class SpeciesTable
{
    static readonly IReadOnlyDictionary<Species, SpeciesStats> Rows = Content.Tables.Species;

    public static SpeciesStats Of(Species s) =>
        Rows.TryGetValue(s, out var row) ? row : throw new KeyNotFoundException($"No species row for {s}");

    /// A new member: the species' plan at a random size, most near average (old Animal.GenerateTraits).
    /// A human is the dressed baseline adult.
    public static BodyState NewBody(Species species, Rng rng)
    {
        if (species == Species.Human) return BodyState.BaselineHuman();
        var row = Of(species);
        // Average of two uniforms: roughly normal around 1, spread 0.15, kept to 0.7-1.3.
        double size = Math.Clamp(1.0 + ((rng.NextDouble() + rng.NextDouble()) / 2 - 0.5) * 2 * 0.15, 0.7, 1.3);
        return BodyState.Create(row.Body, row.WeightKg * size, row.FatPct, row.MusclePct);
    }
}
