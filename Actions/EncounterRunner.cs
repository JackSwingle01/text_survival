using text_survival.Actors.Animals;
using text_survival.Web;
using text_survival.Web.Dto;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Result of a predator encounter, used by callers to handle cleanup.
/// </summary>
public enum EncounterOutcome
{
    PredatorRetreated,  // Boldness dropped below 0.3, predator left
    PlayerEscaped,      // Player outran or distracted predator
    CombatVictory,      // Player killed predator
    PlayerDied          // Player was killed
}

/// <summary>
/// Handles predator encounters using the overlay UI.
///
/// State Machine:
/// 1. Choice Phase: Player selects action (stand/back/run/fight/drop_meat)
/// 2. Process: Apply effects of choice, update distance/boldness
/// 3. Check: If boldness &lt; retreat threshold → Retreated
///           If distance &lt;= attack range → Combat
///           Else → Continue to next turn
/// 4. Outcome Phase: Show result message, wait for acknowledgment
///
/// Transitions to CombatRunner when fight initiated or predator charges.
/// </summary>
public static class EncounterRunner
{
    // Distance constants (meters)
    private const double StandGroundDistanceCloseMeters = 10;
    private const double BackAwayDistanceGainMeters = 5;
    private const double AggressiveCloseDistanceMeters = 10;
    private const double AttackRangeMeters = 5;
    private const double FightOptionRangeMeters = 20;
    private const double CaughtDistanceMeters = 5;

    // Boldness constants (0-1 scale)
    private const double StandGroundBoldnessDecrease = 0.10;
    private const double BackAwayBoldnessIncrease = 0.05;
    private const double BoldnessRetreatThreshold = 0.3;
    private const double BoldnessAggressiveThreshold = 0.7;

    // Other thresholds
    private const double PlayerWeaknessVitalityThreshold = 0.7;

    /// <summary>
    /// Handles a predator encounter using the overlay UI. Returns outcome for caller.
    /// </summary>
    public static EncounterOutcome HandlePredatorEncounter(Animal predator, GameContext ctx)
    {
        // Initialize boldness from observable context
        predator.EncounterBoldness = predator.CalculateBoldness(ctx.player, ctx.Inventory);

        double? prevDistance = null;
        string? statusMessage = null;

        while (predator.IsAlive && ctx.player.IsAlive)
        {
            // Build encounter DTO
            var encounterDto = BuildEncounterDto(ctx, predator, prevDistance, statusMessage);

            // Get player choice
            string action = WebIO.WaitForEncounterChoice(ctx, encounterDto);

            // Save distance for next animation
            prevDistance = predator.DistanceFromPlayer;

            switch (action)
            {
                case "stand":
                    predator.DistanceFromPlayer -= StandGroundDistanceCloseMeters;
                    predator.EncounterBoldness -= StandGroundBoldnessDecrease;

                    if (predator.EncounterBoldness < BoldnessRetreatThreshold)
                    {
                        // Show retreat outcome
                        var retreatDto = BuildEncounterDto(ctx, predator, prevDistance, null,
                            new EncounterOutcomeDto("retreated", $"The {predator.Name} hesitates... then slinks away."));
                        WebIO.RenderEncounter(ctx, retreatDto);
                        WebIO.WaitForEncounterContinue(ctx);
                        return EncounterOutcome.PredatorRetreated;
                    }
                    statusMessage = $"The {predator.Name} moves closer, but seems less certain.";
                    break;

                case "back":
                    predator.DistanceFromPlayer += BackAwayDistanceGainMeters;
                    predator.EncounterBoldness += BackAwayBoldnessIncrease;
                    statusMessage = "You slowly back away, keeping eyes on the predator.";
                    break;

                case "run":
                    var (escaped, narrative) = HuntingCalculator.CalculatePursuitOutcome(
                        ctx.player, predator, predator.DistanceFromPlayer);

                    if (escaped)
                    {
                        var escapeDto = BuildEncounterDto(ctx, predator, prevDistance, null,
                            new EncounterOutcomeDto("escaped", narrative));
                        WebIO.RenderEncounter(ctx, escapeDto);
                        WebIO.WaitForEncounterContinue(ctx);
                        return EncounterOutcome.PlayerEscaped;
                    }

                    // Caught — forced combat
                    predator.DistanceFromPlayer = CaughtDistanceMeters;
                    var caughtDto = BuildEncounterDto(ctx, predator, prevDistance, null,
                        new EncounterOutcomeDto("fight", narrative + " It catches you!"));
                    WebIO.RenderEncounter(ctx, caughtDto);
                    WebIO.WaitForEncounterContinue(ctx);
                    WebIO.ClearEncounter(ctx);

                    return TransitionToCombat(ctx, predator);

                case "fight":
                    var fightDto = BuildEncounterDto(ctx, predator, prevDistance, null,
                        new EncounterOutcomeDto("fight", "You ready yourself for combat."));
                    WebIO.RenderEncounter(ctx, fightDto);
                    WebIO.WaitForEncounterContinue(ctx);
                    WebIO.ClearEncounter(ctx);

                    return TransitionToCombat(ctx, predator);

                case "drop_meat":
                    double meatDropped = ctx.Inventory.DropAllMeat();
                    var meatDto = BuildEncounterDto(ctx, predator, prevDistance, null,
                        new EncounterOutcomeDto("escaped",
                            $"You drop {meatDropped:F1}kg of meat and back away. The {predator.Name} goes for the meat."));
                    WebIO.RenderEncounter(ctx, meatDto);
                    WebIO.WaitForEncounterContinue(ctx);
                    return EncounterOutcome.PlayerEscaped;

                default:
                    statusMessage = null;
                    break;
            }

            // Boldness ceiling: very bold predator closes regardless of player action
            if (predator.EncounterBoldness >= BoldnessAggressiveThreshold)
            {
                predator.DistanceFromPlayer -= AggressiveCloseDistanceMeters;
                statusMessage = $"The {predator.Name} grows impatient and closes in.";
            }

            // Check if predator reaches attack range
            if (predator.DistanceFromPlayer <= AttackRangeMeters)
            {
                var chargeDto = BuildEncounterDto(ctx, predator, prevDistance, null,
                    new EncounterOutcomeDto("fight", $"The {predator.Name} charges!"));
                WebIO.RenderEncounter(ctx, chargeDto);
                WebIO.WaitForEncounterContinue(ctx);
                WebIO.ClearEncounter(ctx);

                return TransitionToCombat(ctx, predator);
            }

            ctx.Update(1, ActivityType.Encounter); // 1 minute per turn
        }

        WebIO.ClearEncounter(ctx);
        return ctx.player.IsAlive ? EncounterOutcome.PredatorRetreated : EncounterOutcome.PlayerDied;
    }

