using text_survival.Actors.Animals;
using text_survival.Actors.Player;

namespace text_survival;

/// <summary>
/// Utility class for hunting and stealth calculations.
/// Contains formulas for detection, approach, and ranged combat.
/// </summary>
public static class HuntingCalculator
{
    #region Distance and Approach

    /// <summary>
    /// Calculates distance reduction for an approach action (20-30m).
    /// </summary>
    /// <returns>Distance reduction in meters</returns>
    public static double CalculateApproachDistance()
    {
        return Utils.RandInt(20, 30); // 20-30m reduction per approach
    }

    /// <summary>
    /// Returns time cost for approach action in minutes (5-10 minutes).
    /// </summary>
    public static int CalculateApproachTime()
    {
        return Utils.RandInt(5, 10);
    }

    #endregion

    #region Detection Formulas

    /// <summary>
    /// Calculates the chance of being detected by an animal during approach.
    /// Detection difficulty increases exponentially as distance decreases.
    /// </summary>
    /// <param name="distance">Current distance from animal in meters</param>
    /// <param name="animalState">Animal's current awareness state</param>
    /// <param name="huntingSkill">Player's Hunting skill level</param>
    /// <param name="failedAttempts">Number of previous failed stealth checks</param>
    /// <returns>Detection chance (0.0 - 1.0)</returns>
    public static double CalculateDetectionChance(
        double distance,
        AnimalState animalState,
        int huntingSkill,
        int failedAttempts = 0)
    {
        // Base detection chance increases as distance decreases
        // 100m = ~0% detection, 50m = ~20%, 30m = ~50%, 10m = ~90%
        double baseDetectionChance = 1.0 - (distance / 100.0); // Linear scaling
        baseDetectionChance = Math.Pow(baseDetectionChance, 0.7); // Exponential curve (makes close range very dangerous)

        // Animal state modifier
        double stateModifier = animalState switch
        {
            AnimalState.Idle => 1.0,      // Normal detection
            AnimalState.Alert => 1.5,     // 50% more likely to detect
            AnimalState.Detected => 2.0,  // Already detected (flee/attack imminent)
            _ => 1.0
        };

        baseDetectionChance *= stateModifier;

        // Skill modifier: -5% detection per Hunting level
        double skillModifier = huntingSkill * 0.05;
        baseDetectionChance -= skillModifier;

        // Failed attempts penalty: +10% per failed check
        double failureModifier = failedAttempts * 0.10;
        baseDetectionChance += failureModifier;

        // Clamp to 5%-95% range
        return Math.Clamp(baseDetectionChance, 0.05, 0.95);
    }

    /// <summary>
    /// Determines if detection should escalate animal to Alert state (partial detection).
    /// </summary>
    /// <param name="detectionRoll">Random roll (0-1)</param>
    /// <param name="detectionChance">Calculated detection chance</param>
    /// <returns>True if animal becomes alert (but not fully detected)</returns>
    public static bool ShouldBecomeAlert(double detectionRoll, double detectionChance)
    {
        // If roll is within 20% of detection threshold, animal becomes alert
        double alertThreshold = detectionChance - 0.20;
        return detectionRoll >= alertThreshold && detectionRoll < detectionChance;
    }

    /// <summary>
    /// Calculates detection chance with animal traits and activity factored in.
    /// More nervous animals are harder to approach. Grazing animals are easier.
    /// </summary>
    /// <param name="distance">Current distance from animal in meters</param>
    /// <param name="animal">The target animal (provides state, nervousness, activity)</param>
    /// <param name="huntingSkill">Player's Hunting skill level</param>
    /// <param name="failedAttempts">Number of previous failed stealth checks</param>
    /// <returns>Detection chance (0.0 - 1.0)</returns>
    public static double CalculateDetectionChanceWithTraits(
        double distance,
        Animal animal,
        int huntingSkill,
        int failedAttempts = 0)
    {
        // Get base detection using existing formula
        double baseChance = CalculateDetectionChance(distance, animal.State, huntingSkill, failedAttempts);

        // Apply nervousness modifier: nervous (0.9) = 1.4x harder, calm (0.1) = 0.6x easier
        // Formula: 0.5 + nervousness maps [0,1] to [0.5, 1.5]
        double nervousnessModifier = 0.5 + animal.Nervousness;
        baseChance *= nervousnessModifier;

        // Apply activity modifier from animal
        baseChance *= animal.GetActivityDetectionModifier();

        return Math.Clamp(baseChance, 0.05, 0.95);
    }

    #endregion

    #region Ranged Combat (Phase 3)

    /// <summary>
    /// Calculates accuracy for ranged attack based on distance and skill.
    /// Used in Phase 3 (Bow Hunting).
    /// </summary>
    /// <param name="distance">Distance to target in meters</param>
    /// <param name="weaponAccuracy">Base weapon accuracy modifier</param>
    /// <param name="huntingSkill">Player's Hunting skill level</param>
    /// <param name="isConcealed">Whether player is still concealed (bonus)</param>
    /// <returns>Hit chance (0.0 - 1.0)</returns>
    public static double CalculateRangedAccuracy(
        double distance,
        double weaponAccuracy,
        int huntingSkill,
        bool isConcealed = false)
    {
        // Effective range for bow: 30-50m optimal
        double rangeModifier = 1.0;

        if (distance < 20)
            rangeModifier = 0.7; // Too close - hard to aim
        else if (distance <= 50)
            rangeModifier = 1.0; // Optimal range
        else if (distance <= 70)
            rangeModifier = 0.8; // Suboptimal but viable
        else
            rangeModifier = 0.5; // Beyond effective range

        // Base accuracy from weapon
        double hitChance = weaponAccuracy * rangeModifier;

        // Skill modifier: +5% per level
        hitChance += (huntingSkill * 0.05);

        // Concealment bonus: +15% if undetected
        if (isConcealed)
            hitChance += 0.15;

        // Clamp to 5%-95%
        return Math.Clamp(hitChance, 0.05, 0.95);
    }

