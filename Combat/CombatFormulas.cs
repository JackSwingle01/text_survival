namespace text_survival.Combat;

/// <summary>
/// The stealth and throwing formulas the combat grid runs on.
/// </summary>
public static class CombatFormulas
{
    /// <summary>
    /// Chance an animal notices something moving at the given distance.
    /// 100m = ~0%, 50m = ~20%, 30m = ~50%, 10m = ~90%, before awareness and skill modifiers.
    /// </summary>
    /// <param name="distance">Distance from the animal in meters</param>
    /// <param name="awareness">The animal's current awareness state</param>
    /// <param name="huntingSkill">Hunting skill level of the one being noticed (-5% per level)</param>
    /// <param name="failedAttempts">Previous failed stealth checks (+10% each)</param>
    public static double CalculateDetectionChance(
        double distance,
        AwarenessState awareness,
        int huntingSkill,
        int failedAttempts = 0)
    {
        double baseDetectionChance = 1.0 - (distance / 100.0);
        baseDetectionChance = Math.Pow(baseDetectionChance, 0.7); // Close range is very dangerous

        double stateModifier = awareness switch
        {
            AwarenessState.Unaware => 1.0,
            AwarenessState.Alert => 1.5,
            AwarenessState.Engaged => 2.0,
            _ => 1.0
        };
        baseDetectionChance *= stateModifier;

        baseDetectionChance -= huntingSkill * 0.05;
        baseDetectionChance += failedAttempts * 0.10;

        return Math.Clamp(baseDetectionChance, 0.05, 0.95);
    }

    /// <summary>
    /// A roll within 20% of the detection threshold is a near miss: the animal becomes alert without spotting you.
    /// </summary>
    public static bool ShouldBecomeAlert(double detectionRoll, double detectionChance)
    {
        double alertThreshold = detectionChance - 0.20;
        return detectionRoll >= alertThreshold && detectionRoll < detectionChance;
    }

    /// <summary>
    /// Hit chance for a thrown spear or stone. Closer is better, with a linear falloff to the weapon's max range.
    /// </summary>
    /// <param name="targetIsSmall">Small game is harder to hit (0.66 multiplier)</param>
    /// <param name="manipulationPenalty">Flat penalty from impaired hands (e.g. 0.15)</param>
    public static double CalculateThrownAccuracy(
        double distance,
        double maxRange,
        double baseAccuracy,
        bool targetIsSmall,
        double manipulationPenalty = 0.0)
    {
        if (distance > maxRange) return 0;

        double accuracy = baseAccuracy * (1.0 - distance / maxRange);
        if (targetIsSmall) accuracy *= 0.66;
        accuracy -= manipulationPenalty;

        return Math.Clamp(accuracy, 0.05, 0.95);
    }
}