    /// <summary>
    /// Build EncounterDto for UI rendering.
    /// </summary>
    private static EncounterDto BuildEncounterDto(
        GameContext ctx,
        Animal predator,
        double? prevDistance,
        string? statusMessage,
        EncounterOutcomeDto? outcome = null)
    {
        string boldnessDesc = predator.EncounterBoldness >= BoldnessAggressiveThreshold ? "aggressive"
            : predator.EncounterBoldness > BoldnessRetreatThreshold ? "wary" : "hesitant";

        // Build threat factors
        var factors = new List<ThreatFactorDto>();
        if (ctx.Inventory.HasMeat)
            factors.Add(new ThreatFactorDto("meat", "Eyeing your meat", "restaurant"));
        if (ctx.player.Vitality < PlayerWeaknessVitalityThreshold)
            factors.Add(new ThreatFactorDto("weakness", "Senses your weakness", "personal_injury"));
        if (ctx.player.EffectRegistry.HasEffect("Bloody"))
            factors.Add(new ThreatFactorDto("blood", "Smells blood on you", "water_drop"));

        // Build choices
        var choices = new List<EncounterChoiceDto>
        {
            new("stand", "Stand your ground", "Face it down. May intimidate it.", true, null),
            new("back", "Back away slowly", "Gain distance but it may grow bolder.", true, null),
            new("run", "Run", "Try to outrun it. Risky if slow.", true, null)
        };

        if (predator.DistanceFromPlayer <= FightOptionRangeMeters)
            choices.Add(new EncounterChoiceDto("fight", "Fight", "Attack now.", true, null));

        if (ctx.Inventory.HasMeat)
            choices.Add(new EncounterChoiceDto("drop_meat", "Drop the meat", "Distract it and escape.", true, null));

        return new EncounterDto(
            PredatorName: predator.Name,
            CurrentDistanceMeters: predator.DistanceFromPlayer,
            PreviousDistanceMeters: prevDistance,
            IsAnimatingDistance: prevDistance != null,
            BoldnessLevel: predator.EncounterBoldness,
            BoldnessDescriptor: boldnessDesc,
            ThreatFactors: factors,
            StatusMessage: statusMessage,
            Choices: choices,
            Outcome: outcome
        );
    }

    /// <summary>
    /// Transition from encounter overlay to combat overlay.
    /// </summary>
    private static EncounterOutcome TransitionToCombat(GameContext ctx, Animal predator)
    {
        var combatResult = CombatRunner.RunCombat(ctx, predator);

        return combatResult switch
        {
            CombatResult.Victory => EncounterOutcome.CombatVictory,
            CombatResult.Defeat => EncounterOutcome.PlayerDied,
            CombatResult.Fled => EncounterOutcome.PlayerEscaped,
            _ => EncounterOutcome.PlayerDied
        };
    }

    /// <summary>
    /// Creates an animal from an EncounterConfig, setting up initial distance and boldness.
    /// Logs a warning and returns null for unknown animal types.
    /// </summary>
    public static Animal? CreateAnimalFromConfig(EncounterConfig config, Location location, GameMap map)
    {
        var animal = AnimalFactory.FromType(config.AnimalType, location, map);

        if (animal == null)
        {
            Console.WriteLine($"[EncounterRunner] WARNING: Unknown animal type '{config.AnimalType}', encounter skipped");
            return null;
        }

        animal.DistanceFromPlayer = config.InitialDistance;
        animal.EncounterBoldness = config.InitialBoldness;
        return animal;
    }
}
