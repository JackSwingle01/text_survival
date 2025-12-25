using text_survival.Bodies;
using text_survival.Environments;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for exploring to discover new locations.
/// Requires unrevealed locations in the zone.
/// Impaired by breathing capacity (scouting takes physical exertion).
/// </summary>
public class ExploreStrategy : IWorkStrategy
{
    private int _selectedTime;

    public string? ValidateLocation(GameContext ctx, Location location)
    {
        if (!ctx.HasUnrevealedLocations())
            return "You've explored everything reachable from here.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        double successChance = WorkRunner.CalculateExploreChance(location);

        var timeChoice = new Choice<int>(
            $"How thoroughly should you scout? ({successChance:P0} chance to find something)"
        );
        timeChoice.AddOption("Quick scout - 15 min", 15);
        timeChoice.AddOption("Standard scout - 30 min (+10%)", 30);
        timeChoice.AddOption("Thorough scout - 60 min (+20%)", 60);
        timeChoice.AddOption("Cancel", 0);
        return timeChoice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        _selectedTime = baseTime; // Store for Execute method

        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkBreathing: true,
            effectRegistry: ctx.player.EffectRegistry
        );

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Exploring;

    public string GetActivityName() => "exploring";

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        GameDisplay.AddNarrative(ctx, "You scout the area, looking for new paths...");

        // Calculate success chance based on originally selected time
        double baseChance = WorkRunner.CalculateExploreChance(location);
        double timeBonus = _selectedTime switch
        {
            30 => 0.10,
            60 => 0.20,
            _ => 0.0,
        };
        double finalChance = Math.Min(0.95, baseChance + timeBonus);

        Location? discovered = null;

        if (Utils.RandDouble(0, 1) <= finalChance)
        {
            var newLocation = ctx.RevealRandomLocation(location);

            if (newLocation != null)
            {
                GameDisplay.AddSuccess(ctx, $"You discovered a new area: {newLocation.Name}!");
                if (!string.IsNullOrEmpty(newLocation.Tags))
                    GameDisplay.AddNarrative(ctx, newLocation.Tags);
                discovered = newLocation;
            }
            else
            {
                GameDisplay.AddNarrative(ctx, "You scouted the area but found no new paths.");
            }
        }
        else
        {
            GameDisplay.AddNarrative(ctx, "You searched the area but couldn't find any new paths.");
        }

        return new WorkResult([], discovered, actualTime, false);
    }
}
