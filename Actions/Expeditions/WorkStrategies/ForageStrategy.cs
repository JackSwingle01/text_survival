using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for foraging ambient resources from a location.
/// Requires ForageFeature. Impaired by moving and breathing capacity.
/// Yields reduced by perception impairment.
/// </summary>
public class ForageStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        var feature = location.GetFeature<ForageFeature>();
        if (feature == null)
            return "There's nothing to forage here.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var choice = new Choice<int>("How long should you forage?");
        choice.AddOption("Quick gather - 15 min", 15);
        choice.AddOption("Standard search - 30 min", 30);
        choice.AddOption("Thorough search - 60 min", 60);
        choice.AddOption("Cancel", 0);
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
            checkBreathing: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Foraging;

    public string GetActivityName() => "foraging";

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        var feature = location.GetFeature<ForageFeature>()!;

        GameDisplay.AddNarrative(ctx, "You search the area for resources...");

        var found = feature.Forage(actualTime / 60.0);

        // Perception impairment reduces yield (-15%)
        var perception = AbilityCalculator.CalculatePerception(
            ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers());
        if (AbilityCalculator.IsPerceptionImpaired(perception))
        {
            found.ApplyMultiplier(0.85);
            GameDisplay.AddWarning(ctx, "Your foggy senses cause you to miss some resources.");
        }

        // Tool bonuses - help gather more efficiently (+10% each)
        bool hasAxeBonus = false;
        bool hasShovelBonus = false;

        var axe = ctx.Inventory.GetTool(ToolType.Axe);
        if (axe != null && axe.Works)
        {
            found.ApplyMultiplier(1.10);
            hasAxeBonus = true;
        }

        var shovel = ctx.Inventory.GetTool(ToolType.Shovel);
        if (shovel != null && shovel.Works)
        {
            found.ApplyMultiplier(1.10);
            hasShovelBonus = true;
        }

        // Tutorial: Tool bonus explanation (once per tool type)
        if (hasAxeBonus)
            ctx.ShowTutorialOnce("Your axe helps break branches and strip bark. (+10% yield)");
        if (hasShovelBonus)
            ctx.ShowTutorialOnce("Your shovel helps dig up roots and turn soil. (+10% yield)");

        var collected = new List<string>();
        string quality = feature.GetQualityDescription();

        if (found.IsEmpty)
        {
            GameDisplay.AddNarrative(ctx, "You find nothing.");
            GameDisplay.AddNarrative(ctx, WorkRunner.GetForageFailureMessage(quality));
        }
        else
        {
            GameDisplay.AddNarrative(ctx, $"You found: {found.GetDescription()}");
            collected.Add(found.GetDescription());
            InventoryCapacityHelper.CombineAndReport(ctx, found);
            if (quality == "sparse" || quality == "picked over")
                GameDisplay.AddNarrative(ctx, "Resources here are getting scarce.");
        }

        // Tutorial: Show fuel progress on Day 1
        double totalFuelGathered = found.GetWeight(ResourceCategory.Fuel);
        if (ctx.DaysSurvived == 0 && totalFuelGathered > 0)
        {
            double currentFuel = ctx.Inventory.GetWeight(ResourceCategory.Fuel);
            if (currentFuel < 8.0)
            {
                double needed = 8.0 - currentFuel;
                GameDisplay.AddNarrative(ctx, $"You'll want about {needed:F0}kg more for tonight.");
            }
        }

        return new WorkResult(collected, null, actualTime, false);
    }
}
