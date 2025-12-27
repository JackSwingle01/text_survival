using text_survival;

namespace text_survival.Environments;

/// <summary>
/// Represents a multi-state weather system that unfolds over time.
/// Example: Storm Front progresses Building → Peak → Trailing → Clearing
/// </summary>
public class WeatherFront
{
    public FrontType Type { get; init; }
    public List<WeatherState> States { get; init; } = [];
    public int CurrentStateIndex { get; set; } = 0;

    public WeatherState CurrentState => States[CurrentStateIndex];

    public WeatherState? NextState =>
        CurrentStateIndex + 1 < States.Count ? States[CurrentStateIndex + 1] : null;

    public bool IsComplete => CurrentStateIndex >= States.Count - 1;

    /// <summary>
    /// Total time remaining until this front completes (sum of remaining state durations)
    /// </summary>
    public TimeSpan TimeRemaining =>
        States.Skip(CurrentStateIndex).Aggregate(TimeSpan.Zero, (acc, s) => acc + s.Duration);
}

/// <summary>
/// Types of weather fronts with approximate frequencies
/// </summary>
public enum FrontType
{
    ClearSpell,      // Multi-day good weather (40% frequency)
    StormSystem,     // Building → peak → clearing pattern (30% frequency)
    ColdSnap,        // Progressive temperature drop (15% frequency)
    Warming,         // Thaw cycle (10% frequency)
    UnsettledPeriod  // Oscillating conditions (5% frequency)
}

/// <summary>
/// A single weather state within a front.
/// Conditions roll within the specified ranges for variability.
/// </summary>
public class WeatherState
{
    public Weather.WeatherCondition Condition { get; init; }
    public (double Min, double Max) TempRange { get; init; }
    public (double Min, double Max) WindRange { get; init; }
    public (double Min, double Max) PrecipRange { get; init; }
    public (double Min, double Max) CloudRange { get; init; }
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Probability this state will be skipped (0.0 = never skip, 0.2 = 20% chance to skip)
    /// Adds randomness to front progression
    /// </summary>
    public double SkipProbability { get; init; } = 0.0;
}
