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

        // Very hungry bear will hunt opportunistically while foraging
        if (herd.Hunger > 0.7)
        {
            var huntResult = TryHuntPrey(herd, ctx);
            if (huntResult != HerdUpdateResult.None)
            {
                return huntResult;
            }
        }

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

        // Hungry bear will hunt while patrolling
        if (herd.Hunger > 0.6)
        {
            var huntResult = TryHuntPrey(herd, ctx);
            if (huntResult != HerdUpdateResult.None)
            {
                return huntResult;
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

        return _rng.NextDouble() < aggression;
    }

    private static HerdUpdateResult TryHuntPrey(Herd bear, GameContext ctx)
    {
        // Check for prey in this tile
        var preyHere = ctx.Herds.GetHerdsAt(bear.Position)
            .FirstOrDefault(h => !h.IsPredator && !h.IsEmpty);

        if (preyHere == null) return HerdUpdateResult.None;

        // Bears are solitary - prefer smaller/weaker prey
        // Skip large healthy herds (bison, large caribou groups)
        if (preyHere.Count > 5 && preyHere.AnimalType != "Megaloceros")
        {
            return HerdUpdateResult.None;
        }

        // Use same resolution as wolves
        var resolution = PredatorPreyResolver.ResolvePredatorPreyEncounter(bear, preyHere);

        if (resolution == PredatorPreyResolver.HuntResolution.PreyEscaped)
        {
            // Prey flees
            if (preyHere.Behavior != null)
            {
                preyHere.Behavior.TriggerFlee(preyHere, bear.Position, ctx);
            }
            else
            {
                preyHere.State = HerdState.Fleeing;
            }

            // Bears don't pursue like wolves - they're ambush predators
            if (ctx.Map != null && ctx.Map.CurrentPosition == bear.Position)
            {
                return HerdUpdateResult.WithNarrative(
                    $"A bear charges at {preyHere.AnimalType.ToLower()}, but they scatter.");
            }

            return HerdUpdateResult.None;
        }

        // Attack initiated - bears have lower success rate solo
        // Apply penalty for solitary hunting
        double catchChance = 0.25; // Base 25% for solitary bear vs wolf pack's higher rate
        if (bear.Hunger > 0.9) catchChance += 0.15; // Desperate bear
        var weakest = preyHere.Members.OrderBy(m => m.Condition).FirstOrDefault();
        if (weakest != null && weakest.Condition < 0.5) catchChance += 0.2; // Injured prey

        if (_rng.NextDouble() < catchChance)
        {
            var victim = preyHere.Members.OrderBy(m => m.SpeedMps * m.Condition)
                .ThenBy(m => m.Condition)
                .FirstOrDefault();

            if (victim != null)
            {
                preyHere.RemoveMember(victim);

                // Create carcass
                if (ctx.Map != null)
                {
                    var location = ctx.Map.GetLocationAt(bear.Position);
                    location?.Features.Add(new CarcassFeature(victim));
                }

                bear.State = HerdState.Feeding;
                bear.Hunger = 0;
                bear.StateTimeMinutes = 0;

                // Remaining prey flees
                if (!preyHere.IsEmpty)
                {
                    if (preyHere.Behavior != null)
                        preyHere.Behavior.TriggerFlee(preyHere, bear.Position, ctx);
                    else
                        preyHere.State = HerdState.Fleeing;
                }

                if (ctx.Map != null && ctx.Map.CurrentPosition == bear.Position)
                {
                    return HerdUpdateResult.WithNarrative(
                        $"A bear brings down a {victim.Name}. It begins feeding.");
                }

                return HerdUpdateResult.WithPreyKill(preyHere, victim, bear.Position);
            }
        }
        else
        {
            // Failed chase
            if (preyHere.Behavior != null)
                preyHere.Behavior.TriggerFlee(preyHere, bear.Position, ctx);
            else
                preyHere.State = HerdState.Fleeing;

            if (ctx.Map != null && ctx.Map.CurrentPosition == bear.Position)
            {
                return HerdUpdateResult.WithNarrative(
                    $"A bear lunges at {preyHere.AnimalType.ToLower()}, but they escape.");
            }
        }

        return HerdUpdateResult.None;
    }

    private static void TryMoveWithinTerritory(Herd herd, int elapsedMinutes, GameContext ctx)
    {
        if (herd.HomeTerritory.Count == 0) return;

        // Get grazed level at current location to influence movement
        double grazedLevel = GetGrazedLevelAtLocation(herd, ctx);

        // Base 1% per minute, increases with grazing depletion
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
}
