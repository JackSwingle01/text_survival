using text_survival.Actions;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;

namespace text_survival.Actors.Animals.Behaviors;

/// <summary>
/// Behavior for pack predators (wolves).
/// States: Resting, Patrolling, Hunting, Feeding.
/// </summary>
public class PackPredatorBehavior : IHerdBehavior
{
    private static readonly Random _rng = new();

    private const double HungerRatePerMinute = 0.001;  // Slower metabolism than prey
    private const int PatrolTimeoutMinutes = 120;
    private const int FeedingDurationMinutes = 60;

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

            case HerdState.Hunting:
                return UpdateHunting(herd, ctx);

            case HerdState.Feeding:
                return UpdateFeeding(herd, elapsedMinutes, ctx);

            case HerdState.Alert:
                // Predators in alert state decide to hunt or not
                return UpdateAlert(herd, ctx);

            default:
                herd.State = HerdState.Resting;
                return HerdUpdateResult.None;
        }
    }

    private static HerdUpdateResult UpdateResting(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Hungry? Start patrolling
        if (herd.Hunger > 0.6)
        {
            herd.State = HerdState.Patrolling;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private HerdUpdateResult UpdatePatrolling(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Move to next territory tile periodically
        if (herd.StateTimeMinutes > 0 && herd.StateTimeMinutes % 30 == 0)
        {
            var nextTile = GetNextPatrolTarget(herd, ctx);
            if (nextTile != null && nextTile != herd.Position)
            {
                herd.Position = nextTile.Value;
                herd.TerritoryIndex = herd.HomeTerritory.IndexOf(nextTile.Value);
                if (herd.TerritoryIndex < 0) herd.TerritoryIndex = 0;
            }
        }

        // Check for player in this tile
        if (ctx.Map != null && herd.Position == ctx.Map.CurrentPosition)
        {
            if (ShouldEngagePlayer(herd, ctx))
            {
                herd.State = HerdState.Hunting;
                herd.StateTimeMinutes = 0;
                return HerdUpdateResult.WithEncounter(herd);
            }
        }

        // Check for prey herds in this tile
        var preyHere = ctx.Herds.GetHerdsAt(herd.Position)
            .FirstOrDefault(h => !h.IsPredator && !h.IsEmpty);

        if (preyHere != null && herd.Hunger > 0.4)
        {
            // Attempt to hunt NPC prey
            return AttemptNpcHunt(herd, preyHere, ctx);
        }

        // Patrol timeout - rest
        if (herd.StateTimeMinutes > PatrolTimeoutMinutes)
        {
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
            ReturnToHome(herd);
        }

        // Low hunger - rest
        if (herd.Hunger < 0.3)
        {
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateAlert(Herd herd, GameContext ctx)
    {
        // Wait a moment then decide
        if (herd.StateTimeMinutes > 3)
        {
            // Check player distance
            if (ctx.Map != null)
            {
                int distance = herd.Position.ManhattanDistance(ctx.Map.CurrentPosition);
                if (distance <= 1 && ShouldEngagePlayer(herd, ctx))
                {
                    herd.State = HerdState.Hunting;
                    herd.StateTimeMinutes = 0;
                    return HerdUpdateResult.WithEncounter(herd);
                }
            }

            // Not engaging - return to patrol
            herd.State = HerdState.Patrolling;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateHunting(Herd herd, GameContext ctx)
    {
        // Player is in the same tile - encounter already triggered
        // This state is transitional
        herd.State = HerdState.Patrolling;
        herd.StateTimeMinutes = 0;
        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateFeeding(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Defend kill if player enters
        if (ctx.Map != null && herd.Position == ctx.Map.CurrentPosition)
        {
            // Always encounter when defending a kill
            return HerdUpdateResult.WithEncounter(herd, isDefending: true);
        }

        if (herd.StateTimeMinutes > FeedingDurationMinutes)
        {
            herd.State = HerdState.Resting;
            herd.StateTimeMinutes = 0;
        }

        return HerdUpdateResult.None;
    }

    private HerdUpdateResult AttemptNpcHunt(Herd predator, Herd prey, GameContext ctx)
    {
        // Predator-prey resolution
        var resolution = PredatorPreyResolver.ResolvePredatorPreyEncounter(predator, prey);

        if (resolution == PredatorPreyResolver.HuntResolution.PreyEscaped)
        {
            // Prey flees
            if (prey.Behavior != null)
            {
                prey.Behavior.TriggerFlee(prey, predator.Position, ctx);
            }
            else
            {
                prey.State = HerdState.Fleeing;
            }

            // Hungry wolves may pursue
            if (predator.Hunger > 0.8 && _rng.NextDouble() < 0.4)
            {
                predator.Position = prey.Position;
                // Will re-encounter next tick
            }

            // Narrative if player present
            if (ctx.Map != null && ctx.Map.CurrentPosition == predator.Position)
            {
                return HerdUpdateResult.WithNarrative(
                    $"Wolves chase {prey.AnimalType.ToLower()}, but they escape.");
            }

            return HerdUpdateResult.None;
        }

        // Attack initiated - resolve kill attempt
        if (PredatorPreyResolver.AttemptPreyKill(predator, prey))
        {
            var victim = prey.Members.OrderBy(m => m.Body.GetSpeed())
                .ThenBy(m => m.Condition)
                .FirstOrDefault();

            if (victim != null)
            {
                prey.RemoveMember(victim);

                // Create carcass at this location
                if (ctx.Map != null)
                {
                    var location = ctx.Map.GetLocationAt(predator.Position);
                    location?.Features.Add(new CarcassFeature(victim));
                }

                predator.State = HerdState.Feeding;
                predator.Hunger = 0;
                predator.StateTimeMinutes = 0;

                // Remaining prey flees
                if (!prey.IsEmpty)
                {
                    if (prey.Behavior != null)
                        prey.Behavior.TriggerFlee(prey, predator.Position, ctx);
                    else
                        prey.State = HerdState.Fleeing;
                }

                // Narrative if player present
                if (ctx.Map != null && ctx.Map.CurrentPosition == predator.Position)
                {
                    return HerdUpdateResult.WithNarrative(
                        $"Wolves bring down a {victim.Name}. They begin feeding.");
                }

                return HerdUpdateResult.WithPreyKill(prey, victim, predator.Position);
            }
        }
        else
        {
            // Chase failed
            if (prey.Behavior != null)
                prey.Behavior.TriggerFlee(prey, predator.Position, ctx);
            else
                prey.State = HerdState.Fleeing;

            predator.State = HerdState.Patrolling;

            if (ctx.Map != null && ctx.Map.CurrentPosition == predator.Position)
            {
                return HerdUpdateResult.WithNarrative(
                    $"Wolves chase {prey.AnimalType.ToLower()}, but the herd escapes.");
            }
        }

        return HerdUpdateResult.None;
    }

    public void TriggerFlee(Herd herd, GridPosition threatSource, GameContext ctx)
    {
        // Predators don't flee - they disengage
        herd.State = HerdState.Patrolling;
        herd.StateTimeMinutes = 0;
    }

    public double GetVisibilityFactor(Herd herd) => 0.3;  // Predators stay hidden

    private static bool ShouldEngagePlayer(Herd herd, GameContext ctx)
    {
        double boldness = CalculateBoldness(herd, ctx);
        return _rng.NextDouble() < boldness;
    }

    private static double CalculateBoldness(Herd herd, GameContext ctx)
    {
        double bold = 0.2;

        // Pack size
        bold += herd.Count * 0.05;

        // Hunger
        if (herd.Hunger > 0.7) bold += 0.2;
        if (herd.Hunger > 0.9) bold += 0.2;

        // Player vulnerability
        bool isBleeding = ctx.player.EffectRegistry.HasEffect("Bleeding") ||
                          ctx.player.EffectRegistry.GetSeverity("Bloody") > 0.3;
        if (isBleeding) bold += 0.15;

        bool carryingMeat = ctx.Inventory.Count(Items.Resource.RawMeat) > 0 ||
                            ctx.Inventory.Count(Items.Resource.CookedMeat) > 0;
        if (carryingMeat) bold += 0.1;

        double movementCapacity = ctx.player.Body.GetCapacities().Moving;
        if (movementCapacity < 0.5) bold += 0.2;

        // Night time
        bool isNight = ctx.GetTimeOfDay() == GameContext.TimeOfDay.Night;
        if (isNight) bold += 0.1;

        return Math.Clamp(bold, 0, 0.9);
    }

    private GridPosition? GetNextPatrolTarget(Herd herd, GameContext ctx)
    {
        // Hungry predators bias toward player tile if signals are strong
        if (herd.Hunger > 0.5 && ctx.Map != null)
        {
            var playerPos = ctx.Map.CurrentPosition;
            int playerDistance = herd.Position.ManhattanDistance(playerPos);

            if (playerDistance <= 2 && herd.HomeTerritory.Contains(playerPos))
            {
                double pullStrength = 0;

                bool isBleeding = ctx.player.EffectRegistry.HasEffect("Bleeding") ||
                                  ctx.player.EffectRegistry.GetSeverity("Bloody") > 0.3;
                if (isBleeding) pullStrength += 0.4;

                bool carryingMeat = ctx.Inventory.Count(Items.Resource.RawMeat) > 0 ||
                                    ctx.Inventory.Count(Items.Resource.CookedMeat) > 0;
                if (carryingMeat) pullStrength += 0.3;

                if (_rng.NextDouble() < pullStrength)
                {
                    return GetTileToward(herd.Position, playerPos, ctx);
                }
            }
        }

        // Normal patrol
        if (herd.HomeTerritory.Count == 0) return null;
        return herd.HomeTerritory[(herd.TerritoryIndex + 1) % herd.HomeTerritory.Count];
    }

    private static GridPosition? GetTileToward(GridPosition from, GridPosition to, GameContext ctx)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);

        var candidates = new List<GridPosition>();
        if (dx != 0) candidates.Add(new GridPosition(from.X + dx, from.Y));
        if (dy != 0) candidates.Add(new GridPosition(from.X, from.Y + dy));

        return candidates.FirstOrDefault(p => ctx.Map?.IsPassable(p) ?? false);
    }

    private static void ReturnToHome(Herd herd)
    {
        if (herd.HomeTerritory.Count > 0)
        {
            herd.Position = herd.HomeTerritory[0];
            herd.TerritoryIndex = 0;
        }
    }
}
