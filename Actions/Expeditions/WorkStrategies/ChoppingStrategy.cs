using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for chopping wood from a wooded area.
/// Requires an axe. Work accumulates across sessions until a tree is felled,
/// then yields a large amount of wood and resets for the next tree.
/// </summary>
public class ChoppingStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var feature = location.GetFeature<WoodedAreaFeature>();
        if (feature == null)
            return "There are no trees to fell here.";
        if (!feature.HasTrees)
            return "All the trees here have been felled.";

        // Check for axe
        var axe = ctx.Inventory.GetTool(ToolType.Axe);
        if (axe == null)
            return "You need an axe to fell trees.";
        if (axe.IsBroken)
            return "Your axe is broken.";

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var feature = location.GetFeature<WoodedAreaFeature>()!;
        double remainingMinutes = feature.MinutesToFell - feature.MinutesWorked;

        string progressText = feature.MinutesWorked > 0
            ? $" ({feature.ProgressPct:P0} complete)"
            : "";

        var choice = new Choice<int>($"How long do you want to chop?{progressText}");

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
            choice.AddOption($"Finish the tree ({(int)remainingMinutes} min)", (int)remainingMinutes);

        choice.AddOption("Cancel", 0);
        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Chopping requires both mobility and arm strength
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Chopping;

    public string GetActivityName() => "chopping wood";

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var feature = location.GetFeature<WoodedAreaFeature>()!;
        var axe = ctx.Inventory.GetTool(ToolType.Axe)!;
        var collected = new List<string>();

        // Use the axe (consume durability once per session)
        bool axeStillWorks = axe.Use();

        // Add work progress
        feature.AddProgress(actualTime);

        // Check if tree is ready to fell
        if (feature.IsTreeReady)
        {
            // Fell the tree and get the yield
            var yield = feature.FellTree();

            GameDisplay.AddNarrative(ctx, "The tree creaks, groans, and crashes to the ground!");
            GameDisplay.AddNarrative(ctx, $"You collected: {yield.GetDescription()}");
            collected.Add(yield.GetDescription());

            InventoryCapacityHelper.CombineAndReport(ctx, yield);

            // Report remaining trees if limited
            if (feature.TreesAvailable != null)
            {
                if (feature.HasTrees)
                    GameDisplay.AddNarrative(ctx, $"{feature.TreesAvailable} trees remain in this area.");
                else
                    GameDisplay.AddNarrative(ctx, "This area has been cleared of trees.");
            }
        }
        else
        {
            // Report progress
            string[] progressMessages = [
                "Chips fly as you swing the axe.",
                "The tree shudders with each blow.",
                "You've cut deep into the trunk.",
                "The tree groans and sways.",
                "Almost there. The trunk is nearly through."
            ];

            // Select message based on progress
            int messageIndex = Math.Min((int)(feature.ProgressPct * progressMessages.Length), progressMessages.Length - 1);
            GameDisplay.AddNarrative(ctx, progressMessages[messageIndex]);
            GameDisplay.AddNarrative(ctx, $"Progress: {feature.ProgressPct:P0} complete.");
            collected.Add($"Tree: {feature.ProgressPct:P0}");
        }

        // Report axe status
        if (!axeStillWorks)
        {
            GameDisplay.AddWarning(ctx, $"Your {axe.Name} has broken!");
        }
        else if (axe.Durability > 0 && axe.Durability <= 3)
        {
            GameDisplay.AddWarning(ctx, $"Your {axe.Name} is wearing out ({axe.Durability} uses left).");
        }

        return new WorkResult(collected, null, actualTime, false);
    }
}
