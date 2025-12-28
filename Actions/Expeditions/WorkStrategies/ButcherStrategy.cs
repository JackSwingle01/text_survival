using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for butchering a carcass. Thin orchestration layer -
/// all butchering logic lives in CarcassFeature.
/// </summary>
public class ButcherStrategy : IWorkStrategy
{
    private readonly CarcassFeature _carcass;

    public ButcherStrategy(CarcassFeature carcass)
    {
        _carcass = carcass;
    }

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        if (_carcass.IsCompletelyButchered)
            return "There's nothing left to butcher.";

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        int remainingMinutes = _carcass.GetRemainingMinutes();
        string decayDesc = _carcass.GetDecayDescription();

        var choice = new Choice<int>($"How long do you want to butcher the {_carcass.AnimalName} carcass? ({decayDesc})");

        // Offer time options based on remaining work
        if (remainingMinutes >= 15)
            choice.AddOption("15 minutes", 15);
        if (remainingMinutes >= 30)
            choice.AddOption("30 minutes", 30);
        if (remainingMinutes >= 60)
            choice.AddOption("1 hour", 60);

        // Option to finish completely
        if (remainingMinutes > 0 && remainingMinutes < 60)
            choice.AddOption($"Finish completely (~{remainingMinutes} min)", remainingMinutes);
        else if (remainingMinutes >= 60)
            choice.AddOption($"Finish completely (~{remainingMinutes} min)", remainingMinutes);

        choice.AddOption("Cancel", 0);

        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Butchering requires manipulation (steady hands for cuts)
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: false,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Butchering;

    public string GetActivityName() => "butchering";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        // Determine tool and impairment status
        bool hasCuttingTool = ctx.Inventory.HasCuttingTool;
        var manipulation = ctx.player.GetCapacities().Manipulation;
        bool manipulationImpaired = AbilityCalculator.IsManipulationImpaired(manipulation);

        // Warn if no cutting tool
        if (!hasCuttingTool)
        {
            GameDisplay.AddWarning(ctx, "Without a cutting tool, you tear what you can by hand...");
        }

        // Warn if manipulation impaired (affects yield, not time here)
        if (manipulationImpaired)
        {
            GameDisplay.AddWarning(ctx, "Your unsteady hands waste some of the meat.");
        }

        // Harvest from carcass
        var yield = _carcass.Harvest(actualTime, hasCuttingTool, manipulationImpaired);

        // Add to inventory
        var collected = new List<string>();
        if (!yield.IsEmpty)
        {
            collected.Add(yield.GetDescription());
            InventoryCapacityHelper.CombineAndReport(ctx, yield);
        }

        // Build result message
        string resultMessage;
        if (_carcass.IsCompletelyButchered)
        {
            resultMessage = $"You've finished butchering the {_carcass.AnimalName}.";

            // Remove carcass feature from location
            location.RemoveFeature(_carcass);
        }
        else
        {
            double progressPct = 1.0 - (_carcass.GetTotalRemainingKg() /
                (_carcass.BodyWeightKg * 0.78));  // Total yield is ~78% of body weight
            progressPct = Math.Clamp(progressPct, 0, 1);

            resultMessage = $"Butchering progress: {progressPct:P0}. ~{_carcass.GetRemainingMinutes()} min remaining.";
        }

        // Decay warning
        if (_carcass.DecayLevel > 0.5 && !_carcass.IsCompletelyButchered)
        {
            resultMessage += " The meat is starting to spoil.";
        }

        // Show results in popup overlay
        WebIO.ShowWorkResult(ctx, "Butchering", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
