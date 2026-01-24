using text_survival.Actions;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;
using text_survival.Desktop;
using DesktopIO = text_survival.Desktop.DesktopIO;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for harvesting specific resources (berry bushes, dead trees, etc).
/// Requires HarvestableFeature with available resources.
/// No specific capacity impairments (general work).
/// </summary>
public class HarvestStrategy : IWorkStrategy
{
    private HarvestableFeature? _selectedTarget;

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        // Use feature's CanBeHarvested method
        var harvestables = location
            .Features.OfType<HarvestableFeature>()
            .Where(h => h.CanBeHarvested())
            .ToList();

        if (harvestables.Count == 0)
            return "There's nothing to harvest here.";

        // If multiple harvestables, prompt for selection
        if (harvestables.Count == 1)
        {
            _selectedTarget = harvestables[0];
        }
        else
        {
            GameDisplay.Render(ctx, statusText: "Planning.");
            var harvestChoice = new Choice<HarvestableFeature>("What do you want to harvest?");
            foreach (var h in harvestables)
            {
                harvestChoice.AddOption($"{h.DisplayName}", h);
            }
            _selectedTarget = harvestChoice.GetPlayerChoice(ctx);
        }

        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        if (_selectedTarget == null)
            return null;

        // Build resource preview
        string resourcePreview = BuildResourcePreview(_selectedTarget);

        // Create enhanced prompt with preview
        string prompt = $"Harvest: {_selectedTarget.DisplayName}\n{resourcePreview}\n\nHow long should you work?";

        var choice = new Choice<int>(prompt);
        choice.AddOption("Quick work - 15 min", 15);
        choice.AddOption("Standard work - 30 min", 30);
        choice.AddOption("Thorough work - 60 min", 60);
        choice.AddOption("Cancel", 0);
        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        // Harvesting doesn't have specific impairments in original implementation
        return (baseTime, []);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => "harvesting";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        if (_selectedTarget == null)
            return WorkResult.Empty(0);

        var found = _selectedTarget.Harvest(actualTime);

        var collected = new List<string>();
        string resultMessage;

        if (found.IsEmpty)
        {
            resultMessage = "You didn't find anything usable.";
        }
        else
        {
            collected.Add(found.GetDescription());
            InventoryCapacityHelper.CombineAndReport(ctx, found);
            resultMessage = $"{_selectedTarget.DisplayName}: {_selectedTarget.GetStatusDescription()}";
        }

        // Show results in popup overlay
        DesktopIO.ShowWorkResult(ctx, "Harvesting", resultMessage, collected);

        return new WorkResult(collected, null, actualTime, false);
    }

    /// <summary>
    /// Build resource preview showing what's available to harvest.
    /// Format: "resource: status (quantity/max, ~weight)"
    /// </summary>
    private static string BuildResourcePreview(HarvestableFeature target)
    {
        var lines = new List<string>();

        // Show each non-depleted resource
        foreach (var resource in target.Resources)
        {
            if (resource.CurrentQuantity == 0)
                continue;  // Skip depleted

            // Calculate status (reuses existing logic from GetStatusDescription)
            string status = resource.CurrentQuantity switch
            {
                var q when q < resource.MaxQuantity / 3.0 => "sparse",
                var q when q < resource.MaxQuantity * 2.0 / 3.0 => "moderate",
                _ => "abundant"
            };

            string quantityInfo = $"{resource.CurrentQuantity}/{resource.MaxQuantity}";
            double totalWeightKg = resource.CurrentQuantity * resource.WeightPerUnit;

            // Format differently for water vs regular resources
            string line = resource.IsWater
                ? $"{resource.DisplayName}: {status} ({quantityInfo} units, ~{totalWeightKg:F1}L)"
                : $"{resource.DisplayName}: {status} ({quantityInfo} units, ~{totalWeightKg:F1}kg)";

            lines.Add(line);
        }

        if (lines.Count == 0)
            return "No resources available";

        // Add tool requirement hint if present
        string toolHint = "";
        if (target.RequiredToolType != null)
        {
            toolHint = $"\n{target.GetToolRequirementDescription()}";
        }

        // Show harvest rate
        string rateHint = target.MinutesToHarvest == 1
            ? "\nHarvest rate: 1 unit per minute"
            : $"\nHarvest rate: 1 unit per {target.MinutesToHarvest} minutes";

        return string.Join("\n", lines) + toolHint + rateHint;
    }
}
