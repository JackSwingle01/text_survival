namespace text_survival;

/// <summary>
/// Tracks continuous value transitions across discrete stage boundaries.
/// Returns a StageChange only when a threshold is crossed, ensuring events fire once per transition.
/// </summary>
/// <typeparam name="TStage">Enum representing the discrete stages</typeparam>
public class StageTracker<TStage> where TStage : struct, Enum
{
    private TStage? _lastNotifiedStage;
    private readonly Func<double, TStage> _stageMapper;

    public StageTracker(Func<double, TStage> stageMapper)
    {
        _stageMapper = stageMapper;
    }

    /// <summary>
    /// Update the tracker with a new value. Returns a StageChange if a threshold was crossed.
    /// </summary>
    /// <param name="value">The current value (typically 0-1 for percentages)</param>
    /// <returns>StageChange if threshold crossed, null otherwise</returns>
    public StageChange<TStage>? Update(double value)
    {
        var newStage = _stageMapper(value);

        if (_lastNotifiedStage == null)
        {
            _lastNotifiedStage = newStage;
            return new StageChange<TStage>(null, newStage);
        }

        if (!newStage.Equals(_lastNotifiedStage))
        {
            var old = _lastNotifiedStage;
            _lastNotifiedStage = newStage;
            return new StageChange<TStage>(old, newStage);
        }

        return null;
    }

    /// <summary>
    /// Reset the tracker to its initial state. Next Update will return an initial stage change.
    /// </summary>
    public void Reset() => _lastNotifiedStage = null;

    /// <summary>
    /// Get the current stage without triggering a change notification.
    /// </summary>
    public TStage? CurrentStage => _lastNotifiedStage;
}

/// <summary>
/// Represents a stage transition from one discrete state to another.
/// </summary>
/// <typeparam name="TStage">The stage enum type</typeparam>
public record StageChange<TStage>(TStage? Previous, TStage Current) where TStage : struct, Enum
{
    /// <summary>
    /// True if this is the initial stage assignment (no previous stage).
    /// </summary>
    public bool IsInitial => Previous == null;

    /// <summary>
    /// True if the transition was to a more severe/critical stage (higher enum value).
    /// </summary>
    public bool IsEscalation => Previous != null &&
        Convert.ToInt32(Current) > Convert.ToInt32(Previous);

    /// <summary>
    /// True if the transition was to a less severe stage (lower enum value).
    /// </summary>
    public bool IsDeescalation => Previous != null &&
        Convert.ToInt32(Current) < Convert.ToInt32(Previous);
}
