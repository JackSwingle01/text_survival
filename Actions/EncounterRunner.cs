using text_survival.Actors.Animals;
using text_survival.Combat;
using text_survival.Environments;
using text_survival.Environments.Grid;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Result of a predator encounter, used by callers to handle cleanup.
/// </summary>
public enum EncounterOutcome
{
    PredatorRetreated,  // Predator fled
    PlayerEscaped,      // Player escaped
    CombatVictory,      // Player killed predator
    PlayerDied          // Player was killed
}

/// <summary>
/// Handles predator encounters by transitioning to the unified stealth-combat system.
/// Player starts Unaware, predator is Alert/Engaged based on context.
/// </summary>
public static class EncounterRunner
{
    /// <summary>
    /// Handles a predator encounter using the combat grid.
    /// Player starts Unaware - combat overlay only appears when player detects predator or is attacked.
    /// </summary>
    public static EncounterOutcome HandlePredatorEncounter(Animal predator, GameContext ctx)
    {
        // Record animal encounter in Discovery Log
        ctx.RecordAnimalEncounter(predator.AnimalType);

        // Initialize boldness from observable context
        predator.EncounterBoldness = predator.CalculateBoldness(ctx.player, ctx.Inventory);

        // Determine predator's initial awareness based on boldness
        // Very bold predators are fully Engaged, others start Alert
        bool predatorIsAlert = predator.EncounterBoldness < 0.7;

        // Run combat with predator encounter setup
        var combatResult = CombatOrchestrator.RunPredatorEncounter(ctx, predator, predatorIsAlert);

        return TranslateCombatResult(combatResult);
    }

    /// <summary>
    /// Translate combat result to encounter outcome.
    /// </summary>
    private static EncounterOutcome TranslateCombatResult(CombatResult combatResult)
    {
        return combatResult switch
        {
            CombatResult.Victory => EncounterOutcome.CombatVictory,
            CombatResult.Defeat => EncounterOutcome.PlayerDied,
            CombatResult.Fled => EncounterOutcome.PlayerEscaped,
            CombatResult.AnimalFled => EncounterOutcome.PredatorRetreated,
            CombatResult.AnimalDisengaged => EncounterOutcome.PredatorRetreated,
            CombatResult.DistractedWithMeat => EncounterOutcome.PlayerEscaped,
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
