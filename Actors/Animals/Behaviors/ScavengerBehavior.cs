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
    private static readonly Random _rng = new();

    private const double HungerRatePerMinute = 0.001;   // Similar to pack predators
    private const int FeedingDurationMinutes = 45;      // Hyenas feed quickly
    private const int CarcassSearchRadius = 2;          // Tiles to scan for carcasses
    private const int PredatorFollowRadius = 3;         // Tiles to detect feeding predators

    public HerdUpdateResult Update(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        herd.StateTimeMinutes += elapsedMinutes;
        herd.Hunger = Math.Clamp(herd.Hunger + elapsedMinutes * HungerRatePerMinute, 0, 1);

        switch (herd.State)
        {
            case HerdState.Resting:
                return UpdateResting(herd, elapsedMinutes, ctx);

            case HerdState.Patrolling:
                return UpdatePatrolling(herd, elapsedMinutes, ctx);

            case HerdState.Feeding:
                return UpdateFeeding(herd, elapsedMinutes, ctx);

            case HerdState.Alert:
                return UpdateAlert(herd, ctx);

            case HerdState.Fleeing:
                return UpdateFleeing(herd, elapsedMinutes, ctx);

            default:
                herd.State = HerdState.Patrolling;
                return HerdUpdateResult.None;
        }
    }

    private static HerdUpdateResult UpdateResting(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Hungry? Start patrolling for food
        if (herd.Hunger > 0.4)
        {
            herd.State = HerdState.Patrolling;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private HerdUpdateResult UpdatePatrolling(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (ctx.Map == null) return HerdUpdateResult.None;

        // Priority 1: Check for carcass at current location
        var carcassHere = GetCarcassAtPosition(herd.Position, ctx);
        if (carcassHere != null && carcassHere.GetTotalRemainingKg() > 0)
        {
            herd.State = HerdState.Feeding;
            herd.StateTimeMinutes = 0;
            return HerdUpdateResult.None;
        }

        // Priority 2: Move toward nearest carcass
        var nearestCarcass = FindNearestCarcass(herd.Position, ctx);
        if (nearestCarcass != null)
        {
            MoveToward(herd, nearestCarcass.Value, ctx);
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
                herd.Position = oneAway.Value;
            }
            return HerdUpdateResult.None;
        }

        // Priority 4: Shadow wolf packs (they'll find food)
        var wolfPack = FindWolfPack(herd, ctx);
        if (wolfPack != null)
        {
            // Move toward wolf territory edge
            MoveTowardTerritory(herd, wolfPack, ctx);
            return HerdUpdateResult.None;
        }

        // Default: Wander within territory
        TryMoveWithinTerritory(herd, elapsedMinutes, ctx);

        // Check for player - hyenas are cowardly but opportunistic
        if (herd.Position == ctx.Map.CurrentPosition)
        {
            if (ShouldEngagePlayer(herd, ctx))
            {
                return HerdUpdateResult.WithEncounter(herd);
            }
        }

        // Rest if not hungry
        if (herd.Hunger < 0.3 && herd.StateTimeMinutes > 60)
        {
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private HerdUpdateResult UpdateFeeding(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (ctx.Map == null) return HerdUpdateResult.None;

        // Check for larger predator at our location - flee!
        var threatHere = ctx.Herds.GetHerdsAt(herd.Position)
            .FirstOrDefault(h => h.Id != herd.Id && IsLargerPredator(h));

        if (threatHere != null)
        {
            // Flee from the larger predator
            TriggerFlee(herd, threatHere.Position, ctx);
            return HerdUpdateResult.WithNarrative(
                $"Hyenas scatter as a {threatHere.AnimalType.DisplayName().ToLower()} approaches.");
        }

        // Consume carcass
        var carcass = GetCarcassAtPosition(herd.Position, ctx);
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
            herd.State = HerdState.Patrolling;
            herd.StateTimeMinutes = 0;
            return HerdUpdateResult.None;
        }

        // Defend carcass if player enters (but less aggressively than wolves)
        if (herd.Position == ctx.Map.CurrentPosition)
        {
            if (ShouldEngagePlayer(herd, ctx, isDefendingCarcass: true))
            {
                return HerdUpdateResult.WithEncounter(herd, isDefending: true);
            }
        }

        // Done feeding
        if (herd.StateTimeMinutes > FeedingDurationMinutes || herd.Hunger < 0.1)
        {
            herd.State = HerdState.Patrolling;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateAlert(Herd herd, GameContext ctx)
    {
        // Hyenas are cautious - longer alert time
        if (herd.StateTimeMinutes > 5)
        {
            // Usually just flee rather than engage
            if (ctx.Map != null && herd.Position == ctx.Map.CurrentPosition)
            {
                if (_rng.NextDouble() < 0.3)  // Only 30% chance to engage
                {
                    return HerdUpdateResult.WithEncounter(herd);
                }
            }

            herd.State = HerdState.Patrolling;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateFleeing(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Flee for a bit then go back to patrolling
        if (herd.StateTimeMinutes > 10)
        {
            herd.State = HerdState.Patrolling;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    public void TriggerFlee(Herd herd, GridPosition threatSource, GameContext ctx)
    {
        herd.State = HerdState.Fleeing;
        herd.StateTimeMinutes = 0;

        // Move away from threat
        if (herd.HomeTerritory.Count > 0)
        {
            // Find territory tile furthest from threat
            var safest = herd.HomeTerritory
                .OrderByDescending(p => p.ManhattanDistance(threatSource))
                .First();
            herd.Position = safest;
        }
    }

    public double GetVisibilityFactor(Herd herd) => 0.5;  // Noisy scavengers, somewhat visible

    #region Carcass Detection

    private static CarcassFeature? GetCarcassAtPosition(GridPosition pos, GameContext ctx)
    {
        var location = ctx.Map?.GetLocationAt(pos);
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
                var carcass = GetCarcassAtPosition(pos, ctx);

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
        return ctx.Herds.GetPredatorHerds()
            .Where(h => h.State == HerdState.Feeding)
            .Where(h => h.Position.ManhattanDistance(from) <= PredatorFollowRadius)
            .OrderBy(h => h.Position.ManhattanDistance(from))
            .FirstOrDefault();
    }

    private static Herd? FindWolfPack(Herd herd, GameContext ctx)
    {
        return ctx.Herds.GetHerdsByType(AnimalType.Wolf)
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

    private static void MoveToward(Herd herd, GridPosition target, GameContext ctx)
    {
        int dx = Math.Sign(target.X - herd.Position.X);
        int dy = Math.Sign(target.Y - herd.Position.Y);

        // Prefer direction with greater distance
        GridPosition? newPos = null;
        if (Math.Abs(target.X - herd.Position.X) >= Math.Abs(target.Y - herd.Position.Y) && dx != 0)
        {
            newPos = new GridPosition(herd.Position.X + dx, herd.Position.Y);
        }
        else if (dy != 0)
        {
            newPos = new GridPosition(herd.Position.X, herd.Position.Y + dy);
        }
        else if (dx != 0)
        {
            newPos = new GridPosition(herd.Position.X + dx, herd.Position.Y);
        }

        if (newPos != null && ctx.Map?.GetLocationAt(newPos.Value)?.IsPassable == true)
        {
            herd.Position = newPos.Value;
        }
    }

    private static GridPosition? GetTileNear(GridPosition from, GridPosition target, GameContext ctx)
    {
        // Get a tile adjacent to target but not on target
        var candidates = target.GetCardinalNeighbors()
            .Where(p => ctx.Map?.GetLocationAt(p)?.IsPassable == true)
            .OrderBy(p => p.ManhattanDistance(from))
            .ToList();

        return candidates.FirstOrDefault();
    }

    private static void MoveTowardTerritory(Herd herd, Herd target, GameContext ctx)
    {
        if (target.HomeTerritory.Count == 0) return;

        // Move toward nearest territory tile
        var nearest = target.HomeTerritory
            .Where(p => !target.HomeTerritory.Contains(herd.Position))  // Don't enter their territory
            .OrderBy(p => p.ManhattanDistance(herd.Position))
            .FirstOrDefault();

        if (nearest != default)
        {
            MoveToward(herd, nearest, ctx);
        }
    }

    private static void TryMoveWithinTerritory(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (herd.HomeTerritory.Count == 0) return;

        // Hyenas move frequently while patrolling (faster than wolves)
        double moveChancePerMinute = 0.04;  // ~4% per minute
        double moveProbability = 1.0 - Math.Pow(1.0 - moveChancePerMinute, elapsedMinutes);

        if (_rng.NextDouble() < moveProbability)
        {
            herd.TerritoryIndex = (herd.TerritoryIndex + 1) % herd.HomeTerritory.Count;
            herd.Position = herd.HomeTerritory[herd.TerritoryIndex];
        }
    }

    #endregion

    #region Player Engagement

    private static bool ShouldEngagePlayer(Herd herd, GameContext ctx, bool isDefendingCarcass = false)
    {
        double boldness = CalculateBoldness(herd, ctx, isDefendingCarcass);
        return _rng.NextDouble() < boldness;
    }

    private static double CalculateBoldness(Herd herd, GameContext ctx, bool isDefendingCarcass)
    {
        double bold = 0.1;  // Very low base - hyenas are cowards

        // Pack size matters a lot for hyenas
        bold += herd.Count * 0.08;

        // Vulnerability checks
        double vulnerability = GetPlayerVulnerability(ctx);
        bold += vulnerability * 0.25;

        // Starving hyenas are bolder
        if (herd.Hunger > 0.8) bold += 0.2;

        // Defending food
        if (isDefendingCarcass) bold += 0.15;

        // Apply learned fear (multiplicative - preserves relative relationships)
        if (herd.PlayerFear > 0)
            bold *= (1.0 - herd.PlayerFear);

        // Cap at 0.6 - hyenas won't engage confidently like wolves
        return Math.Clamp(bold, 0, 0.6);
    }

    private static double GetPlayerVulnerability(GameContext ctx)
    {
        double vuln = 0;

        bool isBleeding = ctx.player.EffectRegistry.HasEffect("Bleeding") ||
                          ctx.player.EffectRegistry.GetSeverity("Bloody") > 0.3;
        if (isBleeding) vuln += 0.3;

        double movementCapacity = ctx.player.GetCapacities().Moving;
        if (movementCapacity < 0.7) vuln += 0.2;
        if (movementCapacity < 0.5) vuln += 0.3;

        bool carryingMeat = ctx.Inventory.Count(Resource.RawMeat) > 0 ||
                            ctx.Inventory.Count(Resource.CookedMeat) > 0;
        if (carryingMeat) vuln += 0.2;

        return Math.Clamp(vuln, 0, 1);
    }

    #endregion
}
