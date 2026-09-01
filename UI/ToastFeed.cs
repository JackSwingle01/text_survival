namespace text_survival.UI;

/// <summary>
/// Types of toast notifications with different visual styling.
/// </summary>
public enum ToastType
{
    Info,
    Success,
    Warning,
    Danger
}

/// <summary>A single toast notification, counting down.</summary>
public class Toast
{
    public string Message { get; init; } = "";
    public ToastType Type { get; init; }
    public float TimeRemaining { get; set; }
}

/// <summary>
/// The queue of things that just happened. Narrative writes into it; the renderer reads
/// it and ticks it down. State, not drawing - which is why game logic may touch it.
/// </summary>
public static class ToastFeed
{
    private const int MaxToasts = 5;
    private static readonly List<Toast> _toasts = [];

    public static IReadOnlyList<Toast> Active => _toasts;

    public static void Show(string message, ToastType type = ToastType.Info, float durationSeconds = 8f)
    {
        while (_toasts.Count >= MaxToasts)
            _toasts.RemoveAt(0);

        _toasts.Add(new Toast { Message = message, Type = type, TimeRemaining = durationSeconds });
    }

    /// <summary>Count the toasts down and drop the expired ones. Once per frame.</summary>
    public static void Tick(float deltaTime)
    {
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            _toasts[i].TimeRemaining -= deltaTime;
            if (_toasts[i].TimeRemaining <= 0)
                _toasts.RemoveAt(i);
        }
    }
}
