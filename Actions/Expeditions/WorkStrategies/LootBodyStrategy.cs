using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for taking belongings from a deceased NPC's body.
/// Quick action (5 minutes).
/// </summary>
public class LootBodyStrategy(NPCBodyFeature body) : IWorkStrategy
{
    private readonly NPCBodyFeature _body = body;

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        if (_body.Belongings.CurrentWeightKg == 0 && _body.Belongings.Tools.Count == 0)
            return $"{_body.NPCName} has nothing to take.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var choice = new Choice<int>($"Take {_body.NPCName}'s belongings?");
        choice.AddOption("Yes (5 minutes)", 5);
        choice.AddOption("Leave them", 0);
        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Light work - just requires manipulation
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: false,
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Resting;

    public string GetActivityName() => "searching";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        // Get description of what's being taken before transfer
        var description = _body.Belongings.GetDescription();

        // Transfer items, respecting player capacity
        var leftovers = ctx.Inventory.CombineWithCapacity(_body.Belongings);

        // Update body's belongings to only contain what didn't fit
        _body.Belongings = leftovers;

        if (leftovers.IsEmpty)
        {
            GameDisplay.AddNarrative(ctx, $"You take {_body.NPCName}'s belongings: {description}.");
        }
        else
        {
            GameDisplay.AddNarrative(ctx, $"You take what you can carry from {_body.NPCName}'s belongings. Some items remain.");
        }

        return WorkResult.Empty(actualTime);
    }
}
