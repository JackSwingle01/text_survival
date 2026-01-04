using text_survival.Actors;

namespace text_survival.Combat;

/// <summary>
/// Unified result type for combat engagements (both interactive and automatic).
/// Provides a common return type for CombatRunner.StartCombat() that can represent
/// any combat outcome regardless of resolution mode.
/// </summary>
public class CombatResult
{
    public ResultType Type { get; init; }
    public List<Actor> Survivors { get; init; } = [];
    public List<Actor> Casualties { get; init; } = [];
    public string NarrativeSummary { get; init; } = "";

    /// <summary>
    /// For interactive mode - converts from existing CombatOutcome enum.
    /// </summary>
    public static CombatResult FromInteractive(CombatOutcome outcome, Actor player, Actor? enemy, string summary)
    {
        return outcome switch
        {
            CombatOutcome.Victory => new CombatResult
            {
                Type = ResultType.Victory,
                Survivors = [player],
                Casualties = enemy != null ? new List<Actor> { enemy } : new List<Actor>(),
                NarrativeSummary = summary
            },
            CombatOutcome.PlayerDied => new CombatResult
            {
                Type = ResultType.Defeat,
                Casualties = [player],
                Survivors = enemy != null ? new List<Actor> { enemy } : new List<Actor>(),
                NarrativeSummary = summary
            },
            CombatOutcome.PlayerDisengaged => new CombatResult
            {
                Type = ResultType.Escaped,
                Survivors = enemy != null ? new List<Actor> { player, enemy } : new List<Actor> { player },
                NarrativeSummary = summary
            },
            CombatOutcome.AnimalFled => new CombatResult
            {
                Type = ResultType.EnemyFled,
                Survivors = enemy != null ? new List<Actor> { player, enemy } : new List<Actor> { player },
                NarrativeSummary = summary
            },
            CombatOutcome.AnimalDisengaged => new CombatResult
            {
                Type = ResultType.Incapacitated,
                Survivors = enemy != null ? new List<Actor> { player, enemy } : new List<Actor> { player },
                NarrativeSummary = summary
            },
            CombatOutcome.DistractedWithMeat => new CombatResult
            {
                Type = ResultType.Distracted,
                Survivors = enemy != null ? new List<Actor> { player, enemy } : new List<Actor> { player },
                NarrativeSummary = summary
            },
            _ => new CombatResult
            {
                Type = ResultType.Unknown,
                NarrativeSummary = summary
            }
        };
    }

    /// <summary>
    /// For automatic mode - creates result directly.
    /// </summary>
    public static CombatResult FromAutomatic(
        ResultType type,
        List<Actor> survivors,
        List<Actor> casualties,
        string summary)
    {
        return new CombatResult
        {
            Type = type,
            Survivors = survivors,
            Casualties = casualties,
            NarrativeSummary = summary
        };
    }
}

/// <summary>
/// Unified combat result types across all resolution modes.
/// </summary>
public enum ResultType
{
    Victory,         // Clear winner with casualties
    Defeat,          // Player/attacker killed
    Escaped,         // Actor(s) successfully fled
    EnemyFled,       // Opponent fled
    Incapacitated,   // Player unable to continue, enemy left
    Distracted,      // Combat ended via distraction (meat drop)
    Injured,         // Both sides wounded but alive
    Repelled,        // Attacker driven off
    Unknown          // Fallback
}
