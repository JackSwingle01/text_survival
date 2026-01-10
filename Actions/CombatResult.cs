namespace text_survival.Actions;

/// <summary>
/// Result of a combat encounter.
/// </summary>
public enum CombatResult
{
    Victory,           // Player/allies killed all enemies
    Defeat,            // Player killed
    Fled,              // Player escaped
    AnimalFled,        // Enemy fled
    AnimalDisengaged,  // Enemy disengaged
    DistractedWithMeat // Enemy took meat bait
}
