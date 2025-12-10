namespace text_survival;

/// <summary>
/// Utility class for skill check calculations
/// </summary>
public static class SkillCheckCalculator
{
    /// <summary>
    /// Calculates success chance for a skill check
    /// </summary>
    /// <param name="baseChance">Base success chance (0-1)</param>
    /// <param name="skillLevel">Current skill level</param>
    /// <param name="skillDC">Skill check difficulty class (0 = no DC)</param>
    /// <returns>Success chance clamped to 0.05-0.95</returns>
    public static double CalculateSuccessChance(double baseChance, int skillLevel, int skillDC = 0)
    {
        double successChance = baseChance;

        if (skillDC > 0)
        {
            // Penalty/bonus based on skill vs DC (+/- 10% per level difference)
            double skillModifier = (skillLevel - skillDC) * 0.1;
            successChance += skillModifier;
        }
        else
        {
            // No DC: flat bonus from skill level (+10% per level)
            double skillModifier = skillLevel * 0.1;
            successChance += skillModifier;
        }

        // Clamp to 5%-95% to preserve player agency
        return Math.Clamp(successChance, 0.05, 0.95);
    }

    /// <summary>
    /// Calculates XP reward for skill check attempt
    /// </summary>
    /// <param name="success">Whether the check succeeded</param>
    /// <param name="successXP">XP to award on success</param>
    /// <param name="failureXP">XP to award on failure (default: 1)</param>
    /// <returns>XP to award</returns>
    public static int CalculateXPReward(bool success, int successXP, int failureXP = 1)
    {
        return success ? successXP : failureXP;
    }
}
