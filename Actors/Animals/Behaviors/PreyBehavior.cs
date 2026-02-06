using text_survival.Actions;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Behavior for prey animals (caribou, megaloceros, bison).
/// States: Resting, Grazing, Fleeing.
/// </summary>
public class PreyBehavior : IHerdBehavior
{
    // Hunger rates per minute
    private const double HungerRatePerMinute = 0.003;   // ~5.5 hours to get hungry
    private const double GrazeRatePerMinute = 0.008;    // ~2 hours grazing to satiate

    public HerdUpdateResult Update(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Tick travel progress first
        if (herd.IsTraveling)
        {
            bool arrived = herd.UpdateTravel(elapsedMinutes);
            if (!arrived) return HerdUpdateResult.None; // Still traveling, skip behavior

            // Just arrived - if fleeing, transition to resting
            if (herd.State == HerdState.Fleeing)
            {
                herd.TransitionTo(HerdState.Resting);
                return HerdUpdateResult.None;
            }
        }

        herd.StateTimeMinutes += elapsedMinutes;
        herd.Hunger = Math.Clamp(herd.Hunger + elapsedMinutes * HungerRatePerMinute, 0, 1);

        // Wounded herds heal over time
        if (herd.IsWounded)
        {
            herd.WoundSeverity = Math.Max(0, herd.WoundSeverity - elapsedMinutes * 0.0002);
            if (herd.WoundSeverity <= 0)
                herd.IsWounded = false;
        }

        switch (herd.State)
        {
            case HerdState.Resting:
                return UpdateResting(herd, elapsedMinutes);

            case HerdState.Grazing:
                return UpdateGrazing(herd, elapsedMinutes);

            case HerdState.Fleeing:
                return ExecuteFlee(herd, ctx);

            case HerdState.Alert:
                // Prey in alert state flee immediately
                herd.TransitionTo(HerdState.Fleeing);
                return HerdUpdateResult.None;

            default:
                herd.TransitionTo(HerdState.Resting);
                return HerdUpdateResult.None;
        }
    }

    private static HerdUpdateResult UpdateResting(Herd herd, int elapsedMinutes)
    {
        // Hungry? Start grazing
        if (herd.Hunger > 0.5)
        {
            herd.TransitionTo(HerdState.Grazing);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateGrazing(Herd herd, int elapsedMinutes)
    {
        // Reduce hunger while grazing
        herd.Hunger = Math.Max(0, herd.Hunger - elapsedMinutes * GrazeRatePerMinute);

        // Graze at current location, depleting resources
        herd.GrazeHere(elapsedMinutes);

        // Move slowly within territory (faster if area is grazed)
        // Base 1% per minute, increases with grazing depletion
        double moveChancePerMinute = 0.01 + 0.01 * herd.GetGrazedLevel();
        herd.TryPatrolTerritory(elapsedMinutes, moveChancePerMinute);

        // Full? Rest
        if (herd.Hunger < 0.2)
        {
            herd.TransitionTo(HerdState.Resting);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult ExecuteFlee(Herd herd, GameContext ctx)
    {
        if (ctx.Map == null) return HerdUpdateResult.None;

        var playerPos = ctx.Map.CurrentPosition;
        var fleeTarget = herd.GetFleeTarget(playerPos);

        if (fleeTarget != null && fleeTarget != herd.Position)
        {
            var previousPosition = herd.Position;

            if (!herd.StartTravelTo(fleeTarget.Value, ctx.Map))
            {
                herd.TransitionTo(HerdState.Resting);
                return HerdUpdateResult.None;
            }

            // Narrative if player can see them bolt
            if (ctx.Map.CurrentPosition == previousPosition)
            {
                string direction = Herd.GetCardinalDirection(previousPosition, fleeTarget.Value);
                return HerdUpdateResult.WithNarrative(
                    $"The {herd.AnimalType.DisplayName().ToLower()} herd bolts {direction}.");
            }

            return HerdUpdateResult.None;
        }
        else
        {
            herd.TransitionTo(HerdState.Resting);
        }

        return HerdUpdateResult.None;
    }

    public void TriggerFlee(Herd herd, GridPosition threatSource, GameContext ctx)
    {
        herd.TransitionTo(HerdState.Fleeing);
    }

    public double GetVisibilityFactor(Herd herd)
    {
        double visibility = herd.State switch
        {
            HerdState.Grazing => 1.0,   // Moving, visible
            HerdState.Resting => 0.5,   // Bedded down
            HerdState.Fleeing => 0.1,   // Already spooked
            HerdState.Alert => 0.3,     // Frozen, watching
            _ => 1.0
        };

        // Larger herds easier to spot (diminishing returns)
        visibility *= 1 + Math.Log(herd.Count + 1) * 0.15;

        return visibility;
    }
}
