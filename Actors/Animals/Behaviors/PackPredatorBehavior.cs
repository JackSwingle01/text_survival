using text_survival.Combat;
using text_survival.Actions;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;

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
                return UpdateResting(herd);

            case HerdState.Patrolling:
                return UpdatePatrolling(herd, elapsedMinutes, ctx);

            case HerdState.Hunting:
                return UpdateHunting(herd);

            case HerdState.Feeding:
                return UpdateFeeding(herd, ctx);

            case HerdState.Alert:
                return UpdateAlert(herd, ctx);

            case HerdState.Fleeing:
                return UpdateFleeing(herd, ctx);

            default:
                herd.TransitionTo(HerdState.Resting);
                return HerdUpdateResult.None;
        }
    }

    private static HerdUpdateResult UpdateResting(Herd herd)
    {
        // Hungry? Start patrolling
        if (herd.Hunger > 0.6)
        {
            herd.TransitionTo(HerdState.Patrolling);
        }

        return HerdUpdateResult.None;
    }

    private HerdUpdateResult UpdatePatrolling(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        // Move to next territory tile periodically (skip if already traveling)
        if (!herd.IsTraveling && herd.StateTimeMinutes > 0 && herd.StateTimeMinutes % 30 == 0)
        {
            var nextTile = GetNextPatrolTarget(herd, ctx);
            if (nextTile != null && nextTile != herd.Position && ctx.Map != null)
            {
                herd.StartTravelTo(nextTile.Value, ctx.Map);
                herd.TerritoryIndex = herd.HomeTerritory.IndexOf(nextTile.Value);
                if (herd.TerritoryIndex < 0) herd.TerritoryIndex = 0;
            }
        }

        // Check for player in this tile
        if (herd.IsPlayerHere)
        {
            if (ShouldEngagePlayer(herd, ctx))
            {
                herd.TransitionTo(HerdState.Hunting);
                return HerdUpdateResult.WithEncounter(herd);
            }
        }

        // Check for NPC allies at this location (using unified actor tracking)
        var npcsHere = ctx.GetActorsAt(herd.Position)
            .OfType<NPC>()
            .Where(n => n.IsAlive)
            .ToList();

        foreach (var npc in npcsHere)
        {
            if (Random.Shared.NextDouble() >= herd.BoldnessToward(npc, ctx))
                continue;

            var predator = herd.Members[0];  // Lead predator

            // The target and any NPC here who would stand with them
            var defenders = new List<Actor> { npc };
            defenders.AddRange(npcsHere.Where(other => other != npc && other.WouldDefend(npc, predator)));
            foreach (var defender in defenders.OfType<NPC>())
                defender.SetCombatCooldown(5);  // Prevent double-detection

            var result = CombatOrchestrator.ResolveHeadless(
                ctx, defenders, CombatOrchestrator.AnimalSide(ctx, predator), npc.CurrentLocation,
                startDistanceM: 5, AwarenessState.Engaged, AwarenessState.Engaged);
            Console.WriteLine($"[Predator] {herd.AnimalType.DisplayName()} pack vs {npc.Name}: {result}");

            // Only one attack per update
            break;
        }

        // Check for prey herds in this tile
        var preyHere = ctx.Herds.At(herd.Position)
            .FirstOrDefault(h => !h.IsPredator && !h.IsEmpty);

        if (preyHere != null && herd.Hunger > 0.4)
        {
            // Attempt to hunt NPC prey
            return AttemptNpcHunt(herd, preyHere, ctx);
        }

        // Patrol timeout - rest
        if (herd.StateTimeMinutes > PatrolTimeoutMinutes)
        {
            herd.TransitionTo(HerdState.Resting);
            ReturnToHome(herd, ctx);
        }

        // Low hunger - rest
        if (herd.Hunger < 0.3)
        {
            herd.TransitionTo(HerdState.Resting);
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
                if (distance <= 4 && ShouldEngagePlayer(herd, ctx))
                {
                    herd.TransitionTo(HerdState.Hunting);
                    return HerdUpdateResult.WithEncounter(herd);
                }
            }

            // Not engaging - return to patrol
            herd.TransitionTo(HerdState.Patrolling);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateHunting(Herd herd)
    {
        // Player is in the same tile - encounter already triggered
        // This state is transitional
        herd.TransitionTo(HerdState.Patrolling);
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
                return HerdUpdateResult.WithNarrative($"The {herd.AnimalType.DisplayName()} pack retreats into the distance.");
            }
        }
        else
        {
            herd.TransitionTo(HerdState.Resting);
        }

        return HerdUpdateResult.None;
    }

    private static HerdUpdateResult UpdateFeeding(Herd herd, GameContext ctx)
    {
        // Defend kill if player enters
        if (herd.IsPlayerHere)
        {
            // Always encounter when defending a kill
            return HerdUpdateResult.WithEncounter(herd, isDefending: true);
        }

        if (herd.StateTimeMinutes > FeedingDurationMinutes)
        {
            herd.TransitionTo(HerdState.Resting);
        }

        return HerdUpdateResult.None;
    }

    private HerdUpdateResult AttemptNpcHunt(Herd predator, Herd prey, GameContext ctx)
    {
        // Skip hunting if fearful (just lost a fight)
        if (predator.Fear > 0.5)
        {
            return HerdUpdateResult.None;
        }

        // The pack goes for the weakest few; the rest of the herd scatters afterward.
        var targets = prey.Members
            .Where(m => m.IsAlive)
            .OrderBy(m => m.SpeedMps * m.Condition)
            .ThenBy(m => m.Condition)
            .Take(3)
            .ToList<Actor>();
        var pack = predator.Members.Where(m => m.IsAlive).Take(6).ToList<Actor>();
        if (targets.Count == 0 || pack.Count == 0)
            return HerdUpdateResult.None;

        // Vigilance decides how the fight opens: noticed prey start alert at range, surprised prey are ambushed.
        bool noticed = HerdVigilance.PreyNoticesPredator(predator, prey);
        var result = CombatOrchestrator.ResolveHeadless(
            ctx, pack, targets, predator.CurrentLocation,
            startDistanceM: noticed ? 15 : 5,
            AwarenessState.Engaged,
            noticed ? AwarenessState.Alert : AwarenessState.Unaware);

        bool killed = targets.Any(t => !t.IsAlive);
        if (!killed)
        {
            // Chase failed; hungry wolves may pursue
            if (predator.State == HerdState.Hunting)
                predator.State = HerdState.Patrolling;
            if (predator.Hunger > 0.8 && _rng.NextDouble() < 0.4 && ctx.Map != null && !prey.IsEmpty)
                predator.StartTravelTo(prey.Position, ctx.Map);
        }

        if (!predator.IsPlayerHere)
            return HerdUpdateResult.None;

        string preyName = prey.AnimalType.DisplayName().ToLower();
        return HerdUpdateResult.WithNarrative(killed
            ? $"Wolves bring down a {preyName}. They begin feeding."
            : $"Wolves chase {preyName}, but they escape.");
    }

    public void TriggerFlee(Herd herd, GridPosition threatSource, GameContext ctx)
    {
        herd.TransitionTo(HerdState.Fleeing);
    }

    public double GetVisibilityFactor(Herd herd) => 0.3;  // Predators stay hidden

    private static bool ShouldEngagePlayer(Herd herd, GameContext ctx)
    {
        // Check 30-minute cooldown after recent combat
        int minutesSinceCombat = ctx.TotalMinutesElapsed - herd.LastCombatMinutes;
        if (minutesSinceCombat < 30)
        {
            return false; // Still on cooldown
        }

        return _rng.NextDouble() < herd.BoldnessToward(ctx.player, ctx);
    }

    private GridPosition? GetNextPatrolTarget(Herd herd, GameContext ctx)
    {
        // Hungry predators bias toward player tile if signals are strong
        if (herd.Hunger > 0.5 && ctx.Map != null)
        {
            var playerPos = ctx.Map.CurrentPosition;
            int playerDistance = herd.Position.ManhattanDistance(playerPos);

            if (playerDistance <= 8 && herd.HomeTerritory.Contains(playerPos))
            {
                double pullStrength = 0;

                bool isBleeding = ctx.player.EffectRegistry.HasEffect("Bleeding") ||
                                  ctx.player.EffectRegistry.GetSeverity("Bloody") > 0.3;
                if (isBleeding) pullStrength += 0.4;

                bool carryingMeat = ctx.Inventory.Count(Resource.RawMeat) > 0 ||
                                    ctx.Inventory.Count(Resource.CookedMeat) > 0;
                if (carryingMeat) pullStrength += 0.3;

                if (_rng.NextDouble() < pullStrength)
                {
                    // Move one tile toward player
                    int dx = Math.Sign(playerPos.X - herd.Position.X);
                    int dy = Math.Sign(playerPos.Y - herd.Position.Y);

                    var candidates = new List<GridPosition>();
                    if (dx != 0) candidates.Add(new GridPosition(herd.Position.X + dx, herd.Position.Y));
                    if (dy != 0) candidates.Add(new GridPosition(herd.Position.X, herd.Position.Y + dy));

                    return candidates.FirstOrDefault(p => ctx.Map.GetLocationAt(p)?.IsPassable ?? false);
                }
            }
        }

        // Normal patrol
        if (herd.HomeTerritory.Count == 0) return null;
        return herd.HomeTerritory[(herd.TerritoryIndex + 1) % herd.HomeTerritory.Count];
    }

    private static void ReturnToHome(Herd herd, GameContext ctx)
    {
        if (herd.HomeTerritory.Count > 0 && ctx.Map != null)
        {
            herd.StartTravelTo(herd.HomeTerritory[0], ctx.Map);
            herd.TerritoryIndex = 0;
        }
    }
}
