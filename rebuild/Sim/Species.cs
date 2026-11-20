namespace Survival;

/// What kind of bodies an agent is made of. Ptarmigan, rat and fish are cut: birds have no body
/// plan any more (plan: Keep/Port/Cut), and the others are small-game pools, not fighters.
public enum Species { Human, Wolf, Bear, CaveBear, SaberTooth, Hyena, Caribou, Megaloceros, Bison, Mammoth, Rabbit, Fox }

/// How an animal meets a threat (old AnimalBehaviorType). Humans are their own row: they never bolt by instinct.
public enum Role { Human, Prey, DangerousPrey, Predator, Scavenger }

/// What makes a herd come at a target (old Temperament): a base, a bonus per head, hunger,
/// defending a kill or den, and a ceiling. Species that never start a fight have Cap 0.
public sealed record Temperament(double Base, double PerMember, double HungryAt, double StarvingAt, double HungerBonus, double DefendBonus, double Cap);

/// One row of the species table: the body plan, how it fights, and the morale numbers combat runs on.
/// Old AnimalFactory and AnimalType; humans are the old Actor defaults and Player's unarmed attack.
public sealed record SpeciesStats(
    BodyKind Body, double WeightKg, double FatPct, double MusclePct,
    Role Role, bool IsSmall,
    double AttackDamage, DamageType AttackType,
    double Threat, double Boldness, double Aggression, double Cohesion,
    /// Metres covered in one combat action at full Moving (old CombatMovement.Allowance).
    int MoveAllowanceM,
    Temperament Temperament)
{
    public bool IsAnimal => Role != Role.Human;
    /// Prey and small scavengers run once they know you are there; fighting is what predators do.
    public bool IsSkittish => Role == Role.Prey || (Role == Role.Scavenger && IsSmall);
}

public static class SpeciesTable
{
    static readonly Temperament Never = new(0, 0, 1.1, 1.1, 0, 0, 0);
    static readonly Temperament Bears = new(0.15, 0, 0.8, 0.95, 0.3, 0.55, 1.0);

    // ponytail: tables in code until phase 4 moves content to data files; plain rows so that move is mechanical.
    static readonly Dictionary<Species, SpeciesStats> Rows = new()
    {
        // Old Player: unarmed 0.15 blunt. Old NPC punched for 0.1; one agent model means one number.
        [Species.Human] = new(BodyKind.Human, Survival.Body.BaselineWeightKg, Survival.Body.BaselineFatPct, Survival.Body.BaselineMusclePct,
            Role.Human, false, 0.15, DamageType.Blunt, 1.0, 1.0, 1.0, 0.5, 3, Never),
        [Species.Wolf] = new(BodyKind.Quadruped, 40, 0.20, 0.60, Role.Predator, false, 0.45, DamageType.Pierce, 0.7, 1.3, 0.8, 2, 6,
            new(0.20, 0.05, 0.7, 0.9, 0.2, 0.6, 0.9)),
        [Species.Bear] = new(BodyKind.Quadruped, 250, 0.30, 0.55, Role.Predator, false, 0.60, DamageType.Sharp, 2.5, 1.6, 0.4, 0, 5, Bears),
        [Species.CaveBear] = new(BodyKind.Quadruped, 350, 0.35, 0.55, Role.Predator, false, 0.80, DamageType.Sharp, 3.0, 1.8, 0.4, 0, 4, Bears),
        [Species.SaberTooth] = new(BodyKind.Quadruped, 300, 0.10, 0.70, Role.Predator, false, 0.90, DamageType.Pierce, 3.0, 2.0, 0.7, 0, 7,
            new(0.25, 0, 0.7, 0.9, 0.3, 0.5, 1.0)),
        [Species.Hyena] = new(BodyKind.Quadruped, 70, 0.15, 0.65, Role.Scavenger, false, 0.40, DamageType.Blunt, 0.4, 0.7, 0.5, 2, 6,
            new(0.10, 0.08, 0.8, 1.1, 0.2, 0.15, 0.6)),
        [Species.Caribou] = new(BodyKind.Quadruped, 120, 0.15, 0.60, Role.Prey, false, 0.25, DamageType.Blunt, 0.3, 0.3, 0.05, 1, 3, Never),
        [Species.Megaloceros] = new(BodyKind.Quadruped, 600, 0.15, 0.60, Role.DangerousPrey, false, 0.50, DamageType.Blunt, 0.5, 0.4, 0.1, 1, 3, Never),
        [Species.Bison] = new(BodyKind.Quadruped, 800, 0.20, 0.55, Role.DangerousPrey, false, 0.55, DamageType.Pierce, 1.8, 0.5, 0.1, 2, 3, Never),
        // Old AnimalFactory's 6000 kg body; AnimalType.WeightKg said 5000 (tracks only).
        [Species.Mammoth] = new(BodyKind.Quadruped, 6000, 0.35, 0.50, Role.DangerousPrey, false, 1.2, DamageType.Pierce, 3.5, 1.0, 0.1, 2, 3, Never),
        [Species.Rabbit] = new(BodyKind.Quadruped, 2, 0.10, 0.50, Role.Prey, true, 0.02, DamageType.Blunt, 0.05, 0.1, 0.0, 1, 3, Never),
        [Species.Fox] = new(BodyKind.Quadruped, 6, 0.20, 0.55, Role.Scavenger, true, 0.12, DamageType.Pierce, 0.15, 0.4, 0.1, 0, 3, Never),
    };

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
