using text_survival.Actions;
using text_survival.Web.Dto;

namespace text_survival.Web;

/// <summary>
/// Web-based I/O implementation. Sends frames via WebSocket and waits for responses.
/// All methods are synchronous to match the game's blocking I/O model.
/// </summary>
public static class WebIO
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMinutes(5);

    private static WebGameSession GetSession(GameContext ctx) =>
        SessionRegistry.Get(ctx.SessionId)
        ?? throw new InvalidOperationException($"No session found for ID: {ctx.SessionId}");

    /// <summary>
    /// Present a selection menu and wait for player choice.
    /// </summary>
    public static T Select<T>(GameContext ctx, string prompt, IEnumerable<T> choices, Func<T, string> display)
    {
        var session = GetSession(ctx);
        var list = choices.ToList();

        if (list.Count == 0)
            throw new ArgumentException("Choices cannot be empty", nameof(choices));

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new InputRequestDto("select", prompt, list.Select(display).ToList()),
            null
        );

        session.Send(frame);
        var response = session.WaitForResponse(ResponseTimeout);

        int index = Math.Clamp(response.ChoiceIndex ?? 0, 0, list.Count - 1);
        return list[index];
    }

    /// <summary>
    /// Present a yes/no confirmation and wait for player choice.
    /// </summary>
    public static bool Confirm(GameContext ctx, string prompt)
    {
        var session = GetSession(ctx);

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new InputRequestDto("confirm", prompt, null),
            null
        );

        session.Send(frame);
        var response = session.WaitForResponse(ResponseTimeout);

        // 0 = Yes, 1 = No (matches button order in frontend)
        return response.ChoiceIndex == 0;
    }

    /// <summary>
    /// Wait for any key press (continue button in web UI).
    /// </summary>
    public static void WaitForKey(GameContext ctx, string message = "Press any key to continue...")
    {
        var session = GetSession(ctx);

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new InputRequestDto("anykey", message, null),
            null
        );

        session.Send(frame);
        session.WaitForResponse(ResponseTimeout);
    }

    /// <summary>
    /// Render current game state without requesting input.
    /// </summary>
    public static void Render(GameContext ctx, string? statusText = null, int? progress = null, int? total = null)
    {
        var session = SessionRegistry.Get(ctx.SessionId);
        if (session == null) return; // Silently skip if no session

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            null,
            progress.HasValue ? new ProgressDto(progress.Value, total ?? progress.Value) : null,
            statusText
        );

        session.Send(frame);
    }

    /// <summary>
    /// Add narrative text to the context's log for web display.
    /// </summary>
    public static void AddNarrative(GameContext ctx, string text, UI.LogLevel level = UI.LogLevel.Normal)
    {
        ctx.Log.Add(text, level);
    }

    /// <summary>
    /// Add multiple narrative entries.
    /// </summary>
    public static void AddNarrative(GameContext ctx, IEnumerable<string> texts, UI.LogLevel level = UI.LogLevel.Normal)
    {
        ctx.Log.AddRange(texts, level);
    }

    /// <summary>
    /// Render inventory screen overlay.
    /// </summary>
    public static void RenderInventory(GameContext ctx, Items.Inventory inventory, string title)
    {
        var session = SessionRegistry.Get(ctx.SessionId);
        if (session == null) return;

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            null,
            null,
            null,
            InventoryDto.FromInventory(inventory, title)
        );

        session.Send(frame);
    }
}
