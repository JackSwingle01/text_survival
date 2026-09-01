namespace text_survival.Actions;

/// <summary>
/// The one place that decides how fast game time flows on screen.
/// </summary>
public static class Pacing
{
    public const float SecondsPerMinute = 0.3f;

    /// <summary>How long a progress bar covering <paramref name="minutes"/> should take.</summary>
    public static float ProgressSeconds(int minutes) => Math.Clamp(minutes * SecondsPerMinute, 1f, 30f);

    /// <summary>How long the walk to the next tile should take on screen.</summary>
    public static float TravelSeconds(int minutes) => Math.Clamp(0.5f + minutes * 0.03f, 0.5f, 1.2f);
}
