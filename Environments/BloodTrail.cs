using text_survival.Actors.NPCs;
using text_survival.Core;

namespace text_survival.Environments;

/// <summary>
/// Represents a blood trail left by a wounded animal.
/// Tracks freshness, severity, and provides tracking difficulty calculations.
/// </summary>
public class BloodTrail
{
    /// <summary>
    /// The wounded animal that created this trail.
    /// </summary>
    public Animal SourceAnimal { get; set; }

    /// <summary>
    /// Location where the blood trail starts (where animal was wounded).
    /// </summary>
    public Location OriginLocation { get; set; }

    /// <summary>
    /// Location where the wounded animal fled to (null if unknown/not tracked yet).
    /// </summary>
    public Location? DestinationLocation { get; set; }

    /// <summary>
    /// Game time when the trail was created.
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// Severity of the wound (0.0 = minor, 1.0 = critical).
    /// Affects visibility of trail and bleed-out rate.
    /// </summary>
    public double WoundSeverity { get; set; }

    /// <summary>
    /// Whether the trail has been successfully tracked to completion.
    /// </summary>
    public bool IsTracked { get; set; }

    public BloodTrail(Animal sourceAnimal, Location originLocation, double woundSeverity, DateTime currentTime)
    {
        SourceAnimal = sourceAnimal;
        OriginLocation = originLocation;
        WoundSeverity = woundSeverity;
        CreatedTime = currentTime;
        IsTracked = false;
    }

    /// <summary>
    /// Gets freshness of the trail (1.0 = fresh, 0.0 = completely faded).
    /// Trails fade over 8 hours.
    /// </summary>
    public double GetFreshness(DateTime currentTime)
    {
        TimeSpan elapsed = currentTime - CreatedTime;
        double hoursElapsed = elapsed.TotalHours;

        // Trail fades completely after 8 hours
        const double fadeTime = 8.0;
        double freshness = 1.0 - (hoursElapsed / fadeTime);

        return Math.Clamp(freshness, 0.0, 1.0);
    }

    /// <summary>
    /// Calculates tracking difficulty based on freshness, wound severity, and skill.
    /// Returns success chance (0.0 to 1.0).
    /// </summary>
    public double GetTrackingSuccessChance(int huntingSkill, DateTime currentTime)
    {
        double freshness = GetFreshness(currentTime);

        // Base chance from freshness and severity
        // Fresh, severe wounds = easy to track (90%)
        // Old, minor wounds = hard to track (10%)
        double baseChance = (freshness * 0.5) + (WoundSeverity * 0.4);

        // Skill bonus: +5% per level
        double skillBonus = huntingSkill * 0.05;

        double totalChance = baseChance + skillBonus;

        return Math.Clamp(totalChance, 0.05, 0.95);
    }

    /// <summary>
    /// Gets description of trail condition for player.
    /// </summary>
    public string GetTrailDescription(DateTime currentTime)
    {
        double freshness = GetFreshness(currentTime);

        if (freshness > 0.8)
            return $"Fresh blood trail from {SourceAnimal.Name} (very recent)";
        else if (freshness > 0.5)
            return $"Blood trail from {SourceAnimal.Name} (a few hours old)";
        else if (freshness > 0.2)
            return $"Fading blood trail from {SourceAnimal.Name} (several hours old)";
        else if (freshness > 0.0)
            return $"Nearly faded blood trail from {SourceAnimal.Name} (very old)";
        else
            return $"Completely faded trail (untrackable)";
    }

    /// <summary>
    /// Gets severity description for player feedback.
    /// </summary>
    public string GetSeverityDescription()
    {
        if (WoundSeverity > 0.7)
            return "Heavy bleeding (critical wound)";
        else if (WoundSeverity > 0.4)
            return "Moderate bleeding (serious wound)";
        else
            return "Light bleeding (minor wound)";
    }
}
