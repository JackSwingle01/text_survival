using text_survival.Actions;

namespace text_survival.UI;

public static class GameDisplay
{
    /// <summary>
    /// Record a line of narrative. The persistent HUD shows recent entries and full retained history.
    /// </summary>
    public static void AddNarrative(GameContext ctx, string text, LogLevel level = LogLevel.Normal)
    {
        var timestamp = ctx.GameTime.ToString("h:mm");
        ctx.Log.Add(text, level, timestamp);

    }

    /// <summary>Record several lines of narrative at once.</summary>
    public static void AddNarrative(GameContext ctx, IEnumerable<string> texts, LogLevel level = LogLevel.Normal)
    {
        var timestamp = ctx.GameTime.ToString("h:mm");
        ctx.Log.AddRange(texts, level, timestamp);

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
