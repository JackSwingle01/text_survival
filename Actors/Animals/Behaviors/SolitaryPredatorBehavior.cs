using text_survival.Actions;
using text_survival.Environments.Features;
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
                return UpdateResting(herd, elapsedMinutes, ctx);

            case HerdState.Grazing:  // Bears use Grazing state for foraging
                return UpdateForaging(herd, elapsedMinutes, ctx);

            case HerdState.Patrolling:
                return UpdatePatrolling(herd, elapsedMinutes, ctx);

            case HerdState.Feeding:
                return UpdateFeeding(herd, elapsedMinutes, ctx);

            case HerdState.Alert:
                return UpdateAlert(herd, ctx);

            case HerdState.Hunting:
                // Bears don't have a prolonged hunting state - they charge
                herd.State = HerdState.Patrolling;
                return HerdUpdateResult.None;

            default:
                herd.State = HerdState.Resting;
                return HerdUpdateResult.None;
        }
    }

    private static HerdUpdateResult UpdateResting(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Hungry? Start foraging
        if (herd.Hunger > 0.5)
        {
            herd.State = HerdState.Grazing;  // Grazing = foraging for bears
            herd.StateTimeMinutes = 0;
        }

        // Defend den if player enters
        if (ctx.Map != null && herd.Position == herd.HomeTerritory.FirstOrDefault() &&
            herd.Position == ctx.Map.CurrentPosition)
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
        GrazeAtLocation(herd, elapsedMinutes, ctx);

        // Move within territory while foraging (faster if area is grazed)
        TryMoveWithinTerritory(herd, elapsedMinutes, ctx);

        // Sated? Rest
        if (herd.Hunger < 0.3)
        {
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
        }

        // Check for player in tile
        if (ctx.Map != null && herd.Position == ctx.Map.CurrentPosition)
        {
            if (ShouldEngagePlayer(herd, ctx))
            {
                return HerdUpdateResult.WithEncounter(herd);
            }
        }

        return HerdUpdateResult.None;
    }

    private static void GrazeAtLocation(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (ctx.Map == null) return;

        var location = ctx.Map.GetLocationAt(herd.Position);
        var forage = location?.Features.OfType<ForageFeature>().FirstOrDefault();

        forage?.Graze(herd.Diet, herd.TotalMassKg, elapsedMinutes);
    }

    private static HerdUpdateResult UpdatePatrolling(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Bears patrol less than wolves - mostly territory check
        TryMoveWithinTerritory(herd, elapsedMinutes, ctx);

        // Check for player in tile
        if (ctx.Map != null && herd.Position == ctx.Map.CurrentPosition)
        {
            if (ShouldEngagePlayer(herd, ctx))
            {
                return HerdUpdateResult.WithEncounter(herd);
            }
        }

        // Short patrol then rest
        if (herd.StateTimeMinutes > 60)
        {
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateAlert(Herd herd, GameContext ctx)
    {
        // Bears decide quickly
        if (herd.StateTimeMinutes > 2)
        {
            if (ctx.Map != null && herd.Position == ctx.Map.CurrentPosition)
            {
                if (ShouldEngagePlayer(herd, ctx))
                {
                    return HerdUpdateResult.WithEncounter(herd);
                }
            }

            // Not engaging - resume previous activity
            herd.State = HerdState.Patrolling;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateFeeding(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Defend kill if player enters
        if (ctx.Map != null && herd.Position == ctx.Map.CurrentPosition)
        {
            return HerdUpdateResult.WithEncounter(herd, isDefending: true);
        }

        // Bears feed longer than wolves
        if (herd.StateTimeMinutes > FeedingDurationMinutes)
        {
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    public void TriggerFlee(Herd herd, GridPosition threatSource, GameContext ctx)
    {
        // Bears don't flee - they disengage
        herd.State = HerdState.Patrolling;
        herd.StateTimeMinutes = 0;
    }

    public double GetVisibilityFactor(Herd herd) => 0.4;  // Slightly more visible than wolves

    private static bool ShouldEngagePlayer(Herd herd, GameContext ctx)
    {
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
        if (herd.PlayerFear > 0)
            aggression *= (1.0 - herd.PlayerFear);

        return _rng.NextDouble() < aggression;
    }

    private static void TryMoveWithinTerritory(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (herd.HomeTerritory.Count == 0 || herd.IsTraveling || ctx.Map == null) return;

        // Get grazed level at current location to influence movement
        double grazedLevel = GetGrazedLevelAtLocation(herd, ctx);

        // Base 1% per minute, increases with grazing depletion
        double moveChancePerMinute = 0.01 + 0.01 * grazedLevel;

        // Calculate probability of moving at least once during elapsed time
        double moveProbability = 1.0 - Math.Pow(1.0 - moveChancePerMinute, elapsedMinutes);

        if (_rng.NextDouble() < moveProbability)
        {
            herd.TerritoryIndex = (herd.TerritoryIndex + 1) % herd.HomeTerritory.Count;
            herd.StartTravelTo(herd.HomeTerritory[herd.TerritoryIndex], ctx.Map);
        }
    }

    private static double GetGrazedLevelAtLocation(Herd herd, GameContext ctx)
    {
        if (ctx.Map == null) return 0;

        var location = ctx.Map.GetLocationAt(herd.Position);
        var forage = location?.Features.OfType<ForageFeature>().FirstOrDefault();

        return forage?.GetGrazingLevelForDiet(herd.Diet) ?? 0;
    }
}
