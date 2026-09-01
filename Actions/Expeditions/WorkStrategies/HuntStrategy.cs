using text_survival.Actions;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.UI;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy for the search phase of hunting.
/// Large game is searched for in herds on the tile; small game in the tile's SmallGameFeature.
/// If animal found, returns WorkResult.FoundAnimal for caller to handle interactive hunt.
/// </summary>
public class HuntStrategy : IWorkStrategy
{
    public Task<string?> ValidateLocation(GameContext ctx, Location location)
    {
        // Large game: herds standing on this tile
        if (ctx.Map != null && ctx.Herds.At(ctx.Map.GetPosition(location)).Any(h => h.Count > 0))
            return Task.FromResult<string?>(null);

        // Small game: local density
        var smallGame = location.GetFeature<SmallGameFeature>();
        if (smallGame == null)
            return Task.FromResult<string?>("There's no game to be found here.");
        if (!smallGame.CanHunt())
            return Task.FromResult<string?>("There's no game here.");
        return Task.FromResult<string?>(null);
    }

    public Task<Choice<int>?> GetTimeOptions(GameContext ctx, Location location)
    {
        var choice = new Choice<int>("How long do you want to search?");
        choice.AddOption("Quick scan - 15 min", 15);
        choice.AddOption("Thorough search - 30 min", 30);
        choice.AddOption("Cancel", 0);
        return Task.FromResult<Choice<int>?>(choice);
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
        double perception = AbilityCalculator.GetPerception(ctx.player, ctx);
        if (AbilityCalculator.IsPerceptionImpaired(perception))
        {
            warnings.Add("Your dulled senses make it harder to spot game.");
        }

        return ((int)(baseTime * timeFactor), warnings);
    }

    public ActivityType GetActivityType() => ActivityType.Hunting;

    public string GetActivityName() => "hunting";

    public bool AllowedInDarkness => false;

    public async Task<WorkResult> Execute(GameContext ctx, Location location, int actualTime)
    {
        GameDisplay.AddNarrative(ctx, "You scan the area for signs of game...");

        // First, check herd registry for persistent large game
        if (ctx.Map != null)
        {
            var pos = ctx.Map.GetPosition(location);

            var herdResult = ctx.Herds.SearchForLargeGame(pos, actualTime);
            if (herdResult.HasValue)
            {
                var (_, animal) = herdResult.Value;
                GameDisplay.AddNarrative(ctx, $"You spot {animal.GetTraitDescription()}.");
                GameDisplay.AddNarrative(ctx, $"It's {animal.GetActivityDescription()}.");

                return new WorkResult([], null, actualTime, false, animal);
            }
        }

        // Fall back to small game density
        var territory = location.GetFeature<SmallGameFeature>();
        if (territory == null || !territory.CanHunt())
        {
            await ctx.Ui.ShowWorkResult(new WorkResultView("Hunting", "You find no game. The area seems quiet.", []));
            return WorkResult.Empty(actualTime);
        }

        // Search for small game
        var found = territory.SearchForGame(actualTime, location, ctx.Map);

        // Perception impairment reduces effective search time by 25%
        double perception = AbilityCalculator.GetPerception(ctx.player, ctx);
        if (AbilityCalculator.IsPerceptionImpaired(perception) && found == null)
        {
            // Second chance with reduced time if impaired and found nothing
            int reducedTime = (int)(actualTime * 0.75);
            found = territory.SearchForGame(reducedTime, location, ctx.Map);
        }

        if (found == null)
        {
            // Show popup only when no animal found (hunt ends here)
            await ctx.Ui.ShowWorkResult(new WorkResultView("Hunting", "You find no game. The area seems quiet.", []));
            return WorkResult.Empty(actualTime);
        }

        // Found a small animal - no popup, hunt continues interactively
        GameDisplay.AddNarrative(ctx, $"You spot {found.GetTraitDescription()}.");
        GameDisplay.AddNarrative(ctx, $"It's {found.GetActivityDescription()}.");

        // Return with FoundAnimal set (no herd - spawned from small game density)
        return new WorkResult([], null, actualTime, false, found);
    }
}
