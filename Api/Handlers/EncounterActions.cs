using text_survival.Actions;
using text_survival.Items;

namespace text_survival.Api;

/// <summary>
/// Handles encounter-related actions.
/// </summary>
public static class EncounterActions
{
    /// <summary>
    /// Process a player's choice during an encounter.
    /// </summary>
    public static GameResponse ProcessChoice(GameContext ctx, string choiceId)
    {
        if (ctx.PendingActivity?.Phase != ActivityPhase.EncounterActive)
            return GameResponse.Error("No active encounter");

        if (ctx.PendingActivity.Encounter == null)
            return GameResponse.Error("Encounter state missing");

        return choiceId.ToLowerInvariant() switch
        {
            "stand" => ProcessStand(ctx),
            "back" => ProcessBack(ctx),
            "attack" => ProcessAttack(ctx),
            "run" => ProcessRun(ctx),
            "drop_meat" => ProcessDropMeat(ctx),
            _ => GameResponse.Error($"Unknown encounter action: {choiceId}")
        };
    }

    /// <summary>
    /// Process continue after encounter outcome.
    /// </summary>
    public static GameResponse ProcessContinue(GameContext ctx)
    {
        if (ctx.PendingActivity?.Phase != ActivityPhase.EncounterOutcome)
            return GameResponse.Error("No encounter outcome to continue from");

        ctx.PendingActivity = null;
        return GameResponse.Success(ctx);
    }

    private static GameResponse ProcessStand(GameContext ctx)
    {
        var encounter = ctx.PendingActivity!.Encounter!;
        var predator = ctx.PendingActivity.EncounterPredator;

        double currentBoldness = predator?.EncounterBoldness ?? encounter.Boldness;
        double newBoldness = Math.Max(0, currentBoldness - 0.15);
        if (predator != null)
            predator.EncounterBoldness = newBoldness;

        if (newBoldness < 0.3)
        {
            ctx.PendingActivity.Phase = ActivityPhase.EncounterOutcome;
            ctx.PendingActivity.Encounter = encounter with
            {
                Boldness = 0,
                BoldnessDescriptor = "retreating",
                StatusMessage = $"The {encounter.AnimalType.ToLower()} backs away and retreats."
            };
        }
        else
        {
            ctx.PendingActivity.Encounter = encounter with
            {
                Boldness = newBoldness,
                BoldnessDescriptor = GetBoldnessDescriptor(newBoldness),
                StatusMessage = $"The {encounter.AnimalType.ToLower()} seems less certain."
            };
        }

        ctx.Update(1, ActivityType.Encounter);
        return GameResponse.Success(ctx);
    }

    private static GameResponse ProcessBack(GameContext ctx)
    {
        var encounter = ctx.PendingActivity!.Encounter!;
        var predator = ctx.PendingActivity.EncounterPredator;

        double currentDistance = predator?.DistanceFromPlayer ?? encounter.Distance;
        double currentBoldness = predator?.EncounterBoldness ?? encounter.Boldness;
        double newDistance = currentDistance + 5;
        double newBoldness = Math.Min(1.0, currentBoldness + 0.05);

        if (predator != null)
        {
            predator.DistanceFromPlayer = newDistance;
            predator.EncounterBoldness = newBoldness;
        }

        // Check for charge trigger
        if (newBoldness >= 0.8 && currentDistance < 15)
        {
            // Transition to combat
            return TransitionToCombat(ctx, predator, "The predator charges!");
        }

        if (newDistance > 40)
        {
            ctx.PendingActivity.Phase = ActivityPhase.EncounterOutcome;
            ctx.PendingActivity.Encounter = encounter with
            {
                Distance = newDistance,
                Boldness = newBoldness,
                BoldnessDescriptor = GetBoldnessDescriptor(newBoldness),
                StatusMessage = $"You've put enough distance between you and the {encounter.AnimalType.ToLower()}."
            };
        }
        else
        {
            ctx.PendingActivity.Encounter = encounter with
            {
                Distance = newDistance,
                Boldness = newBoldness,
                BoldnessDescriptor = GetBoldnessDescriptor(newBoldness),
                StatusMessage = $"You back away slowly..."
            };
        }

        ctx.Update(1, ActivityType.Encounter);
        return GameResponse.Success(ctx);
    }

    private static GameResponse ProcessAttack(GameContext ctx)
    {
        var predator = ctx.PendingActivity!.EncounterPredator;
        return TransitionToCombat(ctx, predator, "You charge!");
    }

    private static GameResponse ProcessRun(GameContext ctx)
    {
        var encounter = ctx.PendingActivity!.Encounter!;
        var predator = ctx.PendingActivity.EncounterPredator;

        // Running might trigger a chase
        double chaseChance = predator?.EncounterBoldness ?? encounter.Boldness;
        if (Random.Shared.NextDouble() < chaseChance)
        {
            return TransitionToCombat(ctx, predator, $"The {encounter.AnimalType.ToLower()} gives chase!");
        }

        ctx.PendingActivity.Phase = ActivityPhase.EncounterOutcome;
        ctx.PendingActivity.Encounter = encounter with
        {
            StatusMessage = "You run and escape!"
        };

        return GameResponse.Success(ctx);
    }

    private static GameResponse ProcessDropMeat(GameContext ctx)
    {
        var encounter = ctx.PendingActivity!.Encounter!;

        // Drop meat to distract
        double meatWeight = ctx.Inventory.GetWeight(ResourceCategory.Food);
        if (meatWeight < 0.5)
        {
            return GameResponse.Error("Not enough meat to drop");
        }

        // Remove some meat
        ctx.Inventory.Pop(Resource.RawMeat);

        ctx.PendingActivity.Phase = ActivityPhase.EncounterOutcome;
        ctx.PendingActivity.Encounter = encounter with
        {
            StatusMessage = $"The {encounter.AnimalType.ToLower()} goes for the meat. You slip away."
        };

        return GameResponse.Success(ctx);
    }

    private static GameResponse TransitionToCombat(GameContext ctx, Actors.Animals.Animal? predator, string message)
    {
        // For now, end encounter and queue combat
        ctx.PendingActivity!.Phase = ActivityPhase.EncounterOutcome;
        ctx.PendingActivity.Encounter = ctx.PendingActivity.Encounter! with
        {
            StatusMessage = message + " (Combat not yet implemented in new router)"
        };
        return GameResponse.Success(ctx);
    }

    private static string GetBoldnessDescriptor(double boldness)
    {
        return boldness switch
        {
            >= 0.8 => "aggressive",
            >= 0.6 => "bold",
            >= 0.4 => "wary",
            _ => "hesitant"
        };
    }
}
