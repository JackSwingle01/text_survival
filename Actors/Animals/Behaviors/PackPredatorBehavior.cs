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
            // Calculate boldness toward this NPC (similar to player)
            double boldness = CalculateBoldnessTowardNPC(herd, npc, ctx);

            if (Random.Shared.NextDouble() < boldness)
            {
                // Set combat cooldown on the target to prevent double-detection
                npc.SetCombatCooldown(5);

                var predator = herd.Members[0];  // Lead predator

                // Check for NPC allies who might join the fight
                var nearbyAllies = npcsHere
                    .Where(other => other != npc
                        && other.IsAlive
                        && other.WouldDefend(npc, predator))
                    .ToList();

                // Set cooldown on all allies to prevent double-detection
                foreach (var ally in nearbyAllies)
                    ally.SetCombatCooldown(5);

                // Attack the primary target first
                var outcome = ActorCombatResolver.ResolveCombat(
                    new List<Actor> { predator, npc },
                    npc.CurrentLocation
                );

                // Allies join the fight if predator survives the initial combat
                if (predator.IsAlive && nearbyAllies.Count > 0)
                {
                    foreach (var ally in nearbyAllies.Where(a => a.IsAlive))
                    {
                        outcome = ActorCombatResolver.ResolveCombat(
                            new List<Actor> { predator, ally },
                            npc.CurrentLocation
                        );

                        if (!predator.IsAlive) break;  // Stop if predator dies
                    }

                    // Record alliance in relationship memory
                    var allDefenders = new List<Actor> { npc };
                    allDefenders.AddRange(nearbyAllies.Where(a => a.IsAlive));
                    RelationshipEvents.FoughtTogether(allDefenders);
                }

                HandleNPCCombatOutcome(herd, npc, outcome, ctx);

                // Only one attack per update
                break;
            }
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
                prey.TransitionTo(HerdState.Fleeing);
            }

            // Hungry wolves may pursue
            if (predator.Hunger > 0.8 && _rng.NextDouble() < 0.4 && ctx.Map != null)
            {
                predator.StartTravelTo(prey.Position, ctx.Map);
            }

            // Narrative if player present
            if (predator.IsPlayerHere)
            {
                return HerdUpdateResult.WithNarrative(
                    $"Wolves chase {prey.AnimalType.DisplayName().ToLower()}, but they escape.");
            }

            return HerdUpdateResult.None;
        }

        // Attack initiated - resolve kill attempt
        if (PredatorPreyResolver.AttemptPreyKill(predator, prey))
        {
            var victim = prey.Members.OrderBy(m => m.SpeedMps * m.Condition)
                .ThenBy(m => m.Condition)
                .FirstOrDefault();

            if (victim != null)
            {
                prey.RemoveMember(victim);

                // Create carcass at this location
                predator.CurrentLocation?.Features.Add(new CarcassFeature(victim));

                predator.TransitionTo(HerdState.Feeding);
                predator.Hunger = 0;

                // Remaining prey flees
                if (!prey.IsEmpty)
                {
                    if (prey.Behavior != null)
                        prey.Behavior.TriggerFlee(prey, predator.Position, ctx);
                    else
                        prey.TransitionTo(HerdState.Fleeing);
                }

                // Narrative if player present
                if (predator.IsPlayerHere)
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
                prey.TransitionTo(HerdState.Fleeing);

            predator.State = HerdState.Patrolling;

            if (predator.IsPlayerHere)
            {
                return HerdUpdateResult.WithNarrative(
                    $"Wolves chase {prey.AnimalType.DisplayName().ToLower()}, but the herd escapes.");
            }
        }

        return HerdUpdateResult.None;
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

        bool carryingMeat = ctx.Inventory.Count(Resource.RawMeat) > 0 ||
                            ctx.Inventory.Count(Resource.CookedMeat) > 0;
        if (carryingMeat) bold += 0.1;

        double movementCapacity = ctx.player.GetCapacities().Moving;
        if (movementCapacity < 0.5) bold += 0.2;

        // Night time
        bool isNight = ctx.GetTimeOfDay() == GameContext.TimeOfDay.Night;
        if (isNight) bold += 0.1;

        // Apply learned fear (multiplicative - preserves relative relationships)
        if (herd.Fear > 0)
            bold *= (1.0 - herd.Fear);

        return Math.Clamp(bold, 0, 0.9);
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

    private static double CalculateBoldnessTowardNPC(Herd herd, NPC npc, GameContext ctx)
    {
        double bold = 0.3;  // Base boldness

        // Pack size
        bold += herd.Count * 0.05;

        // Hunger
        if (herd.Hunger > 0.7) bold += 0.2;
        if (herd.Hunger > 0.9) bold += 0.2;

        // NPC condition
        if (npc.EffectRegistry.HasEffect("Bleeding"))
            bold += 0.15;

        var capacities = npc.GetCapacities();
        if (capacities.Moving < 0.5)
            bold += 0.2;

        // NPC carrying meat
        if (npc.Inventory.Count(Resource.RawMeat) > 0)
            bold += 0.1;

        return Math.Clamp(bold, 0, 1);
    }

    private void HandleNPCCombatOutcome(
        Herd herd, NPC npc, ActorCombatResolver.CombatOutcome outcome, GameContext ctx)
    {
        switch (outcome)
        {
            case ActorCombatResolver.CombatOutcome.DefenderEscaped:
                Console.WriteLine($"[Predator] {npc.Name} escaped from {herd.AnimalType.DisplayName()}");
                break;

            case ActorCombatResolver.CombatOutcome.DefenderInjured:
                Console.WriteLine($"[Predator] {npc.Name} was mauled by {herd.AnimalType.DisplayName()}");
                herd.Hunger = Math.Max(0, herd.Hunger - 0.3);
                break;

            case ActorCombatResolver.CombatOutcome.DefenderKilled:
                Console.WriteLine($"[Predator] {npc.Name} was killed by {herd.AnimalType.DisplayName()}");
                herd.Hunger = 0;
                break;

            case ActorCombatResolver.CombatOutcome.AttackerRepelled:
                Console.WriteLine($"[Predator] {npc.Name} fought off {herd.AnimalType.DisplayName()}");

                // Check if predator died and remove from herd
                var predator = herd.Members.FirstOrDefault();
                if (predator != null && !predator.IsAlive)
                {
                    herd.RemoveMember(predator);
                }

                // Predator learns fear and flees
                herd.Fear = Math.Min(0.9, herd.Fear + 0.3);
                herd.LastCombatMinutes = ctx.TotalMinutesElapsed;
                TriggerFlee(herd, herd.Position, ctx);
                break;
        }
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
