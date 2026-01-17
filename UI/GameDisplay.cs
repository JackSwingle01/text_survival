using text_survival.Actions;
using text_survival.Desktop.UI;
using text_survival.Effects;
using text_survival.Environments.Features;
using text_survival.IO;
using text_survival.Items;
using text_survival.Survival;

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

    public static void AddSeparator(GameContext ctx)
    {
        ctx.Log.AddSeparator();
    }

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
        Desktop.DesktopIO.Render(ctx, statusText);
        return;
    }

    /// <summary>
    /// Render a progress loop with status panel updates. Simulates time incrementally during progress bar.
    /// Returns elapsed time and whether an event interrupted the operation.
    /// </summary>
    public static (int elapsed, bool interrupted) UpdateAndRenderProgress(GameContext ctx, string statusText, int minutes, ActivityType activity, bool updateTime = true)
    {
        if (!updateTime)
        {
            // Legacy path - just animate without simulation
            Desktop.DesktopIO.RenderWithDuration(ctx, statusText, minutes);
            return (minutes, false);
        }

        // New path - ShowProgress handles both simulation and animation
        return Desktop.DesktopIO.RenderWithDuration(ctx, statusText, minutes, activity);
    }


    // #region Inventory Screen

    public static void RenderInventoryScreen(GameContext ctx, Inventory? inventory = null, string? title = null)
    {
        var inv = inventory ?? ctx.Inventory;
        var headerTitle = title ?? "INVENTORY";

        // Route to web UI when session is active
        if (ctx.SessionId != null)
        {
            Desktop.DesktopIO.RenderInventory(ctx, inv, headerTitle);
            return;
        }
    }

    /// <summary>
    /// Render the crafting screen showing all categories and recipes.
    /// </summary>
    public static void RenderCraftingScreen(GameContext ctx, Crafting.NeedCraftingSystem crafting, string? title = null)
    {
        var headerTitle = title ?? "CRAFTING";

        Desktop.DesktopIO.RenderCrafting(ctx, crafting, headerTitle);
        return;

    }

}
