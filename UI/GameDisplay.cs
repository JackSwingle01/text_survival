using text_survival.Actions;

namespace text_survival.UI;

public static class GameDisplay
{
    /// <summary>
    /// Record a line of narrative: into the journal, and onto the toast feed so it is
    /// seen as it happens. State only - the renderer decides when to draw it.
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
        ToastFeed.Show(text, toastType);
    }

    /// <summary>Record several lines of narrative at once.</summary>
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
            ToastFeed.Show(text, toastType);
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
}
