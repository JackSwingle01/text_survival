using text_survival.Bodies;
using text_survival.Crafting;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions;

/// <summary>
/// Handles camp infrastructure improvements.
/// Separate menu from regular crafting focused on building fire pits, shelters, and bedding.
/// </summary>
public class ImproveCampRunner(GameContext ctx)
{
    private readonly GameContext _ctx = ctx;
    private readonly NeedCraftingSystem _crafting = new();

    /// <summary>
    /// Run the camp improvements menu. Shows available and locked recipes.
    /// </summary>
    public void Run()
    {
        // Render camp improvement screen
        Web.WebIO.RenderCampImprovementScreen(_ctx, _crafting);

        // Get all camp infrastructure options
        var allOptions = _crafting.GetOptionsForNeed(NeedCategory.CampInfrastructure, _ctx.Inventory, showAll: true);

        // Split into available and unavailable based on prerequisites and materials
        var available = new List<CraftOption>();
        var locked = new List<(CraftOption option, string reason)>();

        foreach (var option in allOptions)
        {
            // Check prerequisite first
            string? prereqError = option.Prerequisite?.Invoke(_ctx);
            if (prereqError != null)
            {
                locked.Add((option, prereqError));
                continue;
            }

            // Check materials
            if (option.CanCraft(_ctx.Inventory))
            {
                available.Add(option);
            }
            else
            {
                var (_, missing) = option.CheckRequirements(_ctx.Inventory);
                if (missing.Count < option.Requirements.Count)
                {
                    // Has some materials - show as locked with missing list
                    locked.Add((option, $"Missing: {string.Join(", ", missing)}"));
                }
            }
        }

        if (available.Count == 0 && locked.Count == 0)
        {
            GameDisplay.AddNarrative(_ctx, "You don't have materials to improve your camp.");
            GameDisplay.Render(_ctx, statusText: "Thinking.");
            Web.WebIO.ClearCrafting(_ctx);
            return;
        }

        // Build choice menu
        var choice = new Choice<CraftOption?>("Select a camp improvement:");

        // Add available options first
        foreach (var option in available)
        {
            // Check if this is a multi-session project to show total time
            string timeInfo;
            if (option.ProducesFeature && option.Name.Contains("(Project)"))
            {
                // This is a project - show total build time
                var tempFeature = option.FeatureFactory!();
                if (tempFeature is CraftingProjectFeature project)
                {
                    int totalHours = (int)(project.TimeRequiredMinutes / 60);
                    int remainingMinutes = (int)(project.TimeRequiredMinutes % 60);
                    if (remainingMinutes > 0)
                        timeInfo = $"{totalHours}h {remainingMinutes}m total";
                    else
                        timeInfo = $"{totalHours}h total";
                }
                else
                {
                    timeInfo = $"{option.CraftingTimeMinutes} min";
                }
            }
            else
            {
                // Instant improvement
                timeInfo = $"{option.CraftingTimeMinutes} min";
            }

            string label = $"{option.Name} - {option.GetRequirementsShort()} - {timeInfo}";
            choice.AddOption(label, option);
        }

        // Add locked options (grayed out)
        foreach (var (option, reason) in locked)
        {
            string label = $"{option.Name} ({reason})";
            // Don't add to choice - just show in UI
        }

        choice.AddOption("Cancel", null);

        GameDisplay.Render(_ctx, statusText: "Planning.");
        var selected = choice.GetPlayerChoice(_ctx);

        Web.WebIO.ClearCrafting(_ctx);

        if (selected == null)
            return;

        DoCraft(selected);
    }

    private bool DoCraft(CraftOption option)
    {
        GameDisplay.AddNarrative(_ctx, $"You begin working on {option.Name}...");

        var capacities = _ctx.player.GetCapacities();
        int totalTime = option.CraftingTimeMinutes;

        // Consciousness impairment slows crafting (+25%)
        if (AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
        {
            totalTime = (int)(totalTime * 1.25);
            GameDisplay.AddWarning(_ctx, "Your foggy mind slows the work.");
        }

        // Manipulation impairment slows crafting (+30%)
        if (AbilityCalculator.IsManipulationImpaired(capacities.Manipulation))
        {
            totalTime = (int)(totalTime * 1.30);
            GameDisplay.AddWarning(_ctx, "Your unsteady hands slow the work.");
        }

        // Use centralized progress method - handles web animation and processes all time at once
        var (elapsed, interrupted) = GameDisplay.UpdateAndRenderProgress(_ctx, "Building.", totalTime, ActivityType.Crafting);

        if (!_ctx.player.IsAlive)
            return false;

        if (interrupted)
        {
            GameDisplay.AddWarning(_ctx, "Your work was interrupted.");
            return false;
        }

        // All camp infrastructure options produce features
        var feature = option.CraftFeature(_ctx.Inventory);
        if (feature == null)
            return false;

        // Special handling for bedding - replace existing
        if (feature is BeddingFeature)
        {
            var oldBedding = _ctx.Camp.GetFeature<BeddingFeature>();
            if (oldBedding != null)
            {
                _ctx.Camp.RemoveFeature(oldBedding);
                GameDisplay.AddNarrative(_ctx, "You replace your old bedding.");
            }
        }

        // Add the feature to camp
        _ctx.Camp.AddFeature(feature);

        // Check if this is a multi-session project or instant improvement
        if (feature is CraftingProjectFeature project)
        {
            GameDisplay.AddSuccess(_ctx, $"Started construction project: {project.ProjectName}");
            GameDisplay.AddNarrative(_ctx, $"Materials consumed. Work on it from the camp menu to make progress.");
            GameDisplay.AddNarrative(_ctx, $"Total work required: {project.TimeRequiredMinutes} minutes ({project.TimeRequiredMinutes / 60:F1} hours).");

            if (project.BenefitsFromShovel)
            {
                bool hasShovel = _ctx.Inventory.GetTool(ToolType.Shovel) != null;
                if (hasShovel)
                {
                    GameDisplay.AddNarrative(_ctx, "Your shovel will double progress on this digging work.");
                }
                else
                {
                    GameDisplay.AddNarrative(_ctx, "A shovel would double your progress on this digging work.");
                }
            }
        }
        else
        {
            // Instant improvement
            GameDisplay.AddSuccess(_ctx, $"You built {option.Name}!");
            GameDisplay.AddNarrative(_ctx, "It's now part of your camp.");
        }

        GameDisplay.Render(_ctx, statusText: "Satisfied.");

        return true;
    }
}
