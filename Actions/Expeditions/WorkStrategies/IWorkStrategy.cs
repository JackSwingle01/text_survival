using text_survival.Actions;
using text_survival.Environments;
using text_survival.IO;

namespace text_survival.Actions.Expeditions.WorkStrategies;

/// <summary>
/// Strategy interface for different work types at locations.
/// Each strategy defines validation, time options, and execution for a specific work activity.
/// </summary>
public interface IWorkStrategy
{
    /// <summary>
    /// Check if this work is available at the given location.
    /// Returns null if available, or an error message if not.
    /// </summary>
    string? ValidateLocation(GameContext ctx, Location location);

    /// <summary>
    /// Get time options for this work type.
    /// Returns null if work should proceed without prompting (e.g., salvage, cache).
    /// </summary>
    Choice<int>? GetTimeOptions(GameContext ctx, Location location);

    /// <summary>
    /// Apply time impairments based on player capacities.
    /// Returns (adjustedTime, warnings).
    /// </summary>
    (int adjustedTime, List<string> warnings) ApplyImpairments(GameContext ctx, Location location, int baseTime);

    /// <summary>
    /// Get the activity type for survival calculations during this work.
    /// </summary>
    ActivityType GetActivityType();

    /// <summary>
    /// Get the activity name for UI display (e.g., "foraging", "exploring").
    /// </summary>
    string GetActivityName();

    /// <summary>
    /// Whether this work type can proceed in darkness without a light source.
    /// If true, work can proceed but the strategy may apply penalties in Execute().
    /// </summary>
    bool AllowedInDarkness { get; }

    /// <summary>
    /// Execute the work after time has elapsed.
    /// Returns the work result (collected items, discovered locations).
    /// </summary>
    WorkResult Execute(GameContext ctx, Location location, int actualTime);
}
