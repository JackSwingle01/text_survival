using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;

namespace text_survival.Effects;

public class BleedEffect : Effect
{
    private float _damagePerHour;

    public BleedEffect(string source, MajorBodyPart? targetPart, float severity, float damagePerHour, int durationMin = -1)
        : base("Bleeding", source, targetPart, severity)
    {
        _damagePerHour = damagePerHour;

        // Configure effect properties
        SeverityChangeRate = -0.05f; // Natural clotting
        CanHaveMultiple = true; // Multiple cuts can stack

        // Configure capacity modifiers
        CapacityModifiers["BloodPumping"] = 0.2f; // Reduces blood pumping capacity by 20%
        CapacityModifiers["Consciousness"] = 0.1f; // Minor consciousness impact
    }

    protected override void OnApply(Actor target)
    {
        string location = TargetBodyPart?.Name ?? "body";
        Output.WriteLine($"{target}'s {location} is bleeding!");
    }

    protected override void OnUpdate(Actor target)
    {
        // Calculate damage for one minute based on severity
        double damage = _damagePerHour / 60.0 * Severity;

        // Apply damage to the specific body part
        var damageInfo = new DamageInfo
        {
            Amount = damage,
            Type = DamageType.Bleed,
            Source = Source,
            TargetPart = TargetBodyPart
        };

        target.Damage(damageInfo);

        // Occasionally remind player of bleeding
        if (Utils.DetermineSuccess(0.05f) && Severity > 0.3f)
        {
            string location = TargetBodyPart?.Name ?? "body";
            Output.WriteLine($"Blood continues to flow from {target}'s {location}...");
        }
    }

    protected override void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity)
    {
        if (updatedSeverity < 0.2f && oldSeverity >= 0.2f)
        {
            string location = TargetBodyPart?.Name ?? "body";
            Output.WriteLine($"The bleeding on {target}'s {location} is slowing.");
        }
    }

    protected override void OnRemove(Actor target)
    {
        string location = TargetBodyPart?.Name ?? "body";
        Output.WriteLine($"The bleeding on {target}'s {location} has stopped.");
    }
}