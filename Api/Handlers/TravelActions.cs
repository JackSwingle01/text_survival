using text_survival.Actions;

namespace text_survival.Api;

/// <summary>
/// Handles travel-related actions during pending travel states.
/// </summary>
public static class TravelActions
{
    /// <summary>
    /// Process hazard choice (quick vs careful travel).
    /// </summary>
    public static GameResponse ProcessHazardChoice(GameContext ctx, string choiceId)
    {
        if (ctx.PendingActivity?.Phase != ActivityPhase.TravelHazardPending)
            return GameResponse.Error("No pending hazard choice");

        if (ctx.PendingActivity.Travel == null)
            return GameResponse.Error("Travel state missing");

        var travel = ctx.PendingActivity.Travel;
        bool quickTravel = choiceId.ToLowerInvariant() == "quick";

        // Update travel with choice
        ctx.PendingActivity.Travel = travel with
        {
            QuickTravelChosen = quickTravel
        };

        // Execute the travel
        var targetPos = new Environments.Grid.GridPosition(travel.TargetX, travel.TargetY);
        var destination = ctx.Map?.GetLocationAt(targetPos);

        if (destination == null)
            return GameResponse.Error("Invalid destination");

        int travelMinutes = quickTravel ? travel.QuickTimeMinutes : travel.CarefulTimeMinutes;

        // Apply injury risk if quick travel
        if (quickTravel && Random.Shared.NextDouble() < travel.InjuryRisk)
        {
            // Apply minor injury
            var damageInfo = new Bodies.DamageInfo(
                2 + Random.Shared.NextDouble() * 3,
                Bodies.DamageType.Blunt,
                Bodies.BodyTarget.Random
            );
            ctx.player.Damage(damageInfo);
            UI.GameDisplay.AddWarning(ctx, "You slip and hurt yourself!");
        }

        // Execute travel
        ctx.Map?.MoveTo(destination, ctx.player);
        ctx.Update(travelMinutes, ActivityType.Traveling);

        // Clear pending activity
        ctx.PendingActivity = null;

        return GameResponse.Success(ctx);
    }

    /// <summary>
    /// Process impairment confirmation.
    /// </summary>
    public static GameResponse ProcessImpairmentChoice(GameContext ctx, string choiceId)
    {
        if (ctx.PendingActivity?.Phase != ActivityPhase.TravelImpairmentWarning)
            return GameResponse.Error("No pending impairment warning");

        bool proceed = choiceId.ToLowerInvariant() == "proceed" || choiceId.ToLowerInvariant() == "confirm";

        if (proceed)
        {
            // Continue with travel despite impairment
            if (ctx.PendingActivity.Travel != null)
            {
                var travel = ctx.PendingActivity.Travel;
                var targetPos = new Environments.Grid.GridPosition(travel.TargetX, travel.TargetY);
                var destination = ctx.Map?.GetLocationAt(targetPos);

                if (destination != null)
                {
                    ctx.Map?.MoveTo(destination, ctx.player);
                    ctx.Update(travel.QuickTimeMinutes, ActivityType.Traveling);
                }
            }
        }

        ctx.PendingActivity = null;
        return GameResponse.Success(ctx);
    }

    /// <summary>
    /// Process blocked path continue (dismiss message).
    /// </summary>
    public static GameResponse ProcessBlockedContinue(GameContext ctx)
    {
        ctx.PendingActivity = null;
        return GameResponse.Success(ctx);
    }

    /// <summary>
    /// Process travel interrupted choice (continue or stop).
    /// </summary>
    public static GameResponse ProcessInterruptedChoice(GameContext ctx, string choiceId)
    {
        if (ctx.PendingActivity?.Phase != ActivityPhase.TravelInterrupted)
            return GameResponse.Error("No pending travel interruption");

        bool continueTravel = choiceId.ToLowerInvariant() == "continue";

        if (continueTravel && ctx.PendingActivity.Travel != null)
        {
            // Resume travel to original destination
            var travel = ctx.PendingActivity.Travel;
            var targetPos = new Environments.Grid.GridPosition(travel.TargetX, travel.TargetY);
            var destination = ctx.Map?.GetLocationAt(targetPos);

            if (destination != null)
            {
                ctx.Map?.MoveTo(destination, ctx.player);
                ctx.Update(travel.QuickTimeMinutes, ActivityType.Traveling);
            }
        }

        ctx.PendingActivity = null;
        return GameResponse.Success(ctx);
    }
}
