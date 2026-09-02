using text_survival.Actors;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Items;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Pure game logic for cooking and melting actions.
/// UI code calls these methods; NPCs can call them directly.
/// </summary>
public static class CookingHandler
{
    // ============================================
    // Constants
    // ============================================

    public const int CookMeatTimeMinutes = 15;
    public const int CookFishTimeMinutes = 12;  // Fish cooks faster than meat
    public const int MeltSnowTimeMinutes = 10;
    public const double MeltSnowWaterLiters = 1.0;

    // ============================================
    // Data Types
    // ============================================

    /// <summary>
    /// Result of a cooking or melting action.
    /// </summary>
    public record CookingResult(bool Success, string Message, double Amount);

    // ============================================
    // Pure Game Logic - Queries
    // ============================================

    /// <summary>
    /// Check if location has an active heat source suitable for cooking.
    /// </summary>
    public static bool CanCookAt(Location location)
    {
        var fire = location.GetFeature<HeatSourceFeature>();
        return fire != null && fire.IsActive;
    }

    /// <summary>
    /// Check if actor can cook meat (has raw meat and is at active fire).
    /// </summary>
    public static bool CanCookMeat(Inventory inv, Location location)
    {
        return CanCookAt(location) && inv.Count(Resource.RawMeat) > 0;
    }

    /// <summary>
    /// Check if actor can melt snow (at active fire).
    /// Snow is assumed to be freely available in this ice age setting.
    /// </summary>
    public static bool CanMeltSnow(Location location)
    {
        return CanCookAt(location);
    }

    // ============================================
    // Pure Game Logic - Execution
    // ============================================

    /// <summary>
    /// Cook one unit of raw meat. Consumes RawMeat, produces CookedMeat.
    /// Returns result with success status and amount cooked.
    /// </summary>
    public static CookingResult CookMeat(Inventory inv, Location location)
    {
        if (!CanCookAt(location))
            return new CookingResult(false, "No active fire to cook on.", 0);

        if (inv.Count(Resource.RawMeat) <= 0)
            return new CookingResult(false, "No raw meat to cook.", 0);

        double weight = inv.Pop(Resource.RawMeat);
        inv.Add(Resource.CookedMeat, weight);

        return new CookingResult(true, $"Cooked {weight:F1}kg of meat.", weight);
    }

    /// <summary>
    /// Cook one unit of raw fish. Consumes RawFish, produces CookedFish.
    /// Fish cooks faster than meat (12 min vs 15 min).
    /// Returns result with success status and amount cooked.
    /// </summary>
    public static CookingResult CookFish(Inventory inv, Location location)
    {
        if (!CanCookAt(location))
            return new CookingResult(false, "No active fire to cook on.", 0);

        if (inv.Count(Resource.RawFish) <= 0)
            return new CookingResult(false, "No raw fish to cook.", 0);

        double weight = inv.Pop(Resource.RawFish);
        inv.Add(Resource.CookedFish, weight);

        return new CookingResult(true, $"Cooked {weight:F1}kg of fish.", weight);
    }

    /// <summary>
    /// Melt snow to produce water.
    /// Returns result with success status and amount of water produced.
    /// </summary>
    public static CookingResult MeltSnow(Inventory inv, Location location)
    {
        if (!CanCookAt(location))
            return new CookingResult(false, "No active fire to melt snow.", 0);

        inv.Add(Resource.Water, MeltSnowWaterLiters);

        return new CookingResult(true, $"Melted snow for {MeltSnowWaterLiters:F1}L of water.", MeltSnowWaterLiters);
    }

    // ============================================
    // NPC Entry Points
    // ============================================

    /// <summary>
    /// NPC meat cooking - cooks one unit of raw meat.
    /// Returns true if cooking succeeded.
    /// </summary>
    /// <summary>
    /// Run a cooking action the food screen handed back: the time it takes, then the
    /// transformation itself.
    /// </summary>
    public static async Task RunPendingAction(GameContext ctx, PendingFoodAction action)
    {
        string statusText = action.Action switch
        {
            FoodAction.CookMeat => "Cooking meat...",
            FoodAction.CookFish => "Cooking fish...",
            FoodAction.MeltSnow => "Melting snow...",
            _ => throw new InvalidOperationException($"{action.Action} does not pass time and should not reach here.")
        };

        using (var view = ctx.Ui.BeginProgress(ProgressKind.Activity, statusText))
        {
            await Pacing.PassTime(ctx, action.Minutes, ActivityType.TendingFire, view);
        }

        if (!ctx.player.IsAlive) return;

        CookingResult result = action.Action switch
        {
            FoodAction.CookMeat => CookMeat(ctx.Inventory, ctx.CurrentLocation),
            FoodAction.CookFish => CookFish(ctx.Inventory, ctx.CurrentLocation),
            FoodAction.MeltSnow => MeltSnow(ctx.Inventory, ctx.CurrentLocation),
            _ => throw new InvalidOperationException($"Unhandled cooking action: {action.Action}")
        };

        if (result.Success)
            GameDisplay.AddSuccess(ctx, result.Message);
        else
            GameDisplay.AddWarning(ctx, result.Message);
    }

    public static bool CookMeatNPC(Actor actor, Inventory inv, Location location)
    {
        var result = CookMeat(inv, location);
        if (result.Success)
        {
            (actor as NPC)?.Trace($"[NPC:{actor.Name}] Cooked {result.Amount:F1}kg meat");
        }
        return result.Success;
    }

    /// <summary>
    /// NPC snow melting - melts snow to produce water.
    /// Returns true if melting succeeded.
    /// </summary>
    public static bool MeltSnowNPC(Actor actor, Inventory inv, Location location)
    {
        var result = MeltSnow(inv, location);
        if (result.Success)
        {
            (actor as NPC)?.Trace($"[NPC:{actor.Name}] Melted snow for {result.Amount:F1}L water");
        }
        return result.Success;
    }

    // ============================================
    // Desktop UI Entry Points (with time advancement)
    // ============================================

}