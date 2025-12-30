using text_survival.Actions;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Behavior for prey animals (caribou, megaloceros, bison).
/// States: Resting, Grazing, Fleeing.
/// </summary>
public class PreyBehavior : IHerdBehavior
{
    private static readonly Random _rng = new();

    // Hunger rates per minute
    private const double HungerRatePerMinute = 0.003;   // ~5.5 hours to get hungry
    private const double GrazeRatePerMinute = 0.008;    // ~2 hours grazing to satiate

    public HerdUpdateResult Update(Herd herd, int elapsedMinutes, GameContext ctx)
    {
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
                return UpdateResting(herd, elapsedMinutes, ctx);

            case HerdState.Grazing:
                return UpdateGrazing(herd, elapsedMinutes, ctx);

            case HerdState.Fleeing:
                return ExecuteFlee(herd, ctx);

            case HerdState.Alert:
                return UpdateAlert(herd, ctx);

            default:
                // Fall back to resting for unexpected states
                herd.State = HerdState.Resting;
                return HerdUpdateResult.None;
        }
    }

    private static HerdUpdateResult UpdateResting(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Hungry? Start grazing
        if (herd.Hunger > 0.5)
        {
            herd.State = HerdState.Grazing;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateGrazing(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Reduce hunger while grazing
        herd.Hunger = Math.Max(0, herd.Hunger - elapsedMinutes * GrazeRatePerMinute);

        // Graze at current location, depleting resources
        GrazeAtLocation(herd, elapsedMinutes, ctx);

        // Move slowly within territory (faster if area is grazed)
        TryMoveWithinTerritory(herd, elapsedMinutes, ctx);

        // Full? Rest
        if (herd.Hunger < 0.2)
        {
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
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

    private static HerdUpdateResult UpdateAlert(Herd herd, GameContext ctx)
    {
        // Prey in alert state flee immediately
        herd.State = HerdState.Fleeing;
        herd.StateTimeMinutes = 0;
        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult ExecuteFlee(Herd herd, GameContext ctx)
    {
        var fleeTarget = GetFleeTarget(herd, ctx);

        if (fleeTarget != null && fleeTarget != herd.Position)
        {
            var previousPosition = herd.Position;
            herd.Position = fleeTarget.Value;
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;

            // Narrative if player can see
            if (ctx.Map != null && ctx.Map.CurrentPosition == previousPosition)
            {
                string direction = GetCardinalDirection(previousPosition, fleeTarget.Value);
                return HerdUpdateResult.WithNarrative(
                    $"The {herd.AnimalType.DisplayName().ToLower()} herd bolts {direction}.");
            }

            return new HerdUpdateResult { NewPosition = herd.Position };
        }
        else
        {
            // Can't flee - just rest
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    public void TriggerFlee(Herd herd, GridPosition threatSource, GameContext ctx)
    {
        herd.State = HerdState.Fleeing;
        herd.StateTimeMinutes = 0;
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

    private static GridPosition? GetFleeTarget(Herd herd, GameContext ctx)
    {
        if (ctx.Map == null) return null;

        var playerPos = ctx.Map.CurrentPosition;

        // Get passable neighbors and sort by distance from player (furthest first)
        var options = herd.Position.GetCardinalNeighbors()
            .Where(p => ctx.Map.GetLocationAt(p)?.IsPassable ?? false)
            .OrderByDescending(p => p.ManhattanDistance(playerPos))
            .ToList();

        return options.FirstOrDefault();
    }

    private static void TryMoveWithinTerritory(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (herd.HomeTerritory.Count == 0) return;

        // Get grazed level at current location to influence movement
        double grazedLevel = GetGrazedLevelAtLocation(herd, ctx);

        // Base 1% per minute, increases with grazing depletion
        // grazedLevel=0: 1% per minute (~100 min avg), grazedLevel=1: 2% per minute (~50 min avg)
        double moveChancePerMinute = 0.01 + 0.01 * grazedLevel;

        // Calculate probability of moving at least once during elapsed time
        double moveProbability = 1.0 - Math.Pow(1.0 - moveChancePerMinute, elapsedMinutes);

        if (_rng.NextDouble() < moveProbability)
        {
            herd.TerritoryIndex = (herd.TerritoryIndex + 1) % herd.HomeTerritory.Count;
            herd.Position = herd.HomeTerritory[herd.TerritoryIndex];
        }
    }

    private static double GetGrazedLevelAtLocation(Herd herd, GameContext ctx)
    {
        if (ctx.Map == null) return 0;

        var location = ctx.Map.GetLocationAt(herd.Position);
        var forage = location?.Features.OfType<ForageFeature>().FirstOrDefault();

        return forage?.GetGrazingLevelForDiet(herd.Diet) ?? 0;
    }

    private static string GetCardinalDirection(GridPosition from, GridPosition to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        return (dx, dy) switch
        {
            ( > 0, _) => "east",
            ( < 0, _) => "west",
            (_, > 0) => "south",
            (_, < 0) => "north",
            _ => "away"
        };
    }
}
