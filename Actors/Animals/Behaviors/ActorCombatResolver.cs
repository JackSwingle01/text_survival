using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Environments;
using text_survival.Items;

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
    /// Resolve combat between a predator and an NPC.
    /// </summary>
    public static CombatOutcome ResolveCombat(
        Animal attacker,
        NPC defender,
        Location location)
    {
        // 1. Calculate escape chance
        double escapeChance = CalculateEscapeChance(defender, attacker, location);

        if (Random.Shared.NextDouble() < escapeChance)
        {
            // NPC escapes - add fear effect
            defender.EffectRegistry.AddEffect(EffectFactory.Fear(0.4));
            return CombatOutcome.DefenderEscaped;
        }

        // 2. Combat occurs - simulate 1-3 quick rounds
        int rounds = Random.Shared.Next(1, 4);

        for (int i = 0; i < rounds; i++)
        {
            // Predator attacks NPC
            ApplyAttack(attacker, defender);

            if (!defender.IsAlive)
                return CombatOutcome.DefenderKilled;

            // NPC counterattacks if equipped
            var weapon = defender.Inventory.Weapon;
            if (weapon != null && !weapon.IsBroken)
            {
                ApplyAttack(defender, attacker);

                if (!attacker.IsAlive)
                    return CombatOutcome.AttackerRepelled;
            }
        }

        // Both survived - determine outcome based on condition
        return defender.Vitality < 0.5
            ? CombatOutcome.DefenderInjured
            : CombatOutcome.AttackerRepelled;
    }

    private static double CalculateEscapeChance(
        NPC defender, Animal attacker, Location location)
    {
        // Base 30% escape chance
        double chance = 0.3;

        // NPC speed advantage
        double speedDiff = defender.Speed - attacker.Speed;
        chance += speedDiff * 0.3;  // ±30% for speed difference

        // Injured NPCs struggle to escape
        if (defender.Vitality < 0.7)
            chance -= 0.2;

        // Terrain affects escape
        if (location.IsEscapeTerrain)
            chance += 0.3;  // Thick brush helps escape

        // Pack predators harder to escape
        // (Could check for nearby pack members via Herds)

        return Math.Clamp(chance, 0.05, 0.95);
    }

    private static void ApplyAttack(Actor attacker, Actor defender)
    {
        // Create damage info
        var damageInfo = new DamageInfo(
            attacker.AttackDamage,
            attacker.AttackType);

        // Apply armor if defender has inventory
        if (defender is NPC npc)
        {
            damageInfo.ArmorCushioning = npc.Inventory.TotalCushioning;
            damageInfo.ArmorToughness = npc.Inventory.TotalToughness;
        }

        // Apply damage to body (uses existing damage system)
        var result = DamageProcessor.DamageBody(damageInfo, defender.Body);

        // Apply triggered effects (bleeding, pain)
        foreach (var effect in result.TriggeredEffects)
        {
            defender.EffectRegistry.AddEffect(effect);
        }

        // Log damage for console output
        Console.WriteLine($"[Combat] {attacker.Name} dealt {result.TotalDamageDealt:F1} damage to {defender.Name}");
    }
}
