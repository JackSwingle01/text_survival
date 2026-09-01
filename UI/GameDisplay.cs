using text_survival.Actions;
using text_survival.Desktop.UI;

namespace text_survival.UI;

public static class GameDisplay
{
    #region Context-aware overloads (route to DesktopIO when SessionId present)

    /// <summary>
    /// Add narrative with context - routes to instance log for web sessions.
    /// Also shows a toast notification for immediate feedback.
    /// </summary>
    public static void AddNarrative(GameContext ctx, string text, LogLevel level = LogLevel.Normal)
    {
        var timestamp = ctx.GameTime.ToString("h:mm");
        ctx.Log.Add(text, level, timestamp);

        // Show toast notification for immediate feedback
        var toastType = level switch
        {
            LogLevel.Success => ToastType.Success,
            LogLevel.Warning => ToastType.Warning,
            LogLevel.Danger => ToastType.Danger,
            LogLevel.Discovery => ToastType.Success,
            _ => ToastType.Info
        };
        ToastManager.Show(text, toastType);
    }

    /// <summary>
    /// Add multiple narrative entries with context.
    /// </summary>
    public static void AddNarrative(GameContext ctx, IEnumerable<string> texts, LogLevel level = LogLevel.Normal)
    {
        var timestamp = ctx.GameTime.ToString("h:mm");
        ctx.Log.AddRange(texts, level, timestamp);

        // Show toast for each message
        var toastType = level switch
        {
            LogLevel.Success => ToastType.Success,
            LogLevel.Warning => ToastType.Warning,
            LogLevel.Danger => ToastType.Danger,
            LogLevel.Discovery => ToastType.Success,
            _ => ToastType.Info
        };
        foreach (var text in texts)
        {
            ToastManager.Show(text, toastType);
        }
    }

    public static void AddSuccess(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Success);
    public static void AddWarning(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Warning);
    public static void AddDanger(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Danger);
    public static void AddDiscovery(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Discovery);

    public static void ClearNarrative(GameContext ctx)
    {
        ctx.Log.Clear();
    }

    #endregion

    /// <summary>
    /// Draw a frame so queued narrative reaches the screen before the next blocking step.
    /// </summary>
    public static void Render(GameContext ctx) => Desktop.DesktopIO.Render(ctx);

    /// <summary>
    /// Run a progress bar that simulates the time it displays. Returns elapsed time and
    /// whether an event interrupted the operation.
    /// </summary>
    public static (int elapsed, bool interrupted) UpdateAndRenderProgress(GameContext ctx, string statusText, int minutes, ActivityType activity)
        => Desktop.DesktopIO.RenderWithDuration(ctx, statusText, minutes, activity);

}
