using text_survival.Actions;
using text_survival.Crafting;
using text_survival.Web.Dto;

namespace text_survival.Web;

/// <summary>
/// Web-based I/O implementation. Sends frames via WebSocket and waits for responses.
/// All methods are synchronous to match the game's blocking I/O model.
/// </summary>
public static class WebIO
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<string, InventoryDto> _currentInventory = new();
    private static readonly Dictionary<string, CraftingDto> _currentCrafting = new();

    private static WebGameSession GetSession(GameContext ctx) =>
        SessionRegistry.Get(ctx.SessionId)
        ?? throw new InvalidOperationException($"No session found for ID: {ctx.SessionId}");

    private static InventoryDto? GetInventory(string? sessionId)
    {
        if (sessionId != null && _currentInventory.TryGetValue(sessionId, out var dto))
            return dto;
        return null;
    }

    private static CraftingDto? GetCrafting(string? sessionId)
    {
        if (sessionId != null && _currentCrafting.TryGetValue(sessionId, out var dto))
            return dto;
        return null;
    }

    /// <summary>
    /// Clear the current inventory display for a session.
    /// </summary>
    public static void ClearInventory(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentInventory.Remove(ctx.SessionId);
    }

    /// <summary>
    /// Clear the current crafting display for a session.
    /// </summary>
    public static void ClearCrafting(GameContext ctx)
    {
        if (ctx.SessionId != null)
            _currentCrafting.Remove(ctx.SessionId);
    }

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
            null,
            null,
            GetInventory(ctx.SessionId),
            GetCrafting(ctx.SessionId)
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
            null,
            null,
            GetInventory(ctx.SessionId),
            GetCrafting(ctx.SessionId)
        );

        session.Send(frame);
        var response = session.WaitForResponse(ResponseTimeout);

        // 0 = Yes, 1 = No (matches button order in frontend)
        return response.ChoiceIndex == 0;
    }

    /// <summary>
    /// Wait for any key press (continue button in web UI).
    /// </summary>
    public static void WaitForKey(GameContext ctx, string message = "Continue")
    {
        var session = GetSession(ctx);

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new InputRequestDto("anykey", message, null),
            null,
            null,
            GetInventory(ctx.SessionId),
            GetCrafting(ctx.SessionId)
        );

        session.Send(frame);
        session.WaitForResponse(ResponseTimeout);
    }

    /// <summary>
    /// Present a numeric selection and wait for player choice.
    /// Shows as buttons with the available numbers.
    /// </summary>
    public static int ReadInt(GameContext ctx, string prompt, int min, int max)
    {
        var session = GetSession(ctx);

        var choices = Enumerable.Range(min, max - min + 1).Select(n => n.ToString()).ToList();

        var frame = new WebFrame(
            GameStateDto.FromContext(ctx),
            new InputRequestDto("select", prompt, choices),
            null,
            null,
            GetInventory(ctx.SessionId),
            GetCrafting(ctx.SessionId)
        );

        session.Send(frame);
        var response = session.WaitForResponse(ResponseTimeout);

        int index = Math.Clamp(response.ChoiceIndex ?? 0, 0, choices.Count - 1);
        return min + index;
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
            statusText,
            null,  // No inventory in render-only frames
            null   // No crafting in render-only frames
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
    /// Set the inventory to display. Will be included in subsequent input frames.
    /// </summary>
    public static void RenderInventory(GameContext ctx, Inventory inventory, string title)
    {
        if (ctx.SessionId == null) return;
        _currentInventory[ctx.SessionId] = InventoryDto.FromInventory(inventory, title);
    }

    /// <summary>
    /// Set the crafting screen to display. Will be included in subsequent input frames.
    /// </summary>
    public static void RenderCrafting(GameContext ctx, NeedCraftingSystem crafting, string title = "CRAFTING")
    {
        if (ctx.SessionId == null) return;
        _currentCrafting[ctx.SessionId] = CraftingDto.FromContext(ctx, crafting);
    }
}
