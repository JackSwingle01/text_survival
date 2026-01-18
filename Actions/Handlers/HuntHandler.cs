using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Items;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles hunting calculations for weapon range and accuracy.
/// Stateless handler for game logic calculations.
/// </summary>
public static class HuntHandler
{
    #region Stone Constants

    /// <summary>
    /// Get effective throwing range for a stone.
    /// </summary>
    public static double GetStoneRange() => 15.0;

    /// <summary>
    /// Get base accuracy for a thrown stone.
    /// </summary>
    public static double GetStoneBaseAccuracy() => 0.90;

    #endregion

    #region Spear Methods

    /// <summary>
    /// Get effective throwing range for a spear.
    /// Stone-tipped spears have longer range than wooden spears.
    /// </summary>
    public static double GetSpearRange(Gear spear)
    {
        // Stone-tipped spears have longer range
        return spear.Name.Contains("Stone") ? 25 : 20;
    }

    /// <summary>
    /// Get base accuracy for a spear.
    /// Stone-tipped spears are more accurate than wooden spears.
    /// </summary>
    public static double GetSpearBaseAccuracy(Gear spear)
    {
        // Stone-tipped spears are more accurate
        return spear.Name.Contains("Stone") ? 0.75 : 0.70;
    }

    /// <summary>
    /// Calculate hit chance for throwing a spear at a target.
    /// Factors in distance, weapon quality, target size, and player impairments.
    /// </summary>
    public static double CalculateSpearHitChance(Gear spear, Animal target, GameContext ctx)
    {
        var manipulation = ctx.player.GetCapacities().Manipulation;
        double manipPenalty = AbilityCalculator.IsManipulationImpaired(manipulation) ? 0.15 : 0.0;

        bool applySmallPenalty = target.Size == AnimalSize.Small;
        return HuntingCalculator.CalculateThrownAccuracy(
            target.DistanceFromPlayer,
            GetSpearRange(spear),
            GetSpearBaseAccuracy(spear),
            applySmallPenalty,
            manipPenalty
        );
    }

    /// <summary>
    /// Calculate hit chance for throwing a stone at a target.
    /// Stones are designed for small game and don't get a small target penalty.
    /// </summary>
    public static double CalculateStoneHitChance(Animal target, GameContext ctx)
    {
        var manipulation = ctx.player.GetCapacities().Manipulation;
        double manipPenalty = AbilityCalculator.IsManipulationImpaired(manipulation) ? 0.15 : 0.0;

        // Stones don't get small target penalty (they're designed for small game)
        return HuntingCalculator.CalculateThrownAccuracy(
            target.DistanceFromPlayer,
            GetStoneRange(),
            GetStoneBaseAccuracy(),
            targetIsSmall: false,
            manipPenalty
        );
    }

    /// <summary>
    /// Calculate thrown accuracy for both spears and stones.
    /// Used by hunt menu to show hit chance before player commits.
    /// </summary>
    public static double CalculateThrownAccuracy(
        GameContext ctx,
        Animal target,
        bool isSpear,
        Gear? spear = null)
    {
        var manipulation = ctx.player.GetCapacities().Manipulation;
        double manipPenalty = AbilityCalculator.IsManipulationImpaired(manipulation) ? 0.15 : 0.0;

        if (isSpear && spear != null)
        {
            bool applySmallPenalty = target.Size == AnimalSize.Small;
            return HuntingCalculator.CalculateThrownAccuracy(
                target.DistanceFromPlayer,
                GetSpearRange(spear),
                GetSpearBaseAccuracy(spear),
                applySmallPenalty,
                manipPenalty
            );
        }
        else
        {
            // Stone throw (no small target penalty)
            return HuntingCalculator.CalculateThrownAccuracy(
                target.DistanceFromPlayer,
                GetStoneRange(),
                GetStoneBaseAccuracy(),
                targetIsSmall: false,
                manipPenalty
            );
        }
    }

    #endregion
}
