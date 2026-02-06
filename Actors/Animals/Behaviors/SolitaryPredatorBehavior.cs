using text_survival.Actions;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Behavior for solitary predators (bears).
/// States: Resting, Grazing (foraging), Patrolling, Feeding.
/// Bears can forage to reduce hunger (omnivore behavior) and are territorial near their den.
/// </summary>
public class SolitaryPredatorBehavior : IHerdBehavior
{
    private static readonly Random _rng = new();

    private const double HungerRatePerMinute = 0.0008;  // Slowest metabolism
    private const double ForageRatePerMinute = 0.003;   // Foraging reduces hunger
    private const int FeedingDurationMinutes = 90;      // Bears feed longer than wolves

    public HerdUpdateResult Update(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Tick travel progress first
        if (herd.IsTraveling)
        {
            herd.UpdateTravel(elapsedMinutes);
            if (herd.IsTraveling) return HerdUpdateResult.None; // Still traveling, skip behavior
        }

        herd.StateTimeMinutes += elapsedMinutes;
        herd.Hunger = Math.Clamp(herd.Hunger + elapsedMinutes * HungerRatePerMinute, 0, 1);

        switch (herd.State)
        {
            case HerdState.Resting:
                return UpdateResting(herd, ctx);

            case HerdState.Grazing:  // Bears use Grazing state for foraging
                return UpdateForaging(herd, elapsedMinutes, ctx);

            case HerdState.Patrolling:
                return UpdatePatrolling(herd, elapsedMinutes, ctx);

            case HerdState.Feeding:
                return UpdateFeeding(herd, ctx);

            case HerdState.Alert:
                return UpdateAlert(herd, ctx);

            case HerdState.Hunting:
                // Bears don't have a prolonged hunting state - they charge
                herd.TransitionTo(HerdState.Patrolling);
                return HerdUpdateResult.None;

            case HerdState.Fleeing:
                return UpdateFleeing(herd, ctx);

            default:
                herd.TransitionTo(HerdState.Resting);
                return HerdUpdateResult.None;
        }
    }

    private static HerdUpdateResult UpdateResting(Herd herd, GameContext ctx)
    {
        // Hungry? Start foraging
        if (herd.Hunger > 0.5)
        {
            herd.TransitionTo(HerdState.Grazing);
        }

        // Defend den if player enters
        if (herd.Position == herd.HomeTerritory.FirstOrDefault() && herd.IsPlayerHere)
        {
            // High chance to defend den
            if (_rng.NextDouble() < 0.7)
            {
                return HerdUpdateResult.WithEncounter(herd, isDefending: true);
            }
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateForaging(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Bears can reduce hunger by foraging (omnivore behavior)
        herd.Hunger = Math.Max(0, herd.Hunger - elapsedMinutes * ForageRatePerMinute);

        // Graze at current location, depleting resources
        herd.GrazeHere(elapsedMinutes);

        // Move within territory while foraging (faster if area is grazed)
        double moveChancePerMinute = 0.01 + 0.01 * herd.GetGrazedLevel();
        herd.TryPatrolTerritory(elapsedMinutes, moveChancePerMinute);

        // Sated? Rest
        if (herd.Hunger < 0.3)
        {
            herd.TransitionTo(HerdState.Resting);
        }

        // Check for player in tile
        if (herd.IsPlayerHere)
        {
            if (ShouldEngagePlayer(herd, ctx))
            {
                return HerdUpdateResult.WithEncounter(herd);
            }
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdatePatrolling(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Bears patrol less than wolves - mostly territory check
        double moveChancePerMinute = 0.01 + 0.01 * herd.GetGrazedLevel();
        herd.TryPatrolTerritory(elapsedMinutes, moveChancePerMinute);

        // Check for player in tile
        if (herd.IsPlayerHere)
        {
            if (ShouldEngagePlayer(herd, ctx))
            {
                return HerdUpdateResult.WithEncounter(herd);
            }
        }

        // Short patrol then rest
        if (herd.StateTimeMinutes > 60)
        {
            herd.TransitionTo(HerdState.Resting);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateAlert(Herd herd, GameContext ctx)
    {
        // Bears decide quickly
        if (herd.StateTimeMinutes > 2)
        {
            if (herd.IsPlayerHere)
            {
                if (ShouldEngagePlayer(herd, ctx))
                {
                    return HerdUpdateResult.WithEncounter(herd);
                }
            }

            // Not engaging - resume previous activity
            herd.TransitionTo(HerdState.Patrolling);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateFeeding(Herd herd, GameContext ctx)
    {
        // Defend kill if player enters
        if (herd.IsPlayerHere)
        {
            return HerdUpdateResult.WithEncounter(herd, isDefending: true);
        }

        // Bears feed longer than wolves
        if (herd.StateTimeMinutes > FeedingDurationMinutes)
        {
            herd.TransitionTo(HerdState.Resting);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateFleeing(Herd herd, GameContext ctx)
    {
        if (ctx.Map == null) return HerdUpdateResult.None;

        var fleeTarget = herd.GetFleeTarget(ctx.Map.CurrentPosition);

        if (fleeTarget != null && fleeTarget != herd.Position)
        {
            if (!herd.StartTravelTo(fleeTarget.Value, ctx.Map))
            {
                herd.TransitionTo(HerdState.Resting);
                return HerdUpdateResult.None;
            }

            // Narrative if player sees them flee
            if (herd.IsPlayerHere)
            {
                return HerdUpdateResult.WithNarrative($"The {herd.AnimalType.DisplayName()} retreats into the distance.");
            }
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

    public double GetVisibilityFactor(Herd herd) => 0.4;  // Slightly more visible than wolves

    private static bool ShouldEngagePlayer(Herd herd, GameContext ctx)
    {
        // Check 30-minute cooldown after recent combat
        int minutesSinceCombat = ctx.TotalMinutesElapsed - herd.LastCombatMinutes;
        if (minutesSinceCombat < 30)
        {
            return false; // Still on cooldown
        }

        double aggression = 0.15;  // Lower base than wolves

        // Starving bear is dangerous
        if (herd.Hunger > 0.8) aggression += 0.3;
        if (herd.Hunger > 0.95) aggression += 0.3;

        // Player vulnerability
        bool isBleeding = ctx.player.EffectRegistry.HasEffect("Bleeding") ||
                          ctx.player.EffectRegistry.GetSeverity("Bloody") > 0.3;
        if (isBleeding) aggression += 0.1;

        double movementCapacity = ctx.player.GetCapacities().Moving;
        if (movementCapacity < 0.5) aggression += 0.15;

        // Territorial near den (first tile in territory)
        if (herd.HomeTerritory.Count > 0 && herd.Position == herd.HomeTerritory[0])
        {
            aggression += 0.2;
        }

        // Apply learned fear (multiplicative - preserves relative relationships)
        if (herd.Fear > 0)
            aggression *= (1.0 - herd.Fear);

        return _rng.NextDouble() < aggression;
    }
}
