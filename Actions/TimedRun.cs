namespace text_survival.Actions;

/// <summary>
/// One clock for anything that both animates and simulates. Real seconds go in; whole
/// game minutes come out. Animation progress and simulated minutes are two readings of
/// the same elapsed time, so they finish together by construction.
/// </summary>
public sealed class TimedRun
{
    private readonly int _totalMinutes;
    private readonly float _durationSeconds;
    private float _accumulatedMinutes;

    public TimedRun(int totalMinutes, float durationSeconds)
    {
        if (totalMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(totalMinutes), totalMinutes, "A run cannot last negative minutes.");
        if (durationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "A run needs a positive duration.");

        _totalMinutes = totalMinutes;
        _durationSeconds = durationSeconds;
    }

    public int TotalMinutes => _totalMinutes;
    public float ElapsedSeconds { get; private set; }
    public int SimulatedMinutes { get; private set; }

    /// <summary>Animation progress, 0-1.</summary>
    public float Progress => Math.Min(ElapsedSeconds / _durationSeconds, 1f);

    public bool Done => SimulatedMinutes >= _totalMinutes;

    /// <summary>
    /// Advance the clock by one frame and report how many whole minutes are now due.
    /// The fractional remainder is kept, so no time is lost to truncation and no frame
    /// is forced to simulate a minute it hasn't earned.
    /// </summary>
    public int Advance(float dtSeconds)
    {
        ElapsedSeconds += dtSeconds;

        float minutesPerSecond = _totalMinutes / _durationSeconds;
        _accumulatedMinutes += dtSeconds * minutesPerSecond;

        int due = (int)_accumulatedMinutes;
        if (due <= 0) return 0;

        _accumulatedMinutes -= due;
        return Math.Min(due, _totalMinutes - SimulatedMinutes);
    }

    public void MarkSimulated(int minutes) => SimulatedMinutes += minutes;
}
