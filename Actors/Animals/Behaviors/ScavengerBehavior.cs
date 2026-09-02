using text_survival.Actions;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;

namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Behavior for scavengers (hyenas).
/// States: Resting, Patrolling, Feeding.
/// Hyenas search for carcasses, follow predator kills, and avoid direct conflict.
/// </summary>
public class ScavengerBehavior : IHerdBehavior
{

    private const double HungerRatePerMinute = 0.001;   // Similar to pack predators
    private const int FeedingDurationMinutes = 45;      // Hyenas feed quickly
    private const int CarcassSearchRadius = 8;          // Tiles to scan for carcasses (~800m)
    private const int PredatorFollowRadius = 12;        // Tiles to detect feeding predators (~1200m)

    public HerdUpdateResult Update(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Tick travel progress first
        if (herd.IsTraveling)
        {
            bool arrived = herd.UpdateTravel(elapsedMinutes);
            if (!arrived) return HerdUpdateResult.None; // Still traveling, skip behavior

            // Just arrived - if fleeing, transition to patrolling
            if (herd.State == HerdState.Fleeing)
            {
                herd.TransitionTo(HerdState.Patrolling);
                return HerdUpdateResult.None;
            }
        }

        herd.StateTimeMinutes += elapsedMinutes;
        herd.Hunger = Math.Clamp(herd.Hunger + elapsedMinutes * HungerRatePerMinute, 0, 1);

        switch (herd.State)
        {
            case HerdState.Resting:
                return UpdateResting(herd);

            case HerdState.Patrolling:
                return UpdatePatrolling(herd, elapsedMinutes, ctx);

            case HerdState.Feeding:
                return UpdateFeeding(herd, elapsedMinutes, ctx);

            case HerdState.Alert:
                return UpdateAlert(herd, ctx);

            case HerdState.Fleeing:
                return UpdateFleeing(herd);

            default:
                herd.TransitionTo(HerdState.Patrolling);
                return HerdUpdateResult.None;
        }
    }

    private static HerdUpdateResult UpdateResting(Herd herd)
    {
        // Hungry? Start patrolling for food
        if (herd.Hunger > 0.4)
        {
            herd.TransitionTo(HerdState.Patrolling);
        }

        return HerdUpdateResult.None;
    }

    private HerdUpdateResult UpdatePatrolling(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (ctx.Map == null) return HerdUpdateResult.None;

        // Priority 1: Check for carcass at current location
        var carcassHere = GetCarcassAtPosition(herd.CurrentLocation);
        if (carcassHere != null && carcassHere.GetTotalRemainingKg() > 0)
        {
            herd.TransitionTo(HerdState.Feeding);
            return HerdUpdateResult.None;
        }

        // Priority 2: Move toward nearest carcass
        var nearestCarcass = FindNearestCarcass(herd.Position, ctx);
        if (nearestCarcass != null)
        {
            herd.MoveToward(nearestCarcass.Value);
            return HerdUpdateResult.None;
        }

        // Priority 3: Follow feeding predators (they have food)
        var feedingPredator = FindFeedingPredator(herd.Position, ctx);
        if (feedingPredator != null)
        {
            // Stay one tile away from feeding predator
            var oneAway = GetTileNear(herd.Position, feedingPredator.Position, ctx);
            if (oneAway != null && oneAway != herd.Position)
            {
                herd.StartTravelTo(oneAway.Value, ctx.Map);
            }
            return HerdUpdateResult.None;
        }

        // Priority 4: Shadow wolf packs (they'll find food)
        var wolfPack = FindWolfPack(herd, ctx);
        if (wolfPack != null)
        {
            // Move toward wolf territory edge
            MoveTowardTerritory(herd, wolfPack);
            return HerdUpdateResult.None;
        }

        // Default: Wander within territory
        // Hyenas move frequently while patrolling (faster than wolves)
        herd.TryPatrolTerritory(elapsedMinutes, 0.04);

        // Check for player - hyenas are cowardly but opportunistic
        if (herd.IsPlayerHere)
        {
            if (ShouldEngagePlayer(herd, ctx))
            {
                return HerdUpdateResult.WithEncounter(herd);
            }
        }

        // Rest if not hungry
        if (herd.Hunger < 0.3 && herd.StateTimeMinutes > 60)
        {
            herd.TransitionTo(HerdState.Resting);
        }

        return HerdUpdateResult.None;
    }

