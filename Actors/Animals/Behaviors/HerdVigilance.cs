namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Whether a prey herd notices a stalking predator before it strikes.
/// The answer only sets how the fight opens: noticed prey start Alert, surprised prey start Unaware.
/// The fight itself runs on the combat grid like every other fight.
/// </summary>
public static class HerdVigilance
{

    public static bool PreyNoticesPredator(Herd predator, Herd prey)
    {
        double preyVigilance = CalculatePreyVigilance(prey);
        double predatorStealth = CalculatePredatorStealth(predator);

        double preyRoll = Utils.Rng.NextDouble() * preyVigilance;
        double predatorRoll = Utils.Rng.NextDouble() * predatorStealth;

        return preyRoll > predatorRoll;
    }

    private static double CalculatePreyVigilance(Herd prey)
    {
        double vigilance = 0.5;

        // Herd size (logarithmic - more eyes)
        vigilance += Math.Log(prey.Count + 1) * 0.25;

        vigilance *= prey.State switch
        {
            HerdState.Grazing => 1.0,   // Moving around, somewhat alert
            HerdState.Resting => 0.6,   // Bedded down, less alert
            HerdState.Alert => 1.5,     // Already on high alert
            HerdState.Fleeing => 1.3,   // Running, very aware
            _ => 1.0
        };

        // Just stopped fleeing = hyper-vigilant
        if (prey.StateTimeMinutes < 30 && prey.State == HerdState.Resting)
            vigilance *= 1.4;

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