    /// <summary>
    /// Calculates damage multiplier for critical hits (headshots, vital organs).
    /// Used with Body.Damage() system in Phase 3.
    /// </summary>
    /// <param name="targetBodyPart">Targeted body part name (e.g., "head", "torso")</param>
    /// <returns>Damage multiplier</returns>
    public static double CalculateCriticalMultiplier(string targetBodyPart)
    {
        return targetBodyPart.ToLower() switch
        {
            "head" => 2.0,      // Headshots are lethal
            "brain" => 3.0,     // Brain hits instantly lethal
            "heart" => 2.5,     // Heart hits very lethal
            "torso" => 1.0,     // Standard damage
            _ => 1.0
        };
    }

    #endregion

    #region Thrown Weapons

    /// <summary>
    /// Calculates accuracy for thrown weapons (spears, stones).
    /// Simple formula: closer is better, no optimal range band.
    /// </summary>
    /// <param name="distance">Distance to target in meters</param>
    /// <param name="maxRange">Maximum effective range of weapon</param>
    /// <param name="baseAccuracy">Base accuracy of weapon (0.65 for stone, 0.70-0.75 for spears)</param>
    /// <param name="targetIsSmall">True if targeting small game (applies 50% penalty for spears)</param>
    /// <returns>Hit chance (0.0 - 1.0)</returns>
    public static double CalculateThrownAccuracy(
        double distance,
        double maxRange,
        double baseAccuracy,
        bool targetIsSmall)
    {
        // Beyond max range = 0%
        if (distance > maxRange) return 0;

        // Closer is better (linear falloff from max range)
        double accuracy = baseAccuracy * (1.0 - distance / maxRange);

        // Small target penalty (spears only — stones pass false)
        if (targetIsSmall) accuracy *= 0.5;

        return Math.Clamp(accuracy, 0.05, 0.95);
    }

    #endregion

    #region Pursuit Resolution

    /// <summary>
    /// Calculates whether a player can escape a pursuing predator.
    /// Uses physics-based calculation: can player maintain enough distance until predator gives up?
    /// </summary>
    /// <param name="player">The fleeing player</param>
    /// <param name="predator">The pursuing animal</param>
    /// <param name="headStartMeters">Current distance (head start) in meters</param>
    /// <returns>Tuple of (escaped, narrative description)</returns>
    public static (bool escaped, string narrative) CalculatePursuitOutcome(
        Player player,
        Animal predator,
        double headStartMeters)
    {
        // Player speed (uses body capacity for injuries)
        double playerBaseSpeed = 6.0; // m/s jogging
        double movementCapacity = player.GetCapacities().Moving;
        double playerSpeed = playerBaseSpeed * movementCapacity;

        // Predator speed from animal data
        double predatorSpeed = predator.SpeedMps;

        // Can player outrun?
        if (playerSpeed >= predatorSpeed)
        {
            return (true, $"You're faster than the {predator.Name}. You escape easily.");
        }

        // Pursuit calculation
        double speedDiff = predatorSpeed - playerSpeed;
        double catchTime = headStartMeters / speedDiff;

        // Predator commitment from animal data
        double commitment = predator.PursuitCommitmentSeconds;

        if (catchTime > commitment)
        {
            return (true, $"The {predator.Name} chases but gives up after {commitment:F0} seconds. You escape.");
        }
        else
        {
            return (false, $"The {predator.Name} is faster. It catches you in {catchTime:F0} seconds!");
        }
    }

    #endregion

    #region Blood Trail Tracking (Phase 4)

    /// <summary>
    /// Calculates blood trail freshness decay over time.
    /// Used in Phase 4 (Blood Trail Tracking).
    /// </summary>
    /// <param name="initialFreshness">Starting freshness (0-1)</param>
    /// <param name="minutesElapsed">Time since wound</param>
    /// <param name="woundSeverity">Severity of wound (affects decay rate)</param>
    /// <returns>Current trail freshness (0-1)</returns>
    public static double CalculateTrailFreshness(
        double initialFreshness,
        int minutesElapsed,
        double woundSeverity)
    {
        // Heavy wounds leave trails longer (slower decay)
        double decayRate = 0.1 - (woundSeverity * 0.05); // Higher severity = slower decay
        double freshness = initialFreshness - (minutesElapsed * decayRate / 60.0); // Per hour decay

        return Math.Clamp(freshness, 0.0, 1.0);
    }

    /// <summary>
    /// Calculates success chance for tracking wounded animal.
    /// </summary>
    /// <param name="trailFreshness">Current trail freshness (0-1)</param>
    /// <param name="huntingSkill">Player's Hunting skill level</param>
    /// <param name="trackingDifficulty">Animal's tracking difficulty (0-10)</param>
    /// <returns>Tracking success chance (0.0 - 1.0)</returns>
    public static double CalculateTrackingChance(
        double trailFreshness,
        int huntingSkill,
        int trackingDifficulty)
    {
        // Base chance from trail freshness (fresh = easier)
        double baseChance = trailFreshness * 0.6; // Max 60% from freshness

        // Skill modifier: +8% per level
        baseChance += (huntingSkill * 0.08);

        // Difficulty penalty: -5% per difficulty point
        baseChance -= (trackingDifficulty * 0.05);

        // Clamp to 5%-95%
        return Math.Clamp(baseChance, 0.05, 0.95);
    }

    #endregion
}
