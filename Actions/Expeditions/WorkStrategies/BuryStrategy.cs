using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for burying a deceased NPC's body.
/// Takes 60 minutes with a shovel, 90 minutes without.
/// </summary>
public class BuryStrategy(NPCBodyFeature body) : IWorkStrategy
{
    private readonly NPCBodyFeature _body = body;

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        if (_body.IsBuried)
            return $"{_body.NPCName} has already been buried.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var shovel = ctx.Inventory.GetTool(ToolType.Shovel);
        bool hasShovel = shovel != null && !shovel.IsBroken;

        int baseTime = hasShovel ? 60 : 90;
        string timeDesc = hasShovel ? "1 hour" : "1.5 hours (no shovel)";

        var choice = new Choice<int>($"Bury {_body.NPCName}?");
        choice.AddOption($"Yes ({timeDesc})", baseTime);
        choice.AddOption("Not now", 0);
        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Burial is physical work - requires mobility and arm strength
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

    public string GetActivityName() => "burying";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        // Mark as buried
        _body.IsBuried = true;

        // Narrative
        GameDisplay.AddNarrative(ctx, $"You bury {_body.NPCName}. You mark the grave with stones. It isn't much. It's something.");

        return WorkResult.Empty(actualTime);
    }
}
