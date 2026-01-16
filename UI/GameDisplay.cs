using text_survival.Actions;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.UI;

public static class GameDisplay
{
    #region Context-aware overloads (route to WebIO when SessionId present)

    /// <summary>
    /// Add narrative with context - routes to instance log for web sessions.
    /// </summary>
    public static void AddNarrative(GameContext ctx, string text, LogLevel level = LogLevel.Normal)
    {
        var timestamp = ctx.GameTime.ToString("h:mm");
        ctx.Log.Add(text, level, timestamp);
    }

    /// <summary>
    /// Add multiple narrative entries with context.
    /// </summary>
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

    #endregion

    /// <summary>
    /// Render the game display with optional status text.
    /// Status text style: laconic, character perspective (e.g. "Resting." "Planning." "Thinking.")
    /// </summary>
    public static void Render(
        GameContext ctx,
        bool addSeparator = true,
        string? statusText = null)
    {
        if (addSeparator)
            ctx.Log.AddSeparator();
        Web.WebIO.Render(ctx, statusText);
        return;
    }

    /// <summary>
    /// Render a progress loop with status panel updates. Updates game time by default.
    /// Returns elapsed time and whether an event interrupted the operation.
    /// </summary>
    public static (int elapsed, bool interrupted) UpdateAndRenderProgress(GameContext ctx, string statusText, int minutes, ActivityType activity, bool updateTime = true)
    {
        // Process time first so frame contains updated state for animation deltas
        int elapsed = updateTime ? ctx.Update(minutes, activity) : minutes;

        // Skip progress animation if work was aborted by an event/encounter.
        // User's attention was on the event, not watching work progress.
        if (!ctx.LastEventAborted)
        {
            Web.WebIO.RenderWithDuration(ctx, statusText, elapsed);
        }

        return (elapsed, ctx.EventOccurredLastUpdate);
    }

    /// <summary>
    /// Render the crafting screen showing all categories and recipes.
    /// </summary>
    public static void RenderCraftingScreen(GameContext ctx, Crafting.NeedCraftingSystem crafting, string? title = null)
    {
        var headerTitle = title ?? "CRAFTING";

        Web.WebIO.RenderCrafting(ctx, crafting, headerTitle);
        return;

    }

}
