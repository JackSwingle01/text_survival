using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles torch lighting and extinguishing actions.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class TorchHandler
{

    /// <summary>
    /// Light a torch using firestarter and tinder (similar to fire starting but simpler).
    /// </summary>
    private static void LightTorchWithFirestarter(GameContext ctx)
    {
        var inv = ctx.Inventory;

        // Get fire-making tools
        var fireTools = inv.Tools.Where(t =>
            t.ToolType is ToolType.FireStriker or ToolType.HandDrill or ToolType.BowDrill).ToList();

        if (fireTools.Count == 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have a fire-making tool!");
            return;
        }

        // Build tool options
        var toolChoices = new List<string>();
        var toolMap = new Dictionary<string, (Gear tool, double chance)>();

        foreach (var tool in fireTools)
        {
            double baseChance = FireHandler.GetToolBaseChance(tool);
            var skill = ctx.player.Skills.GetSkill("Firecraft");
            double successChance = baseChance + (skill.Level * 0.1);
            successChance = Math.Clamp(successChance, 0.05, 0.95);

            string label = $"{tool.Name} - {successChance:P0} success";
            toolChoices.Add(label);
            toolMap[label] = (tool, successChance);
        }
        toolChoices.Add("Cancel");

        GameDisplay.Render(ctx, statusText: "Preparing.");
        string choice = Input.Select(ctx, "Light torch with:", toolChoices);

        if (choice == "Cancel")
        {
            GameDisplay.AddNarrative(ctx, "You decide not to light the torch.");
            return;
        }

        var (selectedTool, _) = toolMap[choice];

        // Consume tinder
        inv.Pop(Resource.Tinder);

        double finalChance = FireHandler.GetToolBaseChance(selectedTool);
        var playerSkill = ctx.player.Skills.GetSkill("Firecraft");
        finalChance += playerSkill.Level * 0.1;
        finalChance = Math.Clamp(finalChance, 0.05, 0.95);

        GameDisplay.AddNarrative(ctx, $"You work with the {selectedTool.Name}...");
        ctx.Update(2, ActivityType.TendingFire);

        if (Utils.DetermineSuccess(finalChance))
        {
            inv.LightTorch();
            GameDisplay.AddSuccess(ctx, $"Success! The torch catches fire. ({finalChance:P0} chance)");
            playerSkill.GainExperience(1);
        }
        else
        {
            GameDisplay.AddWarning(ctx, $"The tinder fizzles out. The torch didn't light. ({finalChance:P0} chance)");
            playerSkill.GainExperience(1);

            // Offer retry if materials available
            if (inv.Has(ResourceCategory.Tinder))
            {
                GameDisplay.Render(ctx, statusText: "Thinking.");
                if (Input.Confirm(ctx, "Try again?"))
                    LightTorchWithFirestarter(ctx);
            }
        }
    }

    /// <summary>
    /// Handle torch burn time and chaining logic during time passage.
    /// Called each minute from GameContext.UpdateInternal.
    /// </summary>
    public static void UpdateTorchBurnTime(GameContext ctx, int minutes, HeatSourceFeature? fire)
    {
        if (ctx.Inventory.ActiveTorch == null) return;

        double previousTime = ctx.Inventory.TorchBurnTimeRemainingMinutes;
        ctx.Inventory.TorchBurnTimeRemainingMinutes -= minutes;

        // Torch chaining prompt at 5 minutes (only if not near fire and have another torch)
        if (previousTime > 5 && ctx.Inventory.TorchBurnTimeRemainingMinutes <= 5 &&
            ctx.Inventory.TorchBurnTimeRemainingMinutes > 0 && ctx.Inventory.HasUnlitTorch)
        {
            if (!ctx.CurrentLocation.HasActiveHeatSource())
            {
                int torchCount = ctx.Inventory.Tools.Count(t => t.ToolType == ToolType.Torch && t.Works);
                var chainChoice = new Choice<bool>("Your torch is burning low. Light another?");
                chainChoice.AddOption($"Yes, light new torch ({torchCount} remaining)", true);
                chainChoice.AddOption("No, let it burn out", false);

                GameDisplay.Render(ctx, statusText: "Torch dying.");
                if (chainChoice.GetPlayerChoice(ctx))
                {
                    ctx.Inventory.LightTorch();
                    GameDisplay.AddNarrative(ctx, "You light a fresh torch from the dying flame.");
                }
            }
        }

        // Torch burns out
        if (ctx.Inventory.TorchBurnTimeRemainingMinutes <= 0)
        {
            GameDisplay.AddWarning(ctx, "Your torch sputters and dies.");
            ctx.Inventory.ActiveTorch = null;
            ctx.Inventory.TorchBurnTimeRemainingMinutes = 0;
        }
    }
}
