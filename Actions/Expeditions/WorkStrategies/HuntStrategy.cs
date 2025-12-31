using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.UI;
using text_survival.Web;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for the search phase of hunting.
/// Can hunt on tiles with herds present (checked via HerdRegistry at current position).
/// AnimalTerritoryFeature provides additional small game spawning (rabbit, fox, ptarmigan).
/// If animal found, returns WorkResult.FoundAnimal for caller to handle interactive hunt.
/// </summary>
public class HuntStrategy : IWorkStrategy
{
    public string? ValidateLocation(GameContext ctx, Location location)
    {
        // Check if there are persistent herds on this tile
        if (ctx.Map != null)
        {
            var pos = ctx.Map.GetPosition(location);
            if (pos.HasValue)
            {
                var herdsHere = ctx.Herds.GetHerdsAt(pos.Value);
                if (herdsHere.Any(h => h.Count > 0))
                {
                    return null; // Game available (prey or predator)
                }
            }
        }

        // Fall back to territory check for small game
        var territory = location.GetFeature<AnimalTerritoryFeature>();
        if (territory == null)
            return "There's no game to be found here.";
        if (!territory.CanHunt())
            return "There's no game here.";
        return null;
    }

    public Choice<int>? GetTimeOptions(GameContext ctx, Location location)
    {
        var choice = new Choice<int>("How long do you want to search?");
        choice.AddOption("Quick scan - 15 min", 15);
        choice.AddOption("Thorough search - 30 min", 30);
        choice.AddOption("Cancel", 0);
        return choice;
    }

    public (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime)
    {
        var capacities = ctx.player.GetCapacities();
        var effectModifiers = ctx.player.EffectRegistry.GetCapacityModifiers();

        // Hunting benefits from perception and consciousness
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            effectModifiers,
            checkMoving: true,      // Need to move quietly
            checkBreathing: false,  // Not physically demanding
            effectRegistry: ctx.player.EffectRegistry
        );

        // Check perception impairment separately for warning
        var perception = AbilityCalculator.CalculatePerception(
            ctx.player.Body, effectModifiers);
        if (AbilityCalculator.IsPerceptionImpaired(perception))
        {
            warnings.Add("Your dulled senses make it harder to spot game.");
        }

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Hunting;

    public string GetActivityName() => "hunting";

    public bool AllowedInDarkness => false;

    public WorkResult Execute(GameContext ctx, Location location, int actualTime)
    {
        GameDisplay.AddNarrative(ctx, "You scan the area for signs of game...");

        // First, check herd registry for persistent large game
        if (ctx.Map != null)
        {
            var pos = ctx.Map.GetPosition(location);
            if (pos.HasValue)
            {
                var herdResult = ctx.Herds.SearchForLargeGame(pos.Value, actualTime);
                if (herdResult.HasValue)
                {
                    var (herd, animal) = herdResult.Value;
                    GameDisplay.AddNarrative(ctx, $"You spot {animal.GetTraitDescription()}.");
                    GameDisplay.AddNarrative(ctx, $"It's {animal.GetActivityDescription()}.");

                    // Return with FoundAnimal and HerdId set
                    return new WorkResult([], null, actualTime, false, animal, herd.Id);
                }
            }
        }

        // Fall back to territory-based spawning for small game
        var territory = location.GetFeature<AnimalTerritoryFeature>();
        if (territory == null || !territory.CanHunt())
        {
            WebIO.ShowWorkResult(ctx, "Hunting", "You find no game. The area seems quiet.", []);
            return WorkResult.Empty(actualTime);
        }

        // Search for small game
        var found = territory.SearchForGame(actualTime);

        // Perception impairment reduces effective search time by 25%
        var perception = AbilityCalculator.CalculatePerception(
            ctx.player.Body, ctx.player.EffectRegistry.GetCapacityModifiers());
        if (AbilityCalculator.IsPerceptionImpaired(perception) && found == null)
        {
            // Second chance with reduced time if impaired and found nothing
            int reducedTime = (int)(actualTime * 0.75);
            found = territory.SearchForGame(reducedTime);
        }

        if (found == null)
        {
            // Show popup only when no animal found (hunt ends here)
            WebIO.ShowWorkResult(ctx, "Hunting", "You find no game. The area seems quiet.", []);
            return WorkResult.Empty(actualTime);
        }

        // Found a small animal - no popup, hunt continues interactively
        GameDisplay.AddNarrative(ctx, $"You spot {found.GetTraitDescription()}.");
        GameDisplay.AddNarrative(ctx, $"It's {found.GetActivityDescription()}.");

        // Return with FoundAnimal set (no HerdId - spawned from territory)
        return new WorkResult([], null, actualTime, false, found);
    }
}
