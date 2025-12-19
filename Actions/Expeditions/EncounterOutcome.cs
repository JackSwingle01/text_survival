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
