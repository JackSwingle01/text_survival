
using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Effects;

public class SweatingEffect : Effect
{
    private double _waterLossRate; // Liters per hour

    public SweatingEffect(double severity)
        : base("Sweating", "Heat exposure", null, severity, -2) // Naturally decreases when not refreshed
    {
        // Water loss rate based on severity
        _waterLossRate = 0.05 * severity; // Liters per hour
        IsStackable = false; // Use EffectRegistry's stacking mechanism
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

    protected override void OnUpdate(Actor target)
    {
        if (target is Player player)
        {
            double waterLoss = _waterLossRate / 1000 / 60; // convert to ml per minute
            var stats = new SurvivalStatsUpdate();
            stats.Hydration = -waterLoss;
            player.Body.UpdateSurvivalStats(stats);
            Output.WriteLine($"You lose {waterLoss:F2} ml of water due to sweating.");
        }
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
