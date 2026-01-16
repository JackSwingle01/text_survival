using text_survival.Actions;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Api;

/// <summary>
/// Handles actions when player is in idle/camp state (no pending activity).
/// </summary>
public static class IdleActions
{
    /// <summary>
    /// Process movement to adjacent tile.
    /// Format: "move_x_y" where x,y are grid coordinates
    /// </summary>
    public static GameResponse ProcessMove(GameContext ctx, string choiceId)
    {
        // Parse coordinates from "move_x_y"
        var parts = choiceId.Split('_');
        if (parts.Length != 3 || !int.TryParse(parts[1], out int x) || !int.TryParse(parts[2], out int y))
        {
            return GameResponse.Error($"Invalid move format: {choiceId}");
        }

        var targetPos = new GridPosition(x, y);
        var destination = ctx.Map?.GetLocationAt(targetPos);

        if (destination == null)
            return GameResponse.Error("Invalid destination");

        if (!ctx.Map!.CanMoveTo(x, y))
            return GameResponse.Error("Cannot move to that location");

        // Check for hazardous terrain (TerrainHazardLevel > 0)
        if (destination.TerrainHazardLevel > 0)
        {
            // Set up hazard choice
            int quickTime = destination.BaseTraversalMinutes;
            int carefulTime = (int)(destination.BaseTraversalMinutes * 1.5);
            double injuryRisk = destination.GetEffectiveTerrainHazard() * (1 - ctx.player.Dexterity);

            ctx.PendingActivity = new PendingActivityState
            {
                Phase = ActivityPhase.TravelHazardPending,
                Travel = new TravelSnapshot(
                    TargetX: x,
                    TargetY: y,
                    OriginX: ctx.Map.CurrentPosition.X,
                    OriginY: ctx.Map.CurrentPosition.Y,
                    IsHazardous: true,
                    QuickTimeMinutes: quickTime,
                    CarefulTimeMinutes: carefulTime,
                    InjuryRisk: injuryRisk,
                    HazardDescription: destination.Terrain.ToString(),
                    QuickTravelChosen: null,
                    StatusMessage: null,
                    IsFirstVisit: !destination.Explored
                )
            };
            return GameResponse.Success(ctx);
        }

        // Normal travel
        int travelMinutes = destination.BaseTraversalMinutes;
        ctx.Map.MoveTo(destination, ctx.player);
        ctx.Update(travelMinutes, ActivityType.Traveling);

        return GameResponse.Success(ctx);
    }

    /// <summary>Process fire management actions.</summary>
    public static GameResponse ProcessFire(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from FireController
        return GameResponse.Error($"Fire action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process inventory actions.</summary>
    public static GameResponse ProcessInventory(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from InventoryController
        return GameResponse.Error($"Inventory action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process storage actions.</summary>
    public static GameResponse ProcessStorage(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from StorageController
        return GameResponse.Error($"Storage action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process crafting actions.</summary>
    public static GameResponse ProcessCrafting(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from CraftingController
        return GameResponse.Error($"Crafting action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process eating/drinking actions.</summary>
    public static GameResponse ProcessEat(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from EatingController
        return GameResponse.Error($"Eat action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process sleep actions.</summary>
    public static GameResponse ProcessSleep(GameContext ctx, string choiceId)
    {
        // Parse sleep duration from "sleep_60" etc
        var parts = choiceId.Split('_');
        int minutes = 60;
        if (parts.Length >= 2 && int.TryParse(parts[1], out int parsed))
        {
            minutes = parsed;
        }

        ctx.player.Body.Rest(minutes, ctx.CurrentLocation, null);
        ctx.Update(minutes, ActivityType.Sleeping);

        return GameResponse.Success(ctx);
    }

    /// <summary>Process wait actions.</summary>
    public static GameResponse ProcessWait(GameContext ctx, string choiceId)
    {
        // Parse wait duration from "wait_10" etc
        var parts = choiceId.Split('_');
        int minutes = 10;
        if (parts.Length >= 2 && int.TryParse(parts[1], out int parsed))
        {
            minutes = parsed;
        }

        ctx.Update(minutes, ActivityType.Resting);
        return GameResponse.Success(ctx);
    }

    /// <summary>Process forage actions.</summary>
    public static GameResponse ProcessForage(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from WorkController
        return GameResponse.Error($"Forage action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process hunt actions (starting a hunt).</summary>
    public static GameResponse ProcessHunt(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from WorkController
        return GameResponse.Error($"Hunt action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process chop actions.</summary>
    public static GameResponse ProcessChop(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from WorkController
        return GameResponse.Error($"Chop action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process harvest actions.</summary>
    public static GameResponse ProcessHarvest(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from WorkController
        return GameResponse.Error($"Harvest action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process butcher actions.</summary>
    public static GameResponse ProcessButcher(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from ButcherController
        return GameResponse.Error($"Butcher action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process torch actions.</summary>
    public static GameResponse ProcessTorch(GameContext ctx, string choiceId)
    {
        // TODO: Migrate from relevant controller
        return GameResponse.Error($"Torch action '{choiceId}' not yet implemented in ActionRouter");
    }

    /// <summary>Process continue/dismiss actions.</summary>
    public static GameResponse ProcessContinue(GameContext ctx)
    {
        // Just return current state
        return GameResponse.Success(ctx);
    }

    /// <summary>Process dismiss actions.</summary>
    public static GameResponse ProcessDismiss(GameContext ctx)
    {
        // Clear any notification state and return current state
        return GameResponse.Success(ctx);
    }
}