    private HerdUpdateResult UpdateFeeding(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (ctx.Map == null) return HerdUpdateResult.None;

        // Check for larger predator at our location - flee!
        var threatHere = ctx.Herds.At(herd.Position)
            .FirstOrDefault(h => h != herd && IsLargerPredator(h));

        if (threatHere != null)
        {
            // Flee from the larger predator
            TriggerFlee(herd, threatHere.Position, ctx);
            return HerdUpdateResult.WithNarrative(
                $"Hyenas scatter as a {threatHere.AnimalType.DisplayName().ToLower()} approaches.");
        }

        // Consume carcass
        var carcass = GetCarcassAtPosition(herd.CurrentLocation);
        if (carcass != null && carcass.GetTotalRemainingKg() > 0)
        {
            // Consume proportional to pack size and time
            double consumeRate = herd.Count * 0.5;  // kg per minute per hyena
            double consumed = Math.Min(consumeRate * elapsedMinutes, carcass.MeatRemainingKg);
            carcass.MeatRemainingKg -= consumed;
            herd.Hunger = Math.Max(0, herd.Hunger - consumed * 0.05);
        }
        else
        {
            // Carcass gone, back to patrol
            herd.TransitionTo(HerdState.Patrolling);
            return HerdUpdateResult.None;
        }

        // Defend carcass if player enters (but less aggressively than wolves)
        if (herd.IsPlayerHere)
        {
            if (ShouldEngagePlayer(herd, ctx, isDefendingCarcass: true))
            {
                return HerdUpdateResult.WithEncounter(herd, isDefending: true);
            }
        }

        // Done feeding
        if (herd.StateTimeMinutes > FeedingDurationMinutes || herd.Hunger < 0.1)
        {
            herd.TransitionTo(HerdState.Patrolling);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateAlert(Herd herd, GameContext ctx)
    {
        // Hyenas are cautious - longer alert time
        if (herd.StateTimeMinutes > 5)
        {
            // Usually just flee rather than engage
            if (herd.IsPlayerHere && ShouldEngagePlayer(herd, ctx))
            {
                return HerdUpdateResult.WithEncounter(herd);
            }

            herd.TransitionTo(HerdState.Patrolling);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateFleeing(Herd herd)
    {
        // Flee for a bit then go back to patrolling
        if (herd.StateTimeMinutes > 10)
        {
            herd.TransitionTo(HerdState.Patrolling);
        }

        return HerdUpdateResult.None;
    }

    public void TriggerFlee(Herd herd, GridPosition threatSource, GameContext ctx)
    {
        herd.State = HerdState.Fleeing;
        herd.StateTimeMinutes = 0;

        // Move away from threat
        if (herd.HomeTerritory.Count > 0 && ctx.Map != null)
        {
            // Find territory tile furthest from threat
            var safest = herd.HomeTerritory
                .OrderByDescending(p => p.ManhattanDistance(threatSource))
                .First();
            herd.StartTravelTo(safest, ctx.Map);
        }
    }

    public double GetVisibilityFactor(Herd herd) => 0.5;  // Noisy scavengers, somewhat visible

    #region Carcass Detection

    private static CarcassFeature? GetCarcassAtPosition(Environments.Location? location)
    {
        return location?.Features.OfType<CarcassFeature>().FirstOrDefault();
    }

    private static GridPosition? FindNearestCarcass(GridPosition from, GameContext ctx)
    {
        if (ctx.Map == null) return null;

        GridPosition? nearest = null;
        int nearestDist = int.MaxValue;

        for (int dx = -CarcassSearchRadius; dx <= CarcassSearchRadius; dx++)
        {
            for (int dy = -CarcassSearchRadius; dy <= CarcassSearchRadius; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                var pos = new GridPosition(from.X + dx, from.Y + dy);
                var location = ctx.Map.GetLocationAt(pos);
                var carcass = GetCarcassAtPosition(location);

                if (carcass != null && carcass.GetTotalRemainingKg() > 0)
                {
                    int dist = from.ManhattanDistance(pos);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = pos;
                    }
                }
            }
        }

        return nearest;
    }

    #endregion

    #region Predator Following

    private static Herd? FindFeedingPredator(GridPosition from, GameContext ctx)
    {
        return ctx.Herds.Predators()
            .Where(h => h.State == HerdState.Feeding)
            .Where(h => h.Position.ManhattanDistance(from) <= PredatorFollowRadius)
            .OrderBy(h => h.Position.ManhattanDistance(from))
            .FirstOrDefault();
    }

    private static Herd? FindWolfPack(Herd herd, GameContext ctx)
    {
        return ctx.Herds.OfAnimalType(AnimalType.Wolf)
            .Where(h => h.State == HerdState.Patrolling)
            .OrderBy(h => h.Position.ManhattanDistance(herd.Position))
            .FirstOrDefault();
    }

    private static bool IsLargerPredator(Herd h)
    {
        return h.AnimalType is AnimalType.Wolf or AnimalType.Bear or AnimalType.CaveBear or AnimalType.SaberTooth;
    }

    #endregion

    #region Movement

    private static GridPosition? GetTileNear(GridPosition from, GridPosition target, GameContext ctx)
    {
        // Get a tile adjacent to target but not on target
        var candidates = target.GetCardinalNeighbors()
            .Where(p => ctx.Map?.GetLocationAt(p)?.IsPassable == true)
            .OrderBy(p => p.ManhattanDistance(from))
            .ToList();

        return candidates.FirstOrDefault();
    }

    private static void MoveTowardTerritory(Herd herd, Herd target)
    {
        if (target.HomeTerritory.Count == 0) return;

        // Move toward nearest territory tile
        var nearest = target.HomeTerritory
            .Where(p => !target.HomeTerritory.Contains(herd.Position))  // Don't enter their territory
            .OrderBy(p => p.ManhattanDistance(herd.Position))
            .FirstOrDefault();

        if (nearest != default)
        {
            herd.MoveToward(nearest);
        }
    }

    #endregion

    #region Player Engagement

    private static bool ShouldEngagePlayer(Herd herd, GameContext ctx, bool isDefendingCarcass = false)
    {
        return Utils.Rng.NextDouble() < herd.BoldnessToward(ctx.player, ctx, defending: isDefendingCarcass);
    }

    #endregion
}
