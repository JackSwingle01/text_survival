using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;

namespace text_survival.Effects;

public class ShiveringEffect : Effect
{
    private double _metabolismBoost;

    public ShiveringEffect(BodyPart bodyPart, double severity)
        : base("Shivering", "Cold exposure", bodyPart, severity, -0.1) // Naturally decreases when not refreshed
    {
        // Calculate metabolism boost based on severity
        _metabolismBoost = severity * 0.2; // Up to 20% metabolism increase

        // Setup capacity modifiers - shivering affects fine motor control
        CapacityModifiers["Manipulation"] = -0.2 * severity;

        IsStackable = false; // Use EffectRegistry's stacking mechanism
    }

    protected override void OnUpdate(Actor target)
    {
        // Let base class handle natural severity reduction
        base.OnUpdate(target);

        // Any other shivering-specific update logic could go here
    }

    protected override void OnApply(Actor target)
    {
        if (Severity > 0.7)
        {
            Output.WriteLine("You are shivering violently.");
        }
        else if (Severity > 0.3)
        {
            Output.WriteLine("You are shivering.");
        }
        else
        {
            Output.WriteLine("You are beginning to shiver.");
        }
    }

    protected override void OnRemove(Actor target)
    {
        Output.WriteLine("You stop shivering.");
    }

    protected override void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity)
    {
        // Update metabolism boost when severity changes
        _metabolismBoost = updatedSeverity * 0.2;

        // Update capacity modifiers when severity changes
        CapacityModifiers["Manipulation"] = -0.2 * updatedSeverity;

        // Only notify player of significant changes
        if (Math.Abs(oldSeverity - updatedSeverity) > 0.3)
        {
            if (updatedSeverity > oldSeverity)
            {
                if (updatedSeverity > 0.7)
                {
                    Output.WriteLine("You are now shivering violently.");
                }
                else
                {
                    Output.WriteLine("You are shivering more intensely.");
                }
            }
            else if (updatedSeverity < 0.3)
            {
                Output.WriteLine("Your shivering is subsiding.");
            }
        }
    }

    // Can be accessed by metabolism system to apply heat generation boost
    public double GetMetabolismBoost()
    {
        return _metabolismBoost;
    }

    public override string Describe()
    {
        string intensityDesc;
        if (Severity > 0.7)
            intensityDesc = "Violent";
        else if (Severity > 0.3)
            intensityDesc = "Moderate";
        else
            intensityDesc = "Mild";

        return $"{intensityDesc} shivering";
    }
}