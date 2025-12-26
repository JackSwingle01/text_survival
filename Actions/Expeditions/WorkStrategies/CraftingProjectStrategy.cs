using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;

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

        // Offer time options up to remaining time or 2 hours, whichever is less
        if (remainingMinutes >= 30)
            choice.AddOption("30 minutes", Math.Min(30, (int)remainingMinutes));
        if (remainingMinutes >= 60)
            choice.AddOption("1 hour", Math.Min(60, (int)remainingMinutes));
        if (remainingMinutes >= 90)
            choice.AddOption("1.5 hours", Math.Min(90, (int)remainingMinutes));
        if (remainingMinutes >= 120)
            choice.AddOption("2 hours", Math.Min(120, (int)remainingMinutes));

        // If less than 30 minutes remain, offer to finish
        if (remainingMinutes < 30 && remainingMinutes > 0)
            choice.AddOption($"Finish ({(int)remainingMinutes} min)", (int)remainingMinutes);

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

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var project = location.GetFeature<CraftingProjectFeature>()!;

        GameDisplay.AddNarrative(ctx, $"You work on the {project.ProjectName}...");

        // Add progress
        project.AddProgress(actualTime, location);

        var collected = new List<string>();

        if (project.IsComplete)
        {
            GameDisplay.AddNarrative(ctx, $"You complete the {project.ProjectName}!");
            collected.Add($"Completed: {project.ProjectName}");
        }
        else
        {
            GameDisplay.AddNarrative(ctx, $"Progress: {project.ProgressPct:P0} complete.");
            collected.Add($"{project.ProjectName}: {project.ProgressPct:P0}");
        }

        return new WorkResult(collected, null, actualTime, false);
    }
}
