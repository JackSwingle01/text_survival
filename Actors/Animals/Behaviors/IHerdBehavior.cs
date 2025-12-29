using text_survival.Actions;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Strategy interface for herd AI behavior.
/// Different animal types have different behavior implementations.
/// </summary>
public interface IHerdBehavior
{
    /// <summary>
    /// Update herd state for elapsed time. Called every game tick.
    /// </summary>
    /// <param name="herd">The herd to update.</param>
    /// <param name="elapsedMinutes">Minutes elapsed since last update.</param>
    /// <param name="ctx">Game context for accessing map, player state, other herds.</param>
    /// <returns>Result containing any encounters, narratives, or carcass creations.</returns>
    HerdUpdateResult Update(Herd herd, int elapsedMinutes, GameContext ctx);

    /// <summary>
    /// Trigger flee response (called externally when hunt fails or predator attacks).
    /// </summary>
    /// <param name="herd">The herd to flee.</param>
    /// <param name="threatSource">Position of the threat to flee from.</param>
    /// <param name="ctx">Game context.</param>
    void TriggerFlee(Herd herd, GridPosition threatSource, GameContext ctx);

    /// <summary>
    /// Get visibility factor for player searching (1.0 = normal, lower = harder to find).
    /// </summary>
    double GetVisibilityFactor(Herd herd);
}
