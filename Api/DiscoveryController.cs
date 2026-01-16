using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.Web.Dto;

namespace text_survival.Api;

[ApiController]
[Route("api/game/{sessionId}/discovery")]
public class DiscoveryController : GameControllerBase
{
    [HttpPost("open")]
    public ActionResult<GameResponse> Open(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        var location = ctx.CurrentLocation;

        if (string.IsNullOrEmpty(location.DiscoveryText))
            return BadRequest(new { error = "No discovery text for this location" });

        // Mark as explored
        location.Explore();

        // Record discovery for named locations
        if (!location.IsTerrainOnly)
            ctx.RecordLocationDiscovery(location.Name);

        var discoveryOverlay = new DiscoveryOverlay(new DiscoveryDto(
            location.Name,
            location.DiscoveryText
        ));

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.WithOverlay(ctx, discoveryOverlay));
    }

    [HttpPost("close")]
    public ActionResult<GameResponse> Close(string sessionId)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        // If there's a pending activity, continue to next phase
        if (ctx.PendingActivity != null)
        {
            // Check what phase we're in to determine next action
            switch (ctx.PendingActivity.Phase)
            {
                case ActivityPhase.EventPending:
                    // Show event overlay
                    if (ctx.PendingActivity.Event != null)
                    {
                        var eventDto = new EventDto(
                            Name: ctx.PendingActivity.Event.Id,
                            Description: ctx.PendingActivity.Event.Description,
                            Choices: ctx.PendingActivity.Event.Choices.Select(c =>
                                new EventChoiceDto(
                                    Id: c.Id,
                                    Label: c.Text,
                                    Description: c.Text,
                                    IsAvailable: true,
                                    Cost: null
                                )).ToList()
                        );
                        SaveGameContext(sessionId, ctx);
                        return Ok(GameResponse.WithOverlay(ctx, new EventOverlay(eventDto)));
                    }
                    break;

                case ActivityPhase.EventOutcomeShown:
                    // Show event outcome overlay
                    if (ctx.PendingActivity.Outcome != null)
                    {
                        var outcomeDto = new EventOutcomeDto(
                            Message: ctx.PendingActivity.Outcome.Description,
                            TimeAddedMinutes: 0,
                            EffectsApplied: new List<string>(),
                            DamageTaken: new List<string>(),
                            ItemsGained: new List<string>(),
                            ItemsLost: new List<string>(),
                            TensionsChanged: new List<string>()
                        );
                        SaveGameContext(sessionId, ctx);
                        return Ok(GameResponse.WithOverlay(ctx, new EventOutcomeOverlay(outcomeDto)));
                    }
                    break;
            }

            // Clear pending activity if no other phase to transition to
            ctx.PendingActivity = null;
        }

        SaveGameContext(sessionId, ctx);
        return Ok(GameResponse.Success(ctx));
    }
}
