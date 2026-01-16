using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record ConfirmRequest(bool Confirmed);

[ApiController]
[Route("api/game/{sessionId}")]
public class UniversalController : GameControllerBase
{
    [HttpPost("continue")]
    public ActionResult<GameResponse> Continue(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        // Handle pending activity continuation based on current phase
        if (ctx.PendingActivity != null)
        {
            switch (ctx.PendingActivity.Phase)
            {
                case ActivityPhase.EventOutcomeShown:
                    // Check if there's a chained event to show next
                    if (ctx.PendingActivity.EventSource != null &&
                        ctx.PendingActivity.Outcome != null)
                    {
                        // Record if the event aborted the action
                        ctx.LastEventAborted = ctx.PendingActivity.Outcome.AbortsAction;
                    }

                    // Clear the pending activity
                    ctx.PendingActivity = null;
                    break;

                case ActivityPhase.HuntResult:
                    ctx.PendingActivity = null;
                    break;

                case ActivityPhase.EncounterOutcome:
                    ctx.PendingActivity = null;
                    break;

                case ActivityPhase.CombatResult:
                    // Create carcass on victory - check snapshot's AnimalHealth
                    if (ctx.PendingActivity.Combat?.AnimalHealth <= 0 && ctx.PendingActivity.CombatScenario != null)
                    {
                        // Find the enemy (first animal in Team2)
                        var deadEnemy = ctx.PendingActivity.CombatScenario.Team2.FirstOrDefault();
                        if (deadEnemy?.actor is Actors.Animals.Animal deadAnimal)
                        {
                            var carcass = new Environments.Features.CarcassFeature(deadAnimal);
                            ctx.CurrentLocation.AddFeature(carcass);
                        }
                    }
                    ctx.PendingActivity = null;
                    break;

                default:
                    // For other phases, just clear the activity
                    ctx.PendingActivity = null;
                    break;
            }
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }

    [HttpPost("confirm")]
    public ActionResult<GameResponse> Confirm(string sessionId, [FromBody] ConfirmRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        // Handle confirmation based on context
        // Currently just returns success - logic can be extended as needed

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }
}
