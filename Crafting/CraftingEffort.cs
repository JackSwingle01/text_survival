using text_survival.Actions;
using text_survival.Bodies;

namespace text_survival.Crafting;

/// <summary>
/// How long a recipe actually takes this player, right now. A recipe's
/// CraftingTimeMinutes is the time for someone whole, warm, dry and working in
/// daylight; everything else costs.
/// </summary>
public static class CraftingEffort
{
    /// <summary>Below this, the hands are unsteady enough to slow the work.</summary>
    private const double DexterityThreshold = 0.7;

    /// <summary>Time multiplier at zero dexterity: half again as long.</summary>
    private const double MaxDexterityPenaltyFactor = 0.5;

    /// <summary>
    /// Minutes this craft will take, and what to tell the player about why it is slow.
    /// </summary>
    public static (int minutes, List<string> warnings) ForRecipe(GameContext ctx, CraftOption option)
    {
        var capacities = ctx.player.GetCapacities();

        // Injured or shaking hands, per the same rules as every other kind of work.
        var (timeFactor, warnings) = AbilityCalculator.GetWorkImpairments(
            capacities,
            ctx.player.EffectRegistry.GetCapacityModifiers(),
            checkManipulation: true,
            effectRegistry: ctx.player.EffectRegistry);

        if (AbilityCalculator.IsConsciousnessImpaired(capacities.Consciousness))
        {
            timeFactor *= 1.25;
            warnings.Add("Your foggy mind slows the work.");
        }

        // Dexterity folds in the things the body alone does not know about -
        // darkness, wet hands, general frailty.
        double dexterity = AbilityCalculator.GetDexterity(ctx.player, ctx);
        if (dexterity < DexterityThreshold)
        {
            double shortfall = (DexterityThreshold - dexterity) / DexterityThreshold;
            timeFactor *= 1.0 + shortfall * MaxDexterityPenaltyFactor;
            warnings.Add(DexterityWarning(ctx));
        }

        return ((int)(option.CraftingTimeMinutes * timeFactor), warnings);
    }

    /// <summary>Name the most likely culprit, so the player can act on it.</summary>
    private static string DexterityWarning(GameContext ctx)
    {
        var context = AbilityContext.FromFullContext(
            ctx.player, ctx.Inventory, ctx.CurrentLocation, ctx.GameTime.Hour);

        if (context.DarknessLevel > 0.5 && !context.HasLightSource)
            return "The darkness makes the work harder.";
        if (context.WetnessPct > 0.3)
            return "Your wet hands slow the work.";
        return "Your unsteady hands slow the work.";
    }
}
