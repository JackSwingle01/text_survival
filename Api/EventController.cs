using Microsoft.AspNetCore.Mvc;
using text_survival.Actions;
using text_survival.UI;
using text_survival.Web.Dto;

namespace text_survival.Api;

public record EventChoiceRequest(string ChoiceId);

[ApiController]
[Route("api/game/{sessionId}/event")]
public class EventController : GameControllerBase
{
    [HttpPost("choice")]
    public ActionResult<GameResponse> Choice(string sessionId, [FromBody] EventChoiceRequest req)
    {
        var ctxResult = LoadGameContext(sessionId);
        if (ctxResult.Value == null) return ctxResult.Result!;
        var ctx = ctxResult.Value;

        if (ctx.PendingActivity?.Phase != ActivityPhase.EventPending)
            return BadRequest(new { error = "No pending event" });

        if (ctx.PendingActivity.EventSource == null)
            return BadRequest(new { error = "Event source not available - may have been lost during save/load" });

        var choice = ctx.PendingActivity.EventSource.FindChoiceById(ctx, req.ChoiceId);
        if (choice == null)
            return BadRequest(new { error = $"Invalid choice: {req.ChoiceId}" });

        var outcome = choice.DetermineResult();
        var outcomeData = GameEventRegistry.HandleOutcome(ctx, outcome);

        ctx.PendingActivity.SelectedChoiceId = req.ChoiceId;
        ctx.PendingActivity.Outcome = new EventOutcomeSnapshot(
            Description: outcome.Message,
            AbortsAction: outcome.AbortsAction
        );
        ctx.PendingActivity.Phase = ActivityPhase.EventOutcomeShown;

        if (outcome.ChainEvent != null)
        {
            var chainedEvent = outcome.ChainEvent(ctx);
            ctx.PendingActivity.EventSource = chainedEvent;
        }

        if (outcome.SpawnEncounter != null)
        {
            ctx.QueueEncounter(outcome.SpawnEncounter);
        }

        var frame = BuildEventResponseFrame(ctx);
        SaveGameContext(sessionId, ctx);
        return Ok(new GameResponse(frame));
    }

    private static WebFrame BuildEventResponseFrame(GameContext ctx)
    {
        if (ctx.PendingActivity == null)
            return GameEngine.BuildFrame(ctx);

        var overlays = new List<Overlay>();

        switch (ctx.PendingActivity.Phase)
        {
            case ActivityPhase.EventPending:
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
                            )
                        ).ToList()
                    );
                    overlays.Add(new EventOverlay(eventDto));
                }
                break;

            case ActivityPhase.EventOutcomeShown:
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
                    overlays.Add(new EventOutcomeOverlay(outcomeDto));
                }
                break;
        }

        return GameEngine.BuildFrame(ctx, overlays: overlays);
    }
}
