using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for working on multi-session construction projects.
/// Allows player to choose work duration (30min to 2hr) per session.
/// </summary>
public class CraftingProjectStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var project = location.GetFeature<CraftingProjectFeature>();
        if (project == null)
            return "There's no construction project here.";
        if (project.IsComplete)
            return "This project is already complete.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var project = location.GetFeature<CraftingProjectFeature>()!;
        double remainingMinutes = project.TimeRequiredMinutes - project.TimeInvestedMinutes;

        var choice = new Choice<int>($"How long do you want to work on {project.ProjectName}? ({project.ProgressPct:P0} complete)");

        // Offer standard time options only if there's at least that much time remaining
        if (remainingMinutes >= 15)
            choice.AddOption("15 minutes", 15);
        if (remainingMinutes >= 30)
            choice.AddOption("30 minutes", 30);
        if (remainingMinutes >= 60)
            choice.AddOption("1 hour", 60);
        if (remainingMinutes >= 120)
            choice.AddOption("2 hours", 120);

        // If remaining time doesn't match a standard option, show "Finish" option
        int remaining = (int)remainingMinutes;
        if (remaining > 0 && remaining != 15 && remaining != 30 && remaining != 60 && remaining != 120)
        {
            choice.AddOption($"Finish ({remaining} min)", remaining);
        }

        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Crafting;

    public string GetActivityName() => "constructing";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var project = location.GetFeature<CraftingProjectFeature>()!;

        // Check for shovel bonus - double progress for digging projects
        bool hasShovel = ctx.Inventory.GetTool(ToolType.Shovel) != null;
        double progressMultiplier = 1.0;

        if (project.BenefitsFromShovel && hasShovel)
        {
            progressMultiplier = 2.0;
            GameDisplay.AddNarrative(ctx, $"Your shovel speeds up the digging work on the {project.ProjectName}...");
        }
        else
        {
            GameDisplay.AddNarrative(ctx, $"You work on the {project.ProjectName}...");
        }

        // Add progress (with shovel bonus if applicable)
        double effectiveProgress = actualTime * progressMultiplier;
        project.AddProgress(effectiveProgress, location);

        var collected = new List<string>();
        string resultMessage;

        if (project.IsComplete)
        {
            collected.Add($"Completed: {project.ProjectName}");
            resultMessage = $"You complete the {project.ProjectName}!";
        }
        else
        {
            collected.Add($"{project.ProjectName}: {project.ProgressPct:P0}");
            resultMessage = $"Progress: {project.ProgressPct:P0} complete.";
        }

        // Show results in popup overlay
        WebIO.ShowWorkResult(ctx, "Construction", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }
}
