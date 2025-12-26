using Spectre.Console;
using Spectre.Console.Rendering;
using text_survival.Actions;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;

namespace text_survival.UI;

public static class GameDisplay
{
    private static readonly NarrativeLog _log = new();

    #region Context-aware overloads (route to WebIO when SessionId present)

    /// <summary>
    /// Add narrative with context - routes to instance log for web sessions.
    /// </summary>
    public static void AddNarrative(GameContext ctx, string text, LogLevel level = LogLevel.Normal)
    {
        if (ctx.SessionId != null)
            ctx.Log.Add(text, level);
        else
            _log.Add(text, level);
    }

    /// <summary>
    /// Add multiple narrative entries with context.
    /// </summary>
    public static void AddNarrative(GameContext ctx, IEnumerable<string> texts, LogLevel level = LogLevel.Normal)
    {
        if (ctx.SessionId != null)
            ctx.Log.AddRange(texts, level);
        else
        {
            foreach (var text in texts)
                _log.Add(text, level);
        }
    }

    public static void AddSuccess(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Success);
    public static void AddWarning(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Warning);
    public static void AddDanger(GameContext ctx, string text) => AddNarrative(ctx, text, LogLevel.Danger);

    public static void AddSeparator(GameContext ctx)
    {
        if (ctx.SessionId != null)
            ctx.Log.AddSeparator();
        else
            _log.AddSeparator();
    }

    public static void ClearNarrative(GameContext ctx)
    {
        if (ctx.SessionId != null)
            ctx.Log.Clear();
        else
            _log.Clear();
    }

    #endregion

    #region Static overloads (console-only, for backwards compatibility)

    public static void AddNarrative(string text, LogLevel level = LogLevel.Normal)
    {
        _log.Add(text, level);
    }

    public static void AddNarrative(IEnumerable<string> texts)
    {
        foreach (var text in texts)
            AddNarrative(text);
    }

    public static void AddSuccess(string text) => AddNarrative(text, LogLevel.Success);
    public static void AddWarning(string text) => AddNarrative(text, LogLevel.Warning);
    public static void AddDanger(string text) => AddNarrative(text, LogLevel.Danger);


    #endregion

    /// <summary>
    /// Render the game display with optional status text.
    /// Status text style: laconic, character perspective (e.g. "Resting." "Planning." "Thinking.")
    /// </summary>
    public static void Render(
        GameContext ctx,
        bool addSeparator = true,
        string? statusText = null,
        int? progress = null,
        int? progressTotal = null)
    {
        if (addSeparator)
            ctx.Log.AddSeparator();
        Web.WebIO.Render(ctx, statusText, progress, progressTotal);
        return;
    }

    /// <summary>
    /// Render a progress loop with status panel updates. Updates game time by default.
    /// Returns elapsed time and whether an event interrupted the operation.
    /// </summary>
    public static (int elapsed, bool interrupted) UpdateAndRenderProgress(GameContext ctx, string statusText, int minutes, ActivityType activity, bool updateTime = true)
    {
        // Send estimated duration to client for local animation
        Web.WebIO.RenderWithDuration(ctx, statusText, minutes);

        // Process all time at once - ctx.Update handles minute-by-minute internally
        int elapsed = updateTime ? ctx.Update(minutes, activity) : minutes;

        return (elapsed, ctx.EventOccurredLastUpdate);
    }


    // #region Inventory Screen

    public static void RenderInventoryScreen(GameContext ctx, Inventory? inventory = null, string? title = null)
    {
        var inv = inventory ?? ctx.Inventory;
        var headerTitle = title ?? "INVENTORY";

        // Route to web UI when session is active
        if (ctx.SessionId != null)
        {
            Web.WebIO.RenderInventory(ctx, inv, headerTitle);
            return;
        }
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
