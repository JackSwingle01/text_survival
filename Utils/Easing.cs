namespace text_survival;

/// <summary>
/// Easing curves for animation. One definition, used everywhere something interpolates.
/// </summary>
public static class Easing
{
    /// <summary>
    /// Fast at first, settling at the end. Keeps a moving sprite or camera from stopping dead.
    /// </summary>
    public static float OutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
}
