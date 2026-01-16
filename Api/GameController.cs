using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Persistence;
using text_survival.Web.Dto;

namespace text_survival.Api;

/// <summary>Response for new game creation.</summary>
public record NewGameResponse(string SessionId, WebFrame InitialState);

/// <summary>Request body for game actions.</summary>
public record ActionRequest(string ChoiceId);

/// <summary>
/// Main game controller with single action endpoint.
/// All game actions route through POST /action and are dispatched by ActionRouter.
/// </summary>
[ApiController]
[Route("api/game")]
public class GameController : GameControllerBase
{
    /// <summary>
    /// Create a new game session.
    /// </summary>
    [HttpPost("new")]
    public ActionResult<NewGameResponse> NewGame()
    {
        var ctx = GameEngine.CreateNewGame();
        var sessionId = Guid.NewGuid().ToString();
        ctx.SessionId = sessionId;

        SaveGameContext(sessionId, ctx);

        var frame = GameEngine.BuildFrame(ctx);
        return Ok(new NewGameResponse(sessionId, frame));
    }

    /// <summary>
    /// Get current game state.
    /// </summary>
    [HttpGet("{sessionId}/state")]
    public ActionResult<GameResponse> GetState(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        return Ok(GameResponse.Success(ctx));
    }

    /// <summary>
    /// Process a game action. All actions route through this single endpoint.
    /// The ActionRouter dispatches based on current pending activity phase.
    /// </summary>
    [HttpPost("{sessionId}/action")]
    public ActionResult<GameResponse> Action(string sessionId, [FromBody] ActionRequest request)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var response = ActionRouter.ProcessAction(ctx, request.ChoiceId);

        if (!response.IsError)
        {
            SaveGameContext(sessionId, ctx);
        }

        return Ok(response);
    }
}
