namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Resolves NPC predator-prey interactions when they share a tile.
/// </summary>
public static class PredatorPreyResolver
{
    private static readonly Random _rng = new();

    public enum HuntResolution
    {
        PreyEscaped,
        AttackInitiated
    }

    /// <summary>
    /// Resolve whether predator can initiate attack or prey escapes.
    /// Based on vigilance vs stealth.
    /// </summary>
    public static HuntResolution ResolvePredatorPreyEncounter(Herd predator, Herd prey)
    {
        double preyVigilance = CalculatePreyVigilance(prey);
        double predatorStealth = CalculatePredatorStealth(predator);

        double preyRoll = _rng.NextDouble() * preyVigilance;
        double predatorRoll = _rng.NextDouble() * predatorStealth;

        return preyRoll > predatorRoll
            ? HuntResolution.PreyEscaped
            : HuntResolution.AttackInitiated;
    }

    /// <summary>
    /// Attempt to catch and kill prey after attack is initiated.
    /// </summary>
    public static bool AttemptPreyKill(Herd predator, Herd prey)
    {
        // Target the weakest member (slowest effective speed = base speed * condition)
        var target = prey.Members
            .OrderBy(m => m.SpeedMps * m.Condition)
            .ThenBy(m => m.Condition)
            .FirstOrDefault();

        if (target == null) return false;

        double predatorSpeed = predator.Members.Average(m => m.SpeedMps * m.Condition);
        double targetSpeed = target.SpeedMps * target.Condition;

        double catchChance = 0.3;

        // Speed comparison
        if (predatorSpeed > targetSpeed)
            catchChance += (predatorSpeed - targetSpeed) * 0.4;
        else
            catchChance -= (targetSpeed - predatorSpeed) * 0.3;

        // Target condition
        if (target.Condition < 0.7) catchChance += 0.2;
        if (target.Condition < 0.4) catchChance += 0.3;

        // Pack size advantage
        if (predator.Count >= 4) catchChance += 0.15;

        // Confusion in large herd (easier to isolate one)
        if (prey.Count > 8) catchChance += 0.1;

        catchChance = Math.Clamp(catchChance, 0.1, 0.7);

        return _rng.NextDouble() < catchChance;
    }

    private static double CalculatePreyVigilance(Herd prey)
    {
        double vigilance = 0.5;

        // Herd size (logarithmic - more eyes)
        vigilance += Math.Log(prey.Count + 1) * 0.25;

        // State affects vigilance
        vigilance *= prey.State switch
        {
            HerdState.Grazing => 1.0,   // Moving around, somewhat alert
            HerdState.Resting => 0.6,   // Bedded down, less alert
            HerdState.Alert => 1.5,     // Already on high alert
            HerdState.Fleeing => 1.3,   // Running, very aware
            _ => 1.0
        };

        // Recently spooked = hyper-vigilant
        if (prey.StateTimeMinutes < 30 && prey.State == HerdState.Resting)
        {
            // Just stopped fleeing
            vigilance *= 1.4;
        }

        return vigilance;
    }

    private static double CalculatePredatorStealth(Herd predator)
    {
        double stealth = 0.5;

        // Larger packs are less stealthy
        stealth -= predator.Count * 0.02;

        // Hunger makes predators more focused
        if (predator.Hunger > 0.7) stealth += 0.15;
        if (predator.Hunger > 0.9) stealth += 0.15;

        // Animal type affects stealth
        stealth *= predator.AnimalType switch
        {
            AnimalType.Wolf => 1.2,                          // Wolves are skilled stalkers
            AnimalType.Bear or AnimalType.CaveBear => 0.7,   // Bears are less stealthy
            AnimalType.SaberTooth => 1.3,                    // Ambush predator
            AnimalType.Hyena => 0.8,                         // Noisy scavengers
            _ => 1.0
        };

        return Math.Max(0.2, stealth);
    }
}
