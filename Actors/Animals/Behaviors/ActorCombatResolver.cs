using text_survival.Combat;
using text_survival.Effects;
using text_survival.Environments;

namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Resolves off-screen combat between predators and NPCs.
/// Quick probability-based resolution with actual body damage.
/// </summary>
public static class ActorCombatResolver
{
    public enum CombatOutcome
    {
        DefenderEscaped,    // NPC got away
        DefenderInjured,    // NPC wounded but alive
        DefenderKilled,     // NPC died
        AttackerRepelled    // NPC fought off predator
    }

    /// <summary>
    /// Resolve combat between any actors (generalized).
    /// </summary>
    public static CombatOutcome ResolveCombat(
        List<Actor> combatants,
        Location location)
    {
        if (combatants.Count < 2)
            throw new ArgumentException("Combat requires at least 2 actors");

        // Wrap actors with ICombatActor interface
        var actors = combatants.Select(CreateCombatActor).ToList();

        // 1. Check for escape attempts (first actor tries to flee)
        var escaper = actors[0];
        var pursuer = actors[1];

        double escapeChance = CalculateEscapeChance(escaper, pursuer, location);
        if (Random.Shared.NextDouble() < escapeChance)
        {
            // Escaper got away - add fear effect
            escaper.ActorReference.EffectRegistry.AddEffect(EffectFactory.Fear(0.4));
            return CombatOutcome.DefenderEscaped;
        }

        // 2. Combat occurs - simulate 1-3 quick rounds
        int rounds = Random.Shared.Next(1, 4);

        for (int i = 0; i < rounds; i++)
        {
            // Each actor attacks in order
            for (int j = 0; j < actors.Count; j++)
            {
                var attacker = actors[j];
                var defender = actors[(j + 1) % actors.Count]; // Next actor in list

                if (!attacker.IsAlive || !defender.IsAlive)
                    continue;

                // Attack
                ApplyAttack(attacker.ActorReference, defender.ActorReference);

                // Check for death
                if (!defender.IsAlive)
                {
                    return j == 0 ? CombatOutcome.DefenderKilled : CombatOutcome.AttackerRepelled;
                }
            }
        }

        // Both survived - determine outcome based on condition
        return actors[1].Vitality < 0.5
            ? CombatOutcome.DefenderInjured
            : CombatOutcome.AttackerRepelled;
    }

    /// <summary>
    /// Resolve combat between a predator and an NPC (convenience wrapper).
    /// </summary>
    public static CombatOutcome ResolveCombat(
        Animal attacker,
        NPC defender,
        Location location)
    {
        return ResolveCombat(new List<Actor> { attacker, defender }, location);
    }

    /// <summary>
    /// Creates ICombatActor wrapper for any actor type.
    /// Note: Player combat should use interactive mode (CombatRunner), not automatic resolution.
    /// </summary>
    private static ICombatActor CreateCombatActor(Actor actor)
    {
        return actor switch
        {
            text_survival.Actors.Player.Player player =>
                throw new InvalidOperationException("Player combat should use interactive mode (CombatRunner.RunCombat), not automatic resolution"),
            NPC npc => new NPCCombatActor(npc),
            Animal animal => new AnimalCombatActor(animal),
            _ => throw new ArgumentException($"Unknown actor type: {actor.GetType()}")
        };
    }

    /// <summary>
    /// Calculate escape chance for any actor types (generalized).
    /// </summary>
    private static double CalculateEscapeChance(
        ICombatActor escaper, ICombatActor pursuer, Location location)
    {
        // Base 30% escape chance
        double chance = 0.3;

        // Speed advantage
        double speedDiff = escaper.Speed - pursuer.Speed;
        chance += speedDiff * 0.3;  // ±30% for speed difference

        // Injured actors struggle to escape
        if (escaper.Vitality < 0.7)
            chance -= 0.2;

        // Terrain affects escape
        if (location.IsEscapeTerrain)
            chance += 0.3;  // Thick brush helps escape

        return Math.Clamp(chance, 0.05, 0.95);
    }

    private static void ApplyAttack(Actor attacker, Actor defender)
    {
        var damageInfo = attacker.GetAttackDamage();
        var result = defender.Damage(damageInfo);
        Console.WriteLine($"[Combat] {attacker.Name} dealt {result.TotalDamageDealt:F1} damage to {defender.Name}");
    }
}
