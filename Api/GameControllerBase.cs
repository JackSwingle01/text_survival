using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Persistence;

namespace text_survival.Api;

/// <summary>
/// Base controller with centralized load/save logic for game sessions.
/// </summary>
[Route("api/game/{sessionId:guid}")]
public abstract class GameControllerBase : ControllerBase
{
    /// <summary>
    /// Load game context from session ID. Returns NotFound if session doesn't exist.
    /// Logs errors to console and returns 500 if load fails.
    /// </summary>
    protected ActionResult<GameContext> LoadGameContext(string sessionId)
    {
        if (!SaveManager.HasSaveFile(sessionId))
        {
            Console.WriteLine($"[API] Session not found: {sessionId}");
            return NotFound(new { error = "Session not found" });
        }

        var (ctx, error) = SaveManager.Load(sessionId);
        if (ctx != null)
        {
            ctx.SessionId = sessionId;
            return ctx;
        }

        // Load failed - log and return error
        Console.WriteLine($"[API] Save load failed for {sessionId}: {error}");
        return StatusCode(500, new { error = "Failed to load game state" });
    }

    /// <summary>
    /// Save game context. Logs errors to console if save fails.
    /// </summary>
    protected void SaveGameContext(string sessionId, GameContext ctx)
    {
        ctx.SessionId = sessionId;

        try
        {
            SaveManager.Save(ctx);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Save failed for {sessionId}: {ex.Message}");
            throw;
        }
    }

}
