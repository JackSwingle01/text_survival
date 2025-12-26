using text_survival.Actions;
using text_survival.Actions.Expeditions;
using text_survival.Actions.Expeditions.WorkStrategies;
using text_survival.UI;

namespace text_survival.Environments.Features;

/// <summary>
/// Specialized crafting project for upgrading fire pits.
/// Upon completion, upgrades the existing fire pit's type rather than replacing it,
/// preserving all fire state (fuel, embers, temperature).
/// </summary>
public class FirePitUpgradeProject : CraftingProjectFeature
{
    /// <summary>
    /// The target fire pit type to upgrade to.
    /// </summary>
    public FirePitType TargetPitType { get; set; }

    public FirePitUpgradeProject() : base() { }

    public FirePitUpgradeProject(string projectName, FirePitType targetPitType, double timeRequiredMinutes)
        : base(projectName, new HeatSourceFeature(targetPitType), timeRequiredMinutes)
    {
        TargetPitType = targetPitType;
        BenefitsFromShovel = true; // Fire pit upgrades involve digging
    }

    /// <summary>
    /// Override completion logic to modify existing fire pit instead of replacing it.
    /// This preserves the fire state (fuel, embers, temperature).
    /// </summary>
    public new void AddProgress(double minutes, Location location)
    {
        TimeInvestedMinutes += minutes;

        if (IsComplete)
        {
            var existingFire = location.GetFeature<HeatSourceFeature>();

            if (existingFire != null)
            {
                // Upgrade existing fire pit in place (preserves all fire state!)
                existingFire.PitType = TargetPitType;
            }
            else
            {
                // No existing fire pit - create new one with upgraded type
                location.AddFeature(new HeatSourceFeature(TargetPitType));
            }

            // Remove the completed project
            location.RemoveFeature(this);
        }
    }
}
