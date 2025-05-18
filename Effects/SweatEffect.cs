
using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;

namespace text_survival.Effects;

public class SweatingEffect : Effect
{
    private double _waterLossRate; // Liters per hour

    public SweatingEffect(BodyPart bodyPart, double severity)
        : base("Sweating", "Heat exposure", bodyPart, severity, -0.1) // Naturally decreases when not refreshed
    {
        // Water loss rate based on severity
        _waterLossRate = 0.05 * severity; // Liters per hour
        IsStackable = false; // Use EffectRegistry's stacking mechanism
    }

    protected override void OnUpdate(Actor target)
    {
        // Let base class handle natural severity reduction
        base.OnUpdate(target);

        // Any other sweating-specific update logic could go here
    }

    protected override void OnApply(Actor target)
    {
        if (Severity > 0.7)
        {
            Output.WriteLine("You are sweating profusely.");
        }
        else if (Severity > 0.3)
        {
            Output.WriteLine("You are sweating.");
        }
        else
        {
            Output.WriteLine("You are beginning to sweat.");
        }
    }

    protected override void OnRemove(Actor target)
    {
        Output.WriteLine("You stop sweating.");
    }

    protected override void OnSeverityChange(Actor target, double oldSeverity, double updatedSeverity)
    {
        // Update water loss rate when severity changes
        _waterLossRate = 0.05 * updatedSeverity;

        // Only notify player of significant changes
        if (Math.Abs(oldSeverity - updatedSeverity) > 0.3)
        {
            if (updatedSeverity > oldSeverity)
            {
                if (updatedSeverity > 0.7)
                {
                    Output.WriteLine("You are now sweating profusely.");
                }
                else
                {
                    Output.WriteLine("You are sweating more.");
                }
            }
            else if (updatedSeverity < 0.3)
            {
                Output.WriteLine("You are sweating less.");
            }
        }
    }

    // Can be accessed by thirst system to apply water loss
    public double GetWaterLossForPeriod(TimeSpan period)
    {
        return _waterLossRate * period.TotalHours;
    }

    public override string Describe()
    {
        string intensityDesc;
        if (Severity > 0.7)
            intensityDesc = "Profuse";
        else if (Severity > 0.3)
            intensityDesc = "Moderate";
        else
            intensityDesc = "Mild";

        return $"{intensityDesc} sweating";
    }
}
